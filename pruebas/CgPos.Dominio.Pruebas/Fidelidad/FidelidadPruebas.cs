using CgPos.Dominio.Fidelidad;

namespace CgPos.Dominio.Pruebas.Fidelidad;

public class FidelidadPruebas
{
    // Martes 15 de septiembre de 2026, 10:00 hora local.
    private static readonly DateTimeOffset Martes = new(2026, 9, 15, 10, 0, 0, TimeSpan.FromHours(-4));
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(Martes.DateTime);
    private static readonly Guid Ferreteria = Guid.CreateVersion7();
    private static readonly Guid Cincel = Guid.CreateVersion7();
    private const string Cedula = "00113918205";

    private static ReglaAcumulacion General() =>
        ReglaAcumulacion.Crear(4, "General", TipoReglaAcumulacion.Monto, 100m, 1m, null, null, null, null);

    [Fact]
    public void Cada_linea_toma_la_regla_mas_favorable_y_el_nivel_multiplica()
    {
        var reglas = new[]
        {
            General(),
            ReglaAcumulacion.Crear(6, "Ferretería doble", TipoReglaAcumulacion.Departamento, 100m, 2m, Ferreteria, null, null, null),
            ReglaAcumulacion.Crear(7, "Martes triple", TipoReglaAcumulacion.DiaSemana, 100m, 3m, null, DayOfWeek.Tuesday, null, null),
        };
        var lineas = new[]
        {
            new LineaPuntuable(Cincel, Ferreteria, null, 850m),
            new LineaPuntuable(Guid.CreateVersion7(), Guid.CreateVersion7(), null, 200m),
        };

        // Martes gana la regla del día en ambas líneas: (8.5 + 2) × 3 = 31.5 → × 1.5 = 47.25 → 47.
        Assert.Equal(47, ReglasFidelidad.CalcularPuntos(lineas, reglas, 1.5m, 1m, Martes));

        // Otro día: ferretería 17 + general 2 = 19; si la mitad se pagó con puntos, 9.
        var miercoles = Martes.AddDays(1);
        Assert.Equal(19, ReglasFidelidad.CalcularPuntos(lineas, reglas, 1m, 1m, miercoles));
        Assert.Equal(9, ReglasFidelidad.CalcularPuntos(lineas, reglas, 1m, 0.5m, miercoles));
        Assert.Equal(0, ReglasFidelidad.CalcularPuntos(lineas, [], 1m, 1m, miercoles));
    }

    [Fact]
    public void Reglas_por_promocion_y_fuera_de_vigencia()
    {
        var promocion = Guid.CreateVersion7();
        var reglas = new[]
        {
            ReglaAcumulacion.Crear(8, "Oferta con puntos", TipoReglaAcumulacion.Promocion, 50m, 1m, promocion, null, null, null),
            ReglaAcumulacion.Crear(9, "Vencida", TipoReglaAcumulacion.Monto, 1m, 100m, null, null, Martes.AddDays(-10), Martes.AddDays(-1)),
        };

        Assert.Equal(4, ReglasFidelidad.CalcularPuntos([new LineaPuntuable(Cincel, Ferreteria, promocion, 200m)], reglas, 1m, 1m, Martes));
        Assert.Equal(0, ReglasFidelidad.CalcularPuntos([new LineaPuntuable(Cincel, Ferreteria, null, 200m)], reglas, 1m, 1m, Martes));
        Assert.Throws<ArgumentException>(() => ReglaAcumulacion.Crear(10, "Sin departamento", TipoReglaAcumulacion.Departamento, 100m, 1m, null, null, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReglaAcumulacion.Crear(11, "Base cero", TipoReglaAcumulacion.Monto, 0m, 1m, null, null, null, null));
    }

    [Fact]
    public void Saldo_disponible_suma_lo_local_posterior_a_la_sincronizacion_y_descarta_lo_vencido()
    {
        var sincronizadoEn = Martes.AddDays(-5);
        var miembro = MiembroFidelidad.DesdeCentral(Cedula, "Cliente Ejemplo", Martes.AddYears(-1), Guid.CreateVersion7());
        miembro.SincronizarSaldo(1000, sincronizadoEn, puntosPorVencer: 200, proximoVencimiento: Hoy.AddDays(-1));

        var cajaId = Guid.CreateVersion7();
        var movimientos = new[]
        {
            // Anterior a la sincronización: el Central ya lo contó.
            MovimientoPuntos.Acumulacion(miembro, 50, Guid.CreateVersion7(), "T-1", cajaId, sincronizadoEn.AddDays(-1), null),
            MovimientoPuntos.Acumulacion(miembro, 30, Guid.CreateVersion7(), "T-2", cajaId, Martes.AddDays(-2), Hoy.AddMonths(12)),
            MovimientoPuntos.Acumulacion(miembro, 40, Guid.CreateVersion7(), "T-3", cajaId, Martes.AddDays(-2), Hoy.AddDays(-1)),
            MovimientoPuntos.Canje(miembro, 100, Guid.CreateVersion7(), "T-4", cajaId, Martes.AddDays(-1)),
        };

        // 1000 − 200 vencidos + 30 − 100 = 730 (los 40 locales vencieron y los 50 ya estaban en el saldo).
        Assert.Equal(730, miembro.SaldoDisponible(movimientos, Hoy));
        Assert.Throws<ArgumentException>(() => MiembroFidelidad.Inscribir("123", "Sin cédula", null, null, Martes));
        Assert.Throws<ArgumentException>(() => MiembroFidelidad.Inscribir(Cedula, "Correo malo", null, "sin-arroba", Martes));
    }

    [Fact]
    public void Canje_redondea_hacia_arriba_y_la_devolucion_reversa_en_proporcion()
    {
        Assert.Equal(101, ReglasFidelidad.PuntosParaMonto(100.50m, 1m));
        Assert.Equal(20, ReglasFidelidad.PuntosParaMonto(100m, 5m));

        // Factura de 1,000 con 30 puntos: devolver 400 reversa 12; devolver el resto completa lo que falta.
        Assert.Equal(12, ReglasFidelidad.PuntosAReversar(30, 0, 1000m, 400m, completaFactura: false));
        Assert.Equal(18, ReglasFidelidad.PuntosAReversar(30, 12, 1000m, 600m, completaFactura: true));
        Assert.Equal(0, ReglasFidelidad.PuntosAReversar(30, 30, 1000m, 100m, completaFactura: false));
    }
}
