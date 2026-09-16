using CgPos.Dominio.Promociones;

namespace CgPos.Contratos.Catalogo;

/// <summary>Construcción de entidades del dominio a partir de registros de carga, con las mismas reglas que aplica la caja.</summary>
public static class ConversionMaestros
{
    /// <exception cref="ArgumentException">Si la promoción no cumple las reglas del dominio.</exception>
    public static Promocion ConstruirPromocion(PromocionCarga dato)
    {
        ArgumentNullException.ThrowIfNull(dato);
        var promocion = Promocion.Crear(dato.Codigo, dato.Nombre, dato.Tipo, dato.Valor, dato.VigenteDesde, dato.VigenteHasta, dato.Id);
        promocion.ConfigurarCantidades(dato.CantidadLleva, dato.CantidadPaga, dato.CantidadMinima, dato.LimitePorCliente);
        promocion.Programar(dato.Dias, dato.HoraDesde, dato.HoraHasta);
        promocion.AsignarAlcance(dato.Articulos, dato.Familias, dato.Sucursales);
        promocion.ConfigurarFidelidad(dato.SoloFidelidad);
        if (!dato.Activa)
            promocion.Desactivar();
        return promocion;
    }
}
