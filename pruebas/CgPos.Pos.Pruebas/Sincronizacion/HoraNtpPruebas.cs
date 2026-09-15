using CgPos.Pos.Infraestructura.Sincronizacion;
using Microsoft.Extensions.Configuration;

namespace CgPos.Pos.Pruebas.Sincronizacion;

/// <summary>Cálculo del desfase del reloj con SNTP y opciones del mantenimiento, sin red ni base de datos.</summary>
public class HoraNtpPruebas
{
    [Fact]
    public void Marca_ntp_se_lee_con_fraccion_y_en_la_era_posterior_a_2036()
    {
        var paquete = new byte[48];
        var fecha = new DateTimeOffset(2026, 9, 15, 14, 30, 5, 250, TimeSpan.Zero);
        HoraNtp.EscribirMarca(paquete, 40, fecha);
        Assert.InRange((HoraNtp.LeerMarca(paquete, 40) - fecha).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));

        var despuesDe2036 = new DateTimeOffset(2040, 1, 1, 0, 0, 0, TimeSpan.Zero);
        HoraNtp.EscribirMarca(paquete, 32, despuesDe2036);
        Assert.InRange((HoraNtp.LeerMarca(paquete, 32) - despuesDe2036).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));

        Assert.Throws<FormatException>(() => HoraNtp.LeerMarca(new byte[20], 32));
    }

    [Fact]
    public void Desfase_descuenta_la_demora_de_la_red()
    {
        var caja = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

        // El servidor va 2 s adelantado y la red tarda 100 ms en cada sentido.
        var desfase = HoraNtp.CalcularDesfase(caja, caja.AddSeconds(2.1), caja.AddSeconds(2.2), caja.AddSeconds(0.3));

        Assert.Equal(TimeSpan.FromSeconds(2), desfase);
    }

    [Fact]
    public void Respaldo_y_hora_se_desactivan_sin_configuracion()
    {
        OpcionesMantenimiento Leer(Dictionary<string, string?> valores) => OpcionesMantenimiento.Leer(new ConfigurationBuilder().AddInMemoryCollection(valores).Build());

        var vacias = Leer([]);
        Assert.Null(vacias.HoraRespaldo);
        Assert.Null(vacias.ServidorHora);
        Assert.Null(vacias.CarpetaRespaldo);

        var configuradas = Leer(new() { ["Respaldo:Hora"] = "22", ["Reloj:ServidorNtp"] = "time.windows.com", ["Respaldo:Carpeta"] = @"D:\Respaldos" });
        Assert.Equal(22, configuradas.HoraRespaldo);
        Assert.Equal("time.windows.com", configuradas.ServidorHora);
        Assert.Null(Leer(new() { ["Respaldo:Hora"] = "25" }).HoraRespaldo);
    }
}
