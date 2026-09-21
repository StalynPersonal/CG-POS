using CgPos.Dominio.Catalogo;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Pos.Infraestructura.Catalogo;

/// <summary>Registra precios solo cuando cambian, para que cargar el mismo maestro varias veces no ensucie la bitácora.</summary>
internal static class RegistroPrecios
{
    /// <returns><c>true</c> si se agregó un registro de precio al contexto (sin guardar).</returns>
    public static async Task<bool> RegistrarSiCambiaAsync(
        ContextoDatosPos contexto,
        int articuloId,
        ListaPrecio lista,
        decimal precio,
        DateTimeOffset vigenteDesde,
        DateTimeOffset ahora,
        string origen,
        int? usuarioId,
        string? usuarioNombre,
        CancellationToken cancelacion,
        bool sinHistorial = false)
    {
        // Un artículo que se acaba de crear no tiene precios anteriores, ni en la base ni en esta misma unidad de trabajo:
        // preguntarlo por cada uno es lo que hacía eterna la primera carga de una caja.
        if (sinHistorial)
        {
            contexto.PreciosArticulo.Add(PrecioArticulo.Registrar(articuloId, lista, precio, vigenteDesde, ahora, origen, usuarioId, usuarioNombre));
            return true;
        }

        bool yaRegistrado;

        if (vigenteDesde <= ahora)
        {
            var precioVigente = await contexto.PreciosArticulo
                .Where(p => p.ArticuloId == articuloId && p.Lista == lista && p.VigenteDesde <= ahora)
                .OrderByDescending(p => p.VigenteDesde)
                .ThenByDescending(p => p.RegistradoEn)
                .Select(p => (decimal?)p.Precio)
                .FirstOrDefaultAsync(cancelacion);

            yaRegistrado = precioVigente == precio;
        }
        else
        {
            // Precio programado a futuro: idempotente si ya existe el mismo precio con la misma vigencia.
            yaRegistrado = await contexto.PreciosArticulo.AnyAsync(
                p => p.ArticuloId == articuloId && p.Lista == lista && p.VigenteDesde == vigenteDesde && p.Precio == precio,
                cancelacion);
        }

        // También se consideran los precios agregados en esta misma unidad de trabajo.
        yaRegistrado |= contexto.PreciosArticulo.Local.Any(p =>
            p.ArticuloId == articuloId && p.Lista == lista && p.Precio == precio && p.VigenteDesde == vigenteDesde);

        if (yaRegistrado)
            return false;

        contexto.PreciosArticulo.Add(PrecioArticulo.Registrar(articuloId, lista, precio, vigenteDesde, ahora, origen, usuarioId, usuarioNombre));
        return true;
    }
}
