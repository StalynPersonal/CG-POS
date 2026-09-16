using CgPos.Dominio.Fidelidad;

namespace CgPos.Dominio.Pruebas.Fidelidad;

public class SaldoPuntosCentralPruebas
{
    private static readonly DateTimeOffset Inicio = new(2026, 1, 10, 9, 0, 0, TimeSpan.FromHours(-4));
    private static readonly DateOnly Hoy = new(2026, 9, 15);
    private static readonly Guid Miembro = Guid.CreateVersion7();
    private static readonly Guid Caja = Guid.CreateVersion7();
    private static readonly Guid Sucursal = Guid.CreateVersion7();
    private const string Cedula = "00113918205";

    private static MovimientoPuntosCentral Acumula(int puntos, DateOnly? venceEn, int dia) =>
        MovimientoPuntosCentral.DesdeCaja(Guid.CreateVersion7(), Miembro, Cedula, TipoMovimientoPuntos.Acumulacion, puntos, Guid.CreateVersion7(), null,
            "01-01-00000001", Caja, Sucursal, Inicio.AddDays(dia), venceEn, Inicio.AddDays(dia));

    private static MovimientoPuntosCentral Canjea(int puntos, int dia) =>
        MovimientoPuntosCentral.DesdeCaja(Guid.CreateVersion7(), Miembro, Cedula, TipoMovimientoPuntos.Canje, -puntos, Guid.CreateVersion7(), null,
            "01-01-00000002", Caja, Sucursal, Inicio.AddDays(dia), null, Inicio.AddDays(dia));

    [Fact]
    public void El_canje_gasta_primero_los_puntos_que_vencen_antes()
    {
        var pronto = Hoy.AddDays(10);
        var tarde = Hoy.AddMonths(6);

        var saldo = SaldoPuntosCalculo.Calcular([Acumula(100, tarde, 1), Acumula(60, pronto, 2), Canjea(40, 3)], Hoy);

        // Se gastan 40 de los que vencen pronto, no de los que duran más.
        Assert.Equal(new SaldoPuntos(120, 20, pronto, 0), saldo);
    }

    [Fact]
    public void Los_puntos_vencidos_salen_del_saldo_y_quedan_a_la_vista()
    {
        var vencido = Hoy.AddDays(-1);
        var vigente = Hoy.AddMonths(3);

        var saldo = SaldoPuntosCalculo.Calcular([Acumula(70, vencido, 1), Acumula(30, vigente, 2)], Hoy);

        Assert.Equal(new SaldoPuntos(30, 30, vigente, 70), saldo);
    }

    [Fact]
    public void Los_puntos_sin_vencimiento_no_caducan_y_no_dan_proximo_vencimiento()
    {
        var saldo = SaldoPuntosCalculo.Calcular([Acumula(50, null, 1), Canjea(20, 2)], Hoy);

        Assert.Equal(new SaldoPuntos(30, 0, null, 0), saldo);
    }

    [Fact]
    public void Un_canje_de_una_caja_sin_conexion_puede_dejar_el_saldo_en_negativo_hasta_que_lleguen_los_demas_mensajes()
    {
        var saldo = SaldoPuntosCalculo.Calcular([Acumula(20, null, 1), Canjea(50, 2)], Hoy);

        Assert.Equal(-30, saldo.Puntos);
    }

    [Fact]
    public void El_ajuste_del_central_exige_motivo_y_responsable_y_no_puede_ser_cero()
    {
        var ajuste = MovimientoPuntosCentral.Ajuste(Miembro, Cedula, -25, "Gerente Central", "Corrección de acumulación duplicada", null, Inicio);

        Assert.Equal((TipoMovimientoPuntos.Ajuste, OrigenMovimientoPuntos.Central, -25), (ajuste.Tipo, ajuste.Origen, ajuste.Puntos));
        Assert.Equal("Gerente Central", ajuste.Usuario);
        Assert.Throws<ArgumentOutOfRangeException>(() => MovimientoPuntosCentral.Ajuste(Miembro, Cedula, 0, "Gerente", "Nada", null, Inicio));
        Assert.Throws<ArgumentException>(() => MovimientoPuntosCentral.Ajuste(Miembro, Cedula, 10, "Gerente", "   ", null, Inicio));
    }

    [Fact]
    public void Un_movimiento_de_caja_con_el_signo_cambiado_se_rechaza()
    {
        Assert.Throws<ArgumentException>(() => MovimientoPuntosCentral.DesdeCaja(Guid.CreateVersion7(), Miembro, Cedula, TipoMovimientoPuntos.Canje, 30,
            Guid.CreateVersion7(), null, "01-01-00000003", Caja, Sucursal, Inicio, null, Inicio));
    }

    [Fact]
    public void El_saldo_publicado_avisa_cuando_cambia_para_no_republicar_el_maestro_sin_motivo()
    {
        var saldo = SaldoPuntosCentral.Crear(Miembro, Cedula);
        var calculado = new SaldoPuntos(40, 10, Hoy.AddMonths(1), 0);

        Assert.True(saldo.Aplicar(calculado, Inicio));
        Assert.False(saldo.Aplicar(calculado, Inicio.AddHours(1)));
        Assert.True(saldo.Aplicar(calculado with { Puntos = 35 }, Inicio.AddHours(2)));
        Assert.Equal(35, saldo.Puntos);
    }
}
