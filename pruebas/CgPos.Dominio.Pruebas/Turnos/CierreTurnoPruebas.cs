using CgPos.Dominio.Pagos;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Turnos;

namespace CgPos.Dominio.Pruebas.Turnos;

public class CierreTurnoPruebas
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 15, 18, 0, 0, TimeSpan.FromHours(-4));
    private static readonly int Cajera = Ids.Siguiente();

    /// <summary>Sucursal y caja del turno, con su número.</summary>
    private static OrigenDocumento Origen(long numero) => new("01", "Sucursal de prueba", "01", numero);
    private const string MonedaLocal = "DOP";

    private static readonly FormaPagoCuadre Efectivo = new(Ids.Siguiente(), "EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", 1);
    private static readonly FormaPagoCuadre Dolares = new(Ids.Siguiente(), "USD", "Dólares", TipoFormaPago.MonedaExtranjera, "USD", 2);
    private static readonly FormaPagoCuadre Tarjeta = new(Ids.Siguiente(), "TAR", "Tarjeta", TipoFormaPago.Tarjeta, "DOP", 3);

    private static Turno TurnoAbierto(decimal fondo = 2000m) =>
        Turno.Abrir(Ids.Siguiente(), Ids.Siguiente(), Origen(7), DateOnly.FromDateTime(Ahora.DateTime), Cajera, "Cajera", fondo, Ahora.AddHours(-8));

    [Fact]
    public void Esperado_del_efectivo_descuenta_devuelta_y_retiros_y_deja_el_fondo_fuera_del_cuadre()
    {
        // Venta 1: RD$1,000 en efectivo por 850 (devuelta 150). Venta 2: US$20 a 60 por 1,150 (devuelta 50). Venta 3: tarjeta por 500.
        var pagos = new[]
        {
            new PagoCuadre(Efectivo.FormaPagoId, 1000m, 1000m),
            new PagoCuadre(Dolares.FormaPagoId, 20m, 1200m),
            new PagoCuadre(Tarjeta.FormaPagoId, 500m, 500m),
        };

        var esperados = ReglasCuadre.CalcularEsperados([Efectivo, Dolares, Tarjeta], pagos, devuelta: 200m, retiros: 300m, fondoInicial: 2000m, fondoEnCuadre: false,
            MonedaLocal);

        Assert.Equal(500m, esperados.Single(e => e.FormaPagoId == Efectivo.FormaPagoId).Esperado);
        Assert.Equal(20m, esperados.Single(e => e.FormaPagoId == Dolares.FormaPagoId).Esperado);
        Assert.Equal(500m, esperados.Single(e => e.FormaPagoId == Tarjeta.FormaPagoId).Esperado);
        Assert.Equal(2500m, ReglasCuadre.EfectivoLocalEnGaveta(esperados, 2000m, fondoEnCuadre: false, MonedaLocal));

        var conFondo = ReglasCuadre.CalcularEsperados([Efectivo], pagos, 200m, 300m, 2000m, fondoEnCuadre: true, MonedaLocal);
        Assert.Equal(2500m, conFondo.Single().Esperado);
        Assert.Equal(2500m, ReglasCuadre.EfectivoLocalEnGaveta(conFondo, 2000m, fondoEnCuadre: true, MonedaLocal));
    }

    [Fact]
    public void La_moneda_local_es_la_configurada_y_no_una_fija()
    {
        // Con dólares como moneda local, la devuelta y los retiros salen del efectivo en dólares y los pesos se cuadran aparte.
        var efectivoDolares = Dolares with { Tipo = TipoFormaPago.Efectivo };
        var pagos = new[] { new PagoCuadre(Efectivo.FormaPagoId, 5000m, 100m), new PagoCuadre(efectivoDolares.FormaPagoId, 300m, 300m) };

        var esperados = ReglasCuadre.CalcularEsperados([Efectivo, efectivoDolares], pagos, devuelta: 20m, retiros: 50m, fondoInicial: 100m, fondoEnCuadre: false, "USD");

        Assert.Equal(5000m, esperados.Single(e => e.FormaPagoId == Efectivo.FormaPagoId).Esperado);
        Assert.Equal(230m, esperados.Single(e => e.FormaPagoId == efectivoDolares.FormaPagoId).Esperado);
        Assert.Equal(330m, ReglasCuadre.EfectivoLocalEnGaveta(esperados, 100m, fondoEnCuadre: false, "USD"));
    }

    [Fact]
    public void El_cierre_de_la_caja_informa_lo_esperado_y_no_declara_nada()
    {
        var turno = TurnoAbierto();
        var esperados = new[]
        {
            new EsperadoFormaPago(Efectivo.FormaPagoId, "EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", 1, 3450m, 4),
            new EsperadoFormaPago(Tarjeta.FormaPagoId, "TAR", "Tarjeta", TipoFormaPago.Tarjeta, "DOP", 3, 1200m, 2),
        };

        // La cajera no cuenta ni declara: entrega el dinero y el supervisor lo cuadra en el Central.
        var cierre = CierreTurno.Registrar(turno, 1, fondoEnCuadre: false, 6, 4650m, 300m, esperados, MonedaLocal, Cajera, "Cajera", Ahora);

        Assert.Equal(3450m, cierre.FormasPago.Single(f => f.FormaPagoId == Efectivo.FormaPagoId).Esperado);
        Assert.Equal(1200m, cierre.FormasPago.Single(f => f.FormaPagoId == Tarjeta.FormaPagoId).Esperado);
        Assert.Equal(4650m, cierre.TotalEsperado);
        Assert.False(turno.EstaAbierto);
    }

    [Fact]
    public void Relevo_cambia_el_usuario_del_turno_y_el_cierre_lo_deja_cerrado_para_siempre()
    {
        var turno = TurnoAbierto();
        var relevista = Ids.Siguiente();

        var relevo = turno.Relevar(1, relevista, "Relevista", Ids.Siguiente(), "Supervisor", Ahora);

        Assert.Equal(TipoMovimientoCaja.Relevo, relevo.Tipo);
        Assert.Equal(Cajera, relevo.UsuarioAnteriorId);
        Assert.Equal(relevista, turno.UsuarioActualId);
        Assert.Throws<InvalidOperationException>(() => turno.Relevar(2, relevista, "Relevista", null, null, Ahora));

        // Cerrar es definitivo: el turno queda cerrado y el cierre no se puede deshacer desde la caja.
        var cierre = CierreTurno.Registrar(turno, 1, false, 0, 0m, 0m, [], MonedaLocal, relevista, "Relevista", Ahora);

        Assert.Equal(relevista, cierre.UsuarioId);
        Assert.False(turno.EstaAbierto);
        Assert.Equal(Ahora, turno.CerradoEn);
    }
}
