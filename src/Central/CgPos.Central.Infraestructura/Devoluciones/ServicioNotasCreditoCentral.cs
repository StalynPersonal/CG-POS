using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Devoluciones;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Organizacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Devoluciones;

internal sealed class ServicioNotasCreditoCentral(ContextoDatosCentral contexto, IParametrosCentral parametros, TimeProvider reloj)
    : IServicioNotasCreditoCentral
{
    private DateOnly Hoy => DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);

    /// <summary>Días de vigencia configurados hoy: una nota vence a esos días de su emisión, así que subirlos habilita las vencidas (RF-40).</summary>
    private Task<int> DiasVigenciaAsync(CancellationToken cancelacion) =>
        parametros.ObtenerEnteroPositivoAsync(CatalogoParametros.DiasVigenciaNotaCredito, cancelacion);

    public async Task<DatosNotaCreditoParaCaja?> BuscarParaCajaAsync(string codigo, CancellationToken cancelacion = default)
    {
        var buscado = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        if (buscado.Length == 0)
            return null;

        var nota = await contexto.NotasCredito.AsNoTracking().FirstOrDefaultAsync(n => n.Encf == buscado || n.Numero == buscado, cancelacion);
        return nota is null ? null : ParaCaja((await DatosAsync([nota], cancelacion))[0]);
    }

    public async Task<RespuestaReservaNotaCredito> ReservarAsync(string notaCreditoNumero, int cajaId, string ventaNumero, decimal monto,
        CancellationToken cancelacion = default)
    {
        if (monto <= 0)
            return new RespuestaReservaNotaCredito(false, "El monto a reservar debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(ventaNumero))
            return new RespuestaReservaNotaCredito(false, "Indique la factura para la que se reserva el saldo.");

        var numero = (notaCreditoNumero ?? string.Empty).Trim();
        var factura = ventaNumero.Trim();

        var minutos = await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.NotasCreditoMinutosReserva, cancelacion);
        var ahora = reloj.GetUtcNow();

        // La fila de la nota se bloquea mientras se calcula el disponible: dos cajas no pueden reservar el mismo saldo.
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        var nota = await contexto.NotasCredito
            .FromSql($"SELECT * FROM NotasCredito WITH (UPDLOCK, ROWLOCK) WHERE Numero = {numero}")
            .SingleOrDefaultAsync(cancelacion);
        if (nota is null)
            return new RespuestaReservaNotaCredito(false, "La nota de crédito no existe en el Central.");

        var diasVigencia = await DiasVigenciaAsync(cancelacion);
        if (nota.Estado(Hoy, diasVigencia) == EstadoNotaCreditoCentral.Vencida)
            return new RespuestaReservaNotaCredito(false, $"La nota de crédito {nota.Encf ?? nota.Numero} venció el {nota.VenceEn(diasVigencia):dd/MM/yyyy}.");

        var reservas = await contexto.ReservasNotaCredito.Where(r => r.NotaCreditoId == nota.Id && r.CerradaEn == null).ToListAsync(cancelacion);
        foreach (var vencida in reservas.Where(r => !r.EstaVigente(ahora)))
            vencida.Cerrar("Vencida", ahora);

        // Un reintento del cobro de la misma factura reemplaza su reserva anterior.
        foreach (var anterior in reservas.Where(r => r.CajaId == cajaId && r.VentaNumero == factura && r.CerradaEn is null))
            anterior.Cerrar("Reemplazada", ahora);

        var disponible = nota.Saldo - reservas.Where(r => r.CerradaEn is null && r.EstaVigente(ahora)).Sum(r => r.Monto);
        if (disponible <= 0)
            return new RespuestaReservaNotaCredito(false, $"La nota de crédito {nota.Encf ?? nota.Numero} no tiene saldo disponible.");

        var reservado = Math.Min(monto, disponible);
        var reserva = ReservaNotaCreditoCentral.Crear(nota.Id, cajaId, factura, reservado, ahora, TimeSpan.FromMinutes(minutos));
        contexto.ReservasNotaCredito.Add(reserva);
        await contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        var datos = (await DatosAsync([nota], cancelacion))[0];
        var mensaje = reservado < monto ? $"Solo hay {reservado:N2} {nota.Moneda} disponible en la nota de crédito." : null;
        return new RespuestaReservaNotaCredito(true, mensaje, reservado, reserva.VenceEn, ParaCaja(datos));
    }

    public async Task<bool> LiberarReservaAsync(string notaCreditoNumero, int cajaId, string ventaNumero, CancellationToken cancelacion = default)
    {
        var numero = (notaCreditoNumero ?? string.Empty).Trim();
        var factura = (ventaNumero ?? string.Empty).Trim();
        var reservas = await contexto.ReservasNotaCredito
            .Where(r => r.CajaId == cajaId && r.VentaNumero == factura && r.CerradaEn == null)
            .Join(contexto.NotasCredito.Where(n => n.Numero == numero), r => r.NotaCreditoId, n => n.Id, (r, _) => r)
            .ToListAsync(cancelacion);
        if (reservas.Count == 0)
            return false;

        foreach (var reserva in reservas)
            reserva.Cerrar("Liberada", reloj.GetUtcNow());
        await contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    public async Task<PaginaNotasCreditoCentral> ListarAsync(string? buscar, EstadoNotaCreditoCentral? estado, bool soloSobregiradas, int pagina, int tamano,
        CancellationToken cancelacion = default)
    {
        tamano = Math.Clamp(tamano, 1, IServicioNotasCreditoCentral.TamanoMaximoPagina);
        pagina = Math.Max(pagina, 0);
        // Vencida si se emitió antes de este día (su vencimiento, emisión más los días, ya pasó).
        var emitidaAntesDe = Hoy.AddDays(-await DiasVigenciaAsync(cancelacion));

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
            EstadoNotaCreditoCentral.Vencida => consulta.Where(n => n.Total - n.Consumido > 0 && n.FechaEmision < emitidaAntesDe),
            EstadoNotaCreditoCentral.Vigente => consulta.Where(n => n.Total - n.Consumido > 0 && n.FechaEmision >= emitidaAntesDe),
            _ => consulta,
        };

        if (soloSobregiradas)
            consulta = consulta.Where(n => n.Consumido > n.Total);

        var total = await consulta.CountAsync(cancelacion);
        var notas = await consulta.OrderByDescending(n => n.EmitidaEn).Skip(pagina * tamano).Take(tamano).ToListAsync(cancelacion);
        return new PaginaNotasCreditoCentral(await DatosAsync(notas, cancelacion), total);
    }

    public async Task<IReadOnlyList<DatosMovimientoNotaCredito>> ListarMovimientosAsync(int notaCreditoId, CancellationToken cancelacion = default)
    {
        var nota = await contexto.NotasCredito.AsNoTracking().SingleOrDefaultAsync(n => n.Id == notaCreditoId, cancelacion);
        var numero = nota?.Numero;
        var consumos = await contexto.ConsumosNotaCredito.AsNoTracking().Where(c => c.NotaCreditoNumero == numero).ToListAsync(cancelacion);
        var reservas = await contexto.ReservasNotaCredito.AsNoTracking().Where(r => r.NotaCreditoId == notaCreditoId).ToListAsync(cancelacion);
        var cajas = await CodigosCajasAsync(consumos.Select(c => c.CajaId).Concat(reservas.Select(r => r.CajaId)), cancelacion);
        var ahora = reloj.GetUtcNow();

        var movimientos = consumos
            .Select(c => new DatosMovimientoNotaCredito(c.Fecha, "Consumo", cajas.GetValueOrDefault(c.CajaId) ?? string.Empty, c.Monto, $"Venta {c.VentaNumero}"))
            .Concat(reservas.Select(r => new DatosMovimientoNotaCredito(r.CreadaEn, "Reserva", cajas.GetValueOrDefault(r.CajaId) ?? string.Empty, r.Monto,
                $"Venta {r.VentaNumero}: {r.Cierre ?? (r.EstaVigente(ahora) ? "Vigente" : "Vencida")}")))
            .ToList();

        return movimientos.OrderByDescending(m => m.Fecha).ToList();
    }

    private async Task<IReadOnlyList<DatosNotaCreditoCentral>> DatosAsync(IReadOnlyList<NotaCreditoCentral> notas, CancellationToken cancelacion)
    {
        if (notas.Count == 0)
            return [];

        var ahora = reloj.GetUtcNow();
        var hoy = Hoy;
        var diasVigencia = await DiasVigenciaAsync(cancelacion);
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
                Math.Max(0m, nota.Saldo - retenido), nota.VenceEn(diasVigencia), nota.Estado(hoy, diasVigencia), nota.Sobregirada, nota.EmitidaEn);
        }).ToList();
    }

    private static DatosNotaCreditoParaCaja ParaCaja(DatosNotaCreditoCentral nota) =>
        new(nota.Numero, nota.Encf, nota.SucursalCodigo, nota.CajaCodigo, nota.ClienteDocumento, nota.ClienteNombre, nota.Moneda, nota.Total, nota.Disponible,
            nota.VenceEn, nota.Estado);

    private async Task<Dictionary<int, string>> CodigosCajasAsync(IEnumerable<int> ids, CancellationToken cancelacion)
    {
        var buscar = ids.Distinct().ToList();
        return buscar.Count == 0
            ? []
            : await contexto.Cajas.AsNoTracking().Where(c => buscar.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Codigo.ToString("00"), cancelacion);
    }
}
