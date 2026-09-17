using CgPos.Contratos.Ventas;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Ventas;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Ecf;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Pos.Infraestructura.Sincronizacion;
using CgPos.Pos.Infraestructura.Ventas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.ECF;

/// <summary>
/// Emite el e-CF de las ventas que se cobraron en contingencia, en cuanto la caja vuelve a poder firmarlas. Cada una se emite en su
/// propia transacción: si una falla (por ejemplo, sigue sin secuencia), las demás se emiten igual y esa se reintenta después.
/// </summary>
internal sealed class RegularizacionContingencia(
    ContextoDatosPos contexto,
    IEmisorComprobantes emisorEcf,
    IBandejaSalida bandejaSalida,
    IAuditoria auditoria,
    IParametros parametros,
    TimeProvider reloj,
    ILogger<RegularizacionContingencia> registro) : IRegularizacionContingencia
{
    public async Task<ResultadoRegularizacion> RegularizarAsync(int cajaId, CancellationToken cancelacion = default)
    {
        var pendientes = await contexto.ComprobantesContingencia
            .Where(c => c.CajaId == cajaId && c.RegularizadoEn == null)
            .OrderBy(c => c.CreadoEn)
            .ToListAsync(cancelacion);

        if (pendientes.Count == 0)
            return new ResultadoRegularizacion(0, 0, null);

        var montoIdentificacion = await parametros.ObtenerDecimalAsync(ClavesParametros.MontoIdentificacionConsumo, cajaId, cancelacion);
        var emitidos = 0;
        string? ultimoError = null;

        foreach (var contingencia in pendientes)
        {
            var venta = await contexto.Ventas.Include(v => v.Lineas).Include(v => v.Pagos).Include(v => v.DestinosEntrega)
                .SingleOrDefaultAsync(v => v.Id == contingencia.VentaId, cancelacion);
            if (venta is null || venta.Estado != EstadoVenta.Cobrada)
            {
                contingencia.RegistrarIntento("La venta del comprobante provisional ya no está cobrada.");
                continue;
            }

            await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
            try
            {
                var emision = await emisorEcf.EmitirAsync(venta, cancelacion);
                var ahora = reloj.GetUtcNow();
                contingencia.Regularizar(emision.Documento.Encf, ahora);

                // El Central recibió la venta sin e-CF: se le reenvía con el comprobante ya firmado.
                var datos = venta.ADatos(montoIdentificacion, emision.Documento, emision.VenceSecuencia);
                bandejaSalida.Encolar("Venta.Cobrada", venta.NumeroTransaccion,
                    await contexto.VentaCobradaAsync(venta, datos, emision.ParaCentral, ahora, cancelacion));
                auditoria.Registrar(new EntradaAuditoria("Ecf.ContingenciaRegularizada", "Venta", venta.NumeroTransaccion,
                    Detalle: new { contingencia.Numero, emision.Documento.Encf, contingencia.Motivo }));

                await contexto.SaveChangesAsync(cancelacion);
                await transaccion.CommitAsync(cancelacion);
                emitidos++;
            }
            catch (EmisionEcfExcepcion excepcion)
            {
                await transaccion.RollbackAsync(cancelacion);
                contexto.ChangeTracker.Clear();
                ultimoError = excepcion.Message;

                // El intento fallido se guarda fuera de la transacción de la emisión.
                var seguimiento = await contexto.ComprobantesContingencia.SingleAsync(c => c.Id == contingencia.Id, cancelacion);
                seguimiento.RegistrarIntento(excepcion.Message);
                await contexto.SaveChangesAsync(cancelacion);
                registro.LogWarning("El e-CF de la venta {Venta} sigue pendiente: {Error}", contingencia.VentaNumero, excepcion.Message);
                break;
            }
        }

        if (contexto.ChangeTracker.HasChanges())
            await contexto.SaveChangesAsync(cancelacion);

        if (emitidos > 0)
            registro.LogInformation("Contingencia: se emitieron {Emitidos} e-CF pendientes", emitidos);

        var quedan = await contexto.ComprobantesContingencia.CountAsync(c => c.CajaId == cajaId && c.RegularizadoEn == null, cancelacion);
        return new ResultadoRegularizacion(emitidos, quedan, ultimoError);
    }
}
