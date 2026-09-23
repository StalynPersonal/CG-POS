using CgPos.Pos.Aplicacion.Perifericos;

namespace CgPos.Pos.Infraestructura.Perifericos.Terminales;

/// <summary>
/// Caja sin terminal conectado (<c>Perifericos:Terminal:Tipo = Ninguno</c>). Se cobra con un equipo aparte —un verifone
/// inalámbrico, por ejemplo— y el cajero digita el número de aprobación que imprime su volante. La caja no le habla a
/// nadie: no espera respuesta, no se queda colgada y no aprueba nada por su cuenta, que es lo que hace el simulado.
/// </summary>
internal sealed class TerminalPagoAusente : ITerminalPago
{
    private const string SinTerminal = "Esta caja no tiene terminal conectado: cobre en el equipo y digite el número de aprobación del volante.";

    public bool Integrado => false;

    /// <summary>Nadie lee la tarjeta: el descuento por BIN, si aplica, se resuelve con los dígitos que digite el cajero.</summary>
    public bool ConsultaTarjeta => false;

    public Task<ResultadoConsultaTarjeta> ConsultarTarjetaAsync(CancellationToken cancelacion = default) =>
        Task.FromResult(new ResultadoConsultaTarjeta(false, false, null, null, SinTerminal));

    public Task<ResultadoTerminal> CobrarAsync(decimal monto, decimal impuesto, string referenciaVenta, CancellationToken cancelacion = default) =>
        Task.FromResult(new ResultadoTerminal(false, false, null, null, null, SinTerminal));

    /// <summary>La anulación también se hace en el equipo: la caja solo quita el pago de la venta.</summary>
    public Task<ResultadoTerminal> AnularAsync(string aprobacion, string? referenciaTerminal, decimal monto, CancellationToken cancelacion = default) =>
        Task.FromResult(new ResultadoTerminal(false, false, null, null, null, "Anule el cobro en el equipo donde lo hizo; aquí solo se quita el pago."));

    /// <summary>El lote lo cierra el equipo por su cuenta: en el cuadre se compara contra su comprobante impreso.</summary>
    public Task<ResultadoLoteTerminal> CerrarLoteAsync(CancellationToken cancelacion = default) =>
        Task.FromResult(new ResultadoLoteTerminal(true, "Esta caja no tiene terminal conectado: cierre el lote en el equipo y compare con su comprobante."));
}
