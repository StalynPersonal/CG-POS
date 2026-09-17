using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Devoluciones;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Devoluciones;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Devoluciones;

internal sealed class ServicioNotasCreditoCentral(ContextoDatosCentral contexto, IParametrosCentral parametros, IAuditoriaCentral auditoria, TimeProvider reloj)
    : IServicioNotasCreditoCentral
{
    private DateOnly Hoy => DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);

    public async Task<DatosNotaCreditoCentral?> BuscarAsync(string codigo, CancellationToken cancelacion = default)
    {
        var buscado = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        if (buscado.Length == 0)
            return null;

        var nota = await contexto.NotasCredito.AsNoTracking().FirstOrDefaultAsync(n => n.Encf == buscado || n.Numero == buscado, cancelacion);
        return nota is null ? null : (await DatosAsync([nota], cancelacion))[0];
    }

    public async Task<RespuestaReservaNotaCredito> ReservarAsync(Guid notaCreditoId, Guid cajaId, decimal monto, CancellationToken cancelacion = default)
    {
        if (monto <= 0)
            return new RespuestaReservaNotaCredito(false, "El monto a reservar debe ser mayor que cero.");

        var minutos = await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.NotasCreditoMinutosReserva, cancelacion);
        var ahora = reloj.GetUtcNow();

        // La fila de la nota se bloquea mientras se calcula el disponible: dos cajas no pueden reservar el mismo saldo.
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        var nota = await contexto.NotasCredito
            .FromSql($"SELECT * FROM NotasCredito WITH (UPDLOCK, ROWLOCK) WHERE Id = {notaCreditoId}")
            .SingleOrDefaultAsync(cancelacion);
        if (nota is null)
            return new RespuestaReservaNotaCredito(false, "La nota de crédito no existe en el Central.");

        if (nota.Estado(Hoy) == EstadoNotaCreditoCentral.Vencida)
            return new RespuestaReservaNotaCredito(false, $"La nota de crédito {nota.Encf ?? nota.Numero} venció el {nota.VenceEn:dd/MM/yyyy}.");

        var reservas = await contexto.ReservasNotaCredito.Where(r => r.NotaCreditoId == nota.Id && r.CerradaEn == null).ToListAsync(cancelacion);
        foreach (var vencida in reservas.Where(r => !r.EstaVigente(ahora)))
            vencida.Cerrar("Vencida", ahora);

        var disponible = nota.Saldo - reservas.Where(r => r.EstaVigente(ahora)).Sum(r => r.Monto);
        if (disponible <= 0)
            return new RespuestaReservaNotaCredito(false, $"La nota de crédito {nota.Encf ?? nota.Numero} no tiene saldo disponible.");

        var reservado = Math.Min(monto, disponible);
        var reserva = ReservaNotaCreditoCentral.Crear(nota.Id, cajaId, reservado, ahora, TimeSpan.FromMinutes(minutos));
        contexto.ReservasNotaCredito.Add(reserva);
        await contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        var datos = (await DatosAsync([nota], cancelacion))[0];
        var mensaje = reservado < monto ? $"Solo hay {reservado:N2} {nota.Moneda} disponible en la nota de crédito." : null;
        return new RespuestaReservaNotaCredito(true, mensaje, reserva.Id, reservado, reserva.VenceEn, datos);
    }

    public async Task<bool> LiberarReservaAsync(Guid reservaId, Guid cajaId, CancellationToken cancelacion = default)
    {
        var reserva = await contexto.ReservasNotaCredito.SingleOrDefaultAsync(r => r.Id == reservaId && r.CajaId == cajaId, cancelacion);
        if (reserva is null || reserva.CerradaEn is not null)
            return false;

        reserva.Cerrar("Liberada", reloj.GetUtcNow());
        await contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    public async Task<PaginaNotasCreditoCentral> ListarAsync(string? buscar, EstadoNotaCreditoCentral? estado, bool soloSobregiradas, int pagina, int tamano,
        CancellationToken cancelacion = default)
    {
        tamano = Math.Clamp(tamano, 1, IServicioNotasCreditoCentral.TamanoMaximoPagina);
        pagina = Math.Max(pagina, 0);
        var hoy = Hoy;

        var consulta = contexto.NotasCredito.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var texto = buscar.Trim();
            var codigo = texto.ToUpperInvariant();
            consulta = consulta.Where(n => n.Numero == codigo || n.Encf == codigo || n.ClienteDocumento.Contains(texto) || n.ClienteNombre.Contains(texto));
        }

        consulta = estado switch
        {
            EstadoNotaCreditoCentral.Consumida => consulta.Where(n => n.Total - n.Consumido <= 0),
            EstadoNotaCreditoCentral.Vencida => consulta.Where(n => n.Total - n.Consumido > 0 && n.VenceEn < hoy),
            EstadoNotaCreditoCentral.Vigente => consulta.Where(n => n.Total - n.Consumido > 0 && n.VenceEn >= hoy),
            _ => consulta,
        };

        if (soloSobregiradas)
            consulta = consulta.Where(n => n.Consumido > n.Total);

        var total = await consulta.CountAsync(cancelacion);
        var notas = await consulta.OrderByDescending(n => n.EmitidaEn).Skip(pagina * tamano).Take(tamano).ToListAsync(cancelacion);
        return new PaginaNotasCreditoCentral(await DatosAsync(notas, cancelacion), total);
    }

    public async Task<IReadOnlyList<DatosMovimientoNotaCredito>> ListarMovimientosAsync(Guid notaCreditoId, CancellationToken cancelacion = default)
    {
        var consumos = await contexto.ConsumosNotaCredito.AsNoTracking().Where(c => c.NotaCreditoId == notaCreditoId).ToListAsync(cancelacion);
        var reservas = await contexto.ReservasNotaCredito.AsNoTracking().Where(r => r.NotaCreditoId == notaCreditoId).ToListAsync(cancelacion);
        var nota = await contexto.NotasCredito.AsNoTracking().SingleOrDefaultAsync(n => n.Id == notaCreditoId, cancelacion);
        var cajas = await CodigosCajasAsync(consumos.Select(c => c.CajaId).Concat(reservas.Select(r => r.CajaId)), cancelacion);
        var ahora = reloj.GetUtcNow();

        var movimientos = consumos
            .Select(c => new DatosMovimientoNotaCredito(c.Fecha, "Consumo", cajas.GetValueOrDefault(c.CajaId) ?? string.Empty, c.Monto, $"Venta {c.VentaNumero}"))
            .Concat(reservas.Select(r => new DatosMovimientoNotaCredito(r.CreadaEn, "Reserva", cajas.GetValueOrDefault(r.CajaId) ?? string.Empty, r.Monto,
                r.Cierre ?? (r.EstaVigente(ahora) ? "Vigente" : "Vencida"))))
            .ToList();

        if (nota is { ProrrogadaEn: { } prorrogada })
            movimientos.Add(new DatosMovimientoNotaCredito(prorrogada, "Prórroga", string.Empty, 0m,
                $"Hasta {nota.VenceEn:dd/MM/yyyy} por {nota.ProrrogadaPor}: {nota.MotivoProrroga}"));

        return movimientos.OrderByDescending(m => m.Fecha).ToList();
    }

    public async Task<ResultadoAdministracion> ProrrogarAsync(Guid notaCreditoId, DateOnly venceEn, string motivo, UsuarioAuditoria actor,
        CancellationToken cancelacion = default)
    {
        var meses = await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.NotasCreditoMesesMaximoProrroga, cancelacion);
        if (await contexto.NotasCredito.SingleOrDefaultAsync(n => n.Id == notaCreditoId, cancelacion) is not { } nota)
            return ResultadoAdministracion.Inexistente("La nota de crédito no existe.");

        var limite = DateOnly.FromDateTime(nota.EmitidaEn.LocalDateTime).AddMonths(meses);
        if (venceEn > limite)
            return ResultadoAdministracion.Error($"La nueva fecha no puede pasar del {limite:dd/MM/yyyy}: son {meses} meses desde la emisión.");
        if (venceEn < Hoy)
            return ResultadoAdministracion.Error("La nueva fecha ya pasó.");

        try
        {
            nota.Prorrogar(venceEn, actor.Nombre, motivo, reloj.GetUtcNow());
        }
        catch (ArgumentException excepcion)
        {
            return ResultadoAdministracion.Error(ValidacionMaestros.MensajeError(excepcion));
        }

        auditoria.Registrar(new EntradaAuditoria("NotasCredito.Prorrogada", "NotaCreditoCentral", nota.Id.ToString(),
            Detalle: new { nota.Numero, nota.Encf, nota.VenceEn, Motivo = motivo, Usuario = actor.Nombre }));
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(nota.Id);
    }

    private async Task<IReadOnlyList<DatosNotaCreditoCentral>> DatosAsync(IReadOnlyList<NotaCreditoCentral> notas, CancellationToken cancelacion)
    {
        if (notas.Count == 0)
            return [];

        var ahora = reloj.GetUtcNow();
        var hoy = Hoy;
        var ids = notas.Select(n => n.Id).ToList();
        var reservado = (await contexto.ReservasNotaCredito.AsNoTracking()
                .Where(r => ids.Contains(r.NotaCreditoId) && r.CerradaEn == null && r.VenceEn > ahora)
                .GroupBy(r => r.NotaCreditoId)
                .Select(g => new { NotaCreditoId = g.Key, Monto = g.Sum(r => r.Monto) })
                .ToListAsync(cancelacion))
            .ToDictionary(r => r.NotaCreditoId, r => r.Monto);

        var cajas = await CodigosCajasAsync(notas.Select(n => n.CajaId), cancelacion);
        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Codigo.ToString("00"), cancelacion);

        return notas.Select(nota =>
        {
            var retenido = reservado.GetValueOrDefault(nota.Id);
            return new DatosNotaCreditoCentral(nota.Id, nota.Numero, nota.Encf, nota.SucursalId, sucursales.GetValueOrDefault(nota.SucursalId) ?? string.Empty,
                cajas.GetValueOrDefault(nota.CajaId) ?? string.Empty, nota.ClienteDocumento, nota.ClienteNombre, nota.Moneda, nota.Total, nota.Consumido, retenido,
                Math.Max(0m, nota.Saldo - retenido), nota.VenceEn, nota.Estado(hoy), nota.Sobregirada, nota.EmitidaEn, nota.ProrrogadaEn, nota.ProrrogadaPor,
                nota.MotivoProrroga);
        }).ToList();
    }

    private async Task<Dictionary<Guid, string>> CodigosCajasAsync(IEnumerable<Guid> ids, CancellationToken cancelacion)
    {
        var buscar = ids.Distinct().ToList();
        return buscar.Count == 0
            ? []
            : await contexto.Cajas.AsNoTracking().Where(c => buscar.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Codigo.ToString("00"), cancelacion);
    }
}
