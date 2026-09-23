using CgPos.Dominio.Pagos;
using CgPos.Dominio.Turnos;
using CgPos.Dominio.Reportes;

namespace CgPos.Dominio.Pruebas.Turnos;

public class CierreSucursalPruebas
{
    private static readonly DateOnly Dia = new(2026, 6, 18);
    private static readonly DateTimeOffset Ahora = new(2026, 6, 18, 20, 0, 0, TimeSpan.FromHours(-4));
    private static readonly int Sucursal = Ids.Siguiente();

    private static CierreTurnoCentral CierreCaja(long turno, decimal efectivoEsperado, decimal efectivoDeclarado, decimal dolares = 0m, decimal tarjeta = 0m)
    {
        var cierre = Ids.Asignar(CierreTurnoCentral.Registrar(turno, 1, Sucursal, Ids.Siguiente(), Dia, "Cajero", "DOP", 0m, 5, 0m, 0m,
            efectivoEsperado + tarjeta, Ahora.AddHours(-10), Ahora, Ahora));
        var formas = new List<(TipoFormaPago, string, string, int, decimal)>
        {
            (TipoFormaPago.Efectivo, "Efectivo", "DOP", 5, efectivoEsperado),
        };
        if (dolares > 0)
            formas.Add((TipoFormaPago.MonedaExtranjera, "Dólares", "USD", 1, dolares));
        if (tarjeta > 0)
            formas.Add((TipoFormaPago.Tarjeta, "Tarjeta", "DOP", 2, tarjeta));
        cierre.ReemplazarFormasPago(formas);
        Ids.AsignarHijos(cierre.FormasPago);

        // El supervisor cuadra: es lo que le da al cierre su declarado y su diferencia.
        var declarados = cierre.FormasPago
            .Select(f => new DeclaradoFormaPago(f.Id, f.Tipo == TipoFormaPago.Efectivo && f.Moneda == "DOP" ? efectivoDeclarado : f.Esperado))
            .ToList();
        cierre.Cuadrar(declarados, [], "Supervisor", Ahora);
        return cierre;
    }

    [Fact]
    public void Suma_los_cierres_de_caja_y_deposita_solo_el_efectivo_por_moneda()
    {
        var cierres = new[] { CierreCaja(1, 5000m, 4950m, dolares: 100m, tarjeta: 3000m), CierreCaja(2, 1000m, 1000m) };

        var cierre = CierreSucursal.Consolidar(Sucursal, Dia, cierres,
            [new DepositoSolicitado("dop", "BPD", "Banco Popular", "BOL-1", 5950m, Dia.AddDays(1)), new DepositoSolicitado("USD", "BPD", "Banco Popular", "BOL-2", 90m, Dia)],
            null, "Gerente", Ahora);

        Assert.Equal((2, -50m), (cierre.CantidadCierres, cierre.Diferencia));
        Assert.Equal(5950m, cierre.EfectivoADepositar["DOP"]);
        Assert.Equal(100m, cierre.EfectivoADepositar["USD"]);
        Assert.False(cierre.EfectivoADepositar.ContainsKey("TAR"));
        Assert.Equal((0m, -10m), (cierre.DiferenciaDeposito["DOP"], cierre.DiferenciaDeposito["USD"]));
        Assert.Equal(6000m, cierre.FormasPago.Where(f => f.Tipo == TipoFormaPago.Efectivo).Sum(f => f.Esperado));
    }

    [Fact]
    public void No_se_consolida_sin_cierres_ni_con_depositos_invalidos()
    {
        var cierres = new[] { CierreCaja(1, 1000m, 1000m) };

        Assert.Throws<ArgumentException>(() => CierreSucursal.Consolidar(Sucursal, Dia, [], [], null, "Gerente", Ahora));
        Assert.Throws<ArgumentException>(() => CierreSucursal.Consolidar(Sucursal, Dia.AddDays(1), cierres, [], null, "Gerente", Ahora));
        Assert.Throws<ArgumentException>(() => CierreSucursal.Consolidar(Sucursal, Dia, cierres,
            [new DepositoSolicitado("USD", "BPD", "Banco Popular", "BOL-1", 10m, Dia)], null, "Gerente", Ahora));
        Assert.Throws<ArgumentException>(() => CierreSucursal.Consolidar(Sucursal, Dia, cierres,
            [new DepositoSolicitado("DOP", "BPD", "Banco Popular", " ", 10m, Dia)], null, "Gerente", Ahora));
        Assert.Throws<ArgumentException>(() => CierreSucursal.Consolidar(Sucursal, Dia, cierres,
            [new DepositoSolicitado("DOP", "BPD", "Banco Popular", "BOL-1", 10m, Dia), new DepositoSolicitado("DOP", "BPD", "Banco Popular", "BOL-1", 5m, Dia)],
            null, "Gerente", Ahora));
    }
}
