using CgPos.Dominio.Pagos;
using CgPos.Dominio.Turnos;

namespace CgPos.Dominio.Pruebas.Turnos;

public class CierreTurnoPruebas
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 15, 18, 0, 0, TimeSpan.FromHours(-4));
    private static readonly Guid Cajera = Guid.CreateVersion7();
    private const string MonedaLocal = "DOP";

    private static readonly FormaPagoCuadre Efectivo = new(Guid.CreateVersion7(), "EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", 1);
    private static readonly FormaPagoCuadre Dolares = new(Guid.CreateVersion7(), "USD", "Dólares", TipoFormaPago.MonedaExtranjera, "USD", 2);
    private static readonly FormaPagoCuadre Tarjeta = new(Guid.CreateVersion7(), "TAR", "Tarjeta", TipoFormaPago.Tarjeta, "DOP", 3);

    private static Turno TurnoAbierto(decimal fondo = 2000m) =>
        Turno.Abrir(Guid.CreateVersion7(), Guid.CreateVersion7(), 7, DateOnly.FromDateTime(Ahora.DateTime), Cajera, "Cajera", fondo, Ahora.AddHours(-8));

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
    public void Cierre_compara_declarado_contra_esperado_y_el_conteo_define_el_efectivo()
    {
        var turno = TurnoAbierto();
        var esperados = new[]
        {
            new EsperadoFormaPago(Efectivo.FormaPagoId, "EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", 1, 3450m, 4),
            new EsperadoFormaPago(Tarjeta.FormaPagoId, "TAR", "Tarjeta", TipoFormaPago.Tarjeta, "DOP", 3, 1200m, 2),
        };
        var conteo = new[]
        {
            new ConteoDenominacion(Guid.CreateVersion7(), "DOP", 1000m, TipoDenominacion.Billete, 3),
            new ConteoDenominacion(Guid.CreateVersion7(), "DOP", 200m, TipoDenominacion.Billete, 2),
            new ConteoDenominacion(Guid.CreateVersion7(), "DOP", 25m, TipoDenominacion.Moneda, 0),
        };

        var cierre = CierreTurno.Registrar(turno, 1, ciego: true, fondoEnCuadre: false, 6, 4650m, 300m, esperados,
            [new DeclaradoFormaPago(Tarjeta.FormaPagoId, 1250m)], conteo, MonedaLocal, Cajera, "Cajera", Ahora);

        var efectivo = cierre.FormasPago.Single(f => f.FormaPagoId == Efectivo.FormaPagoId);
        Assert.Equal(3400m, efectivo.Declarado);
        Assert.Equal(-50m, efectivo.Diferencia);
        Assert.Equal(50m, cierre.FormasPago.Single(f => f.FormaPagoId == Tarjeta.FormaPagoId).Diferencia);
        Assert.Equal(4650m, cierre.TotalEsperado);
        Assert.Equal(4650m, cierre.TotalDeclarado);
        Assert.Equal(0m, cierre.Diferencia);
        Assert.Equal(2, cierre.Denominaciones.Count); // las cantidades en cero no se guardan
        Assert.False(turno.EstaAbierto);
    }

    [Fact]
    public void Declaracion_que_no_coincide_con_el_conteo_o_invalida_se_rechaza_sin_cerrar_el_turno()
    {
        var turno = TurnoAbierto();
        var esperados = new[] { new EsperadoFormaPago(Efectivo.FormaPagoId, "EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", 1, 1000m, 1) };
        var conteo = new[] { new ConteoDenominacion(Guid.CreateVersion7(), "DOP", 500m, TipoDenominacion.Billete, 2) };

        CodigoErrorCierre Rechazo(DeclaradoFormaPago[] declarados, ConteoDenominacion[] contado) =>
            Assert.Throws<ReglaCierreExcepcion>(() => CierreTurno.Registrar(turno, 1, true, false, 1, 1000m, 0m, esperados, declarados, contado, MonedaLocal, Cajera, "Cajera", Ahora)).Codigo;

        Assert.Equal(CodigoErrorCierre.ConteoNoCoincide, Rechazo([new DeclaradoFormaPago(Efectivo.FormaPagoId, 900m)], conteo));
        Assert.Equal(CodigoErrorCierre.MontoInvalido, Rechazo([new DeclaradoFormaPago(Efectivo.FormaPagoId, -1m)], []));
        Assert.Equal(CodigoErrorCierre.FormaPagoDesconocida, Rechazo([new DeclaradoFormaPago(Guid.CreateVersion7(), 10m)], []));
        Assert.True(turno.EstaAbierto);
    }

    [Fact]
    public void Relevo_cambia_el_usuario_del_turno_y_la_reapertura_lo_vuelve_a_abrir()
    {
        var turno = TurnoAbierto();
        var relevista = Guid.CreateVersion7();

        var relevo = turno.Relevar(1, relevista, "Relevista", Guid.CreateVersion7(), "Supervisor", Ahora);

        Assert.Equal(TipoMovimientoCaja.Relevo, relevo.Tipo);
        Assert.Equal(Cajera, relevo.UsuarioAnteriorId);
        Assert.Equal(relevista, turno.UsuarioActualId);
        Assert.Throws<InvalidOperationException>(() => turno.Relevar(2, relevista, "Relevista", null, null, Ahora));

        var cierre = CierreTurno.Registrar(turno, 1, true, false, 0, 0m, 0m, [], [], [], MonedaLocal, relevista, "Relevista", Ahora);
        Assert.Equal(CodigoErrorCierre.MotivoRequerido, Assert.Throws<ReglaCierreExcepcion>(() => cierre.Reabrir(Guid.CreateVersion7(), "Gerente", " ", Ahora)).Codigo);

        cierre.Reabrir(Guid.CreateVersion7(), "Gerente", "Billete de 1,000 mal contado", Ahora);
        turno.Reabrir();

        Assert.Equal(EstadoCierre.Reabierto, cierre.Estado);
        Assert.True(turno.EstaAbierto);
        Assert.Null(turno.CerradoEn);
        Assert.Equal(CodigoErrorCierre.YaReabierto, Assert.Throws<ReglaCierreExcepcion>(() => cierre.Reabrir(Guid.CreateVersion7(), "Gerente", "Otra vez", Ahora)).Codigo);
    }
}
