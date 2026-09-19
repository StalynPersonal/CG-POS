using System.Globalization;
using CgPos.Central.Aplicacion.Catalogo;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Infraestructura.Persistencia.Configuraciones;
using CgPos.Contratos.Central;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Promociones;
using Microsoft.EntityFrameworkCore;
using CgPos.Dominio.Comun;

namespace CgPos.Central.Infraestructura.Catalogo;

internal sealed class ServicioChequeadorPrecios(ContextoDatosCentral contexto, IParametrosCentral parametros, TimeProvider reloj) : IServicioChequeadorPrecios
{
    public Task<bool> HabilitadoAsync(CancellationToken cancelacion = default) =>
        parametros.ObtenerBooleanoOpcionalAsync(ClavesParametrosCentral.ChequeadorHabilitado, cancelacion);

    public async Task<IReadOnlyList<DatosSucursalChequeador>> ListarSucursalesAsync(CancellationToken cancelacion = default) =>
        await contexto.Sucursales.AsNoTracking()
            .Where(s => s.Activa)
            .OrderBy(s => s.Codigo)
            .Select(s => new DatosSucursalChequeador(s.Id, s.Nombre))
            .ToListAsync(cancelacion);

    public async Task<DatosPrecioChequeador?> ConsultarAsync(string codigo, int? sucursalId, CancellationToken cancelacion = default)
    {
        var buscado = (codigo ?? string.Empty).Trim();
        if (buscado.Length == 0 || !await HabilitadoAsync(cancelacion))
            return null;

        // Se busca por código interno, de barras, de proveedor o referencia: el cliente escanea lo que trae el empaque.
        // Los precios son propiedades sombra del artículo, así que se proyectan en la misma consulta.
        var encontrado = await contexto.Articulos.AsNoTracking()
            .Where(a => a.Activo && a.VentaEnPos
                && (a.Codigo == buscado || a.Referencia == buscado || a.Codigos.Any(c => c.Codigo == buscado)))
            .Select(a => new
            {
                Articulo = a,
                Precio = EF.Property<decimal>(a, ArticuloConfiguracion.PrecioDetalle),
                PrecioMayor = EF.Property<decimal?>(a, ArticuloConfiguracion.PrecioMayor),
            })
            .FirstOrDefaultAsync(cancelacion);
        if (encontrado is null)
            return null;

        var articulo = encontrado.Articulo;
        var precio = encontrado.Precio;
        var precioMayor = encontrado.PrecioMayor;
        var unidad = await contexto.UnidadesMedida.AsNoTracking()
            .Where(u => u.Id == articulo.UnidadMedidaId)
            .Select(u => u.Nombre)
            .FirstOrDefaultAsync(cancelacion) ?? string.Empty;

        return new DatosPrecioChequeador(
            articulo.Codigo,
            articulo.Descripcion,
            unidad,
            precio,
            articulo.AplicaPrecioMayor ? precioMayor : null,
            articulo.AplicaPrecioMayor ? articulo.CantidadMinimaMayor : null,
            articulo.RutaImagen,
            await OfertasAsync(articulo, sucursalId, precio, cancelacion));
    }

    /// <summary>Ofertas vigentes ahora que alcanzan al artículo; las de fidelidad no se anuncian porque no son para todo el mundo.</summary>
    private async Task<IReadOnlyList<DatosOfertaChequeador>> OfertasAsync(Articulo articulo, int? sucursalId, decimal precio, CancellationToken cancelacion)
    {
        var ahora = reloj.Ahora();
        var candidatas = await contexto.Promociones.AsNoTracking()
            .Where(p => p.Activa && !p.SoloFidelidad && p.VigenteDesde <= ahora && p.VigenteHasta >= ahora)
            .ToListAsync(cancelacion);

        return candidatas
            .Where(p => (sucursalId is not { } sucursal || p.EstaVigente(sucursal, ahora)) && p.AplicaA(articulo.Id, articulo.DepartamentoId, articulo.CategoriaId, articulo.MarcaId))
            .Select(p => new DatosOfertaChequeador(p.Codigo, p.Nombre, Descripcion(p, precio), DateOnly.FromDateTime(p.VigenteHasta.LocalDateTime)))
            .ToList();
    }

    /// <summary>Lo que dice la oferta en pantalla, en palabras del cliente.</summary>
    private static string Descripcion(Promocion promocion, decimal precio)
    {
        var cultura = CultureInfo.GetCultureInfo("es-DO");
        return promocion.Tipo switch
        {
            TipoPromocion.Porcentaje => $"{promocion.Valor.ToString("0.##", cultura)}% de descuento",
            TipoPromocion.MontoPorUnidad => $"{promocion.Valor.ToString("C2", cultura)} menos por unidad",
            TipoPromocion.PrecioEspecial => $"Precio especial {promocion.Valor.ToString("C2", cultura)}",
            TipoPromocion.LlevaPaga => $"Lleve {promocion.CantidadLleva} y pague {promocion.CantidadPaga}",
            TipoPromocion.PrecioPorCantidad => $"{promocion.Valor.ToString("C2", cultura)} llevando {promocion.CantidadMinima?.ToString("0.##", cultura)} o más",
            _ => promocion.Nombre,
        };
    }
}
