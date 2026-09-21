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
            .Select(s => new DatosSucursalChequeador(s.Id, s.Codigo, s.Nombre))
            .ToListAsync(cancelacion);

    public async Task<DatosPrecioChequeador?> ConsultarAsync(string codigo, int? sucursalId, CancellationToken cancelacion = default)
    {
        var buscado = (codigo ?? string.Empty).Trim();
        if (buscado.Length == 0 || !await HabilitadoAsync(cancelacion))
            return null;

        // Se busca por código interno, de barras, de proveedor o referencia: el cliente escanea lo que trae el empaque.
        // Todo va en una sola consulta —incluida la unidad de medida y los precios, que son propiedades sombra— y se
        // proyecta solo lo que la pantalla usa: el cliente está parado frente al equipo esperando el precio.
        var encontrado = await contexto.Articulos.AsNoTracking()
            .Where(a => a.Activo && a.VentaEnPos
                && (a.Codigo == buscado || a.Referencia == buscado || a.Codigos.Any(c => c.Codigo == buscado)))
            .Select(a => new DatosArticuloChequeador(
                a.Id,
                a.Codigo,
                a.Descripcion,
                contexto.UnidadesMedida.Where(u => u.Id == a.UnidadMedidaId).Select(u => u.Nombre).FirstOrDefault() ?? string.Empty,
                EF.Property<decimal>(a, ArticuloConfiguracion.PrecioDetalle),
                a.AplicaPrecioMayor ? EF.Property<decimal?>(a, ArticuloConfiguracion.PrecioMayor) : null,
                a.AplicaPrecioMayor ? a.CantidadMinimaMayor : null,
                a.RutaImagen,
                a.DepartamentoId,
                a.CategoriaId,
                a.MarcaId,
                contexto.Impuestos.Where(i => i.Id == a.ImpuestoId).Select(i => i.Porcentaje).FirstOrDefault()))
            .FirstOrDefaultAsync(cancelacion);
        if (encontrado is null)
            return null;

        // Los precios del maestro van sin ITBIS; el cliente ve lo que paga, con el ITBIS del artículo ya sumado.
        return new DatosPrecioChequeador(
            encontrado.Codigo,
            encontrado.Descripcion,
            encontrado.UnidadMedida,
            ConImpuesto(encontrado.Precio, encontrado.PorcentajeImpuesto),
            encontrado.PrecioMayor is { } mayor ? ConImpuesto(mayor, encontrado.PorcentajeImpuesto) : null,
            encontrado.CantidadMinimaMayor,
            encontrado.RutaImagen,
            await OfertasAsync(encontrado, sucursalId, cancelacion));
    }

    /// <summary>Lo que se trae de la base para responder: nada más que lo que se muestra o decide qué ofertas aplican.</summary>
    private sealed record DatosArticuloChequeador(
        int Id,
        string Codigo,
        string Descripcion,
        string UnidadMedida,
        decimal Precio,
        decimal? PrecioMayor,
        decimal? CantidadMinimaMayor,
        string? RutaImagen,
        int DepartamentoId,
        int? CategoriaId,
        int? MarcaId,
        decimal PorcentajeImpuesto);

    /// <summary>Un monto sin ITBIS con su ITBIS sumado, redondeado igual que en la caja.</summary>
    private static decimal ConImpuesto(decimal monto, decimal porcentajeImpuesto) =>
        monto + CgPos.Dominio.Fiscal.CalculoImpuestos.ImpuestoSobre(monto, porcentajeImpuesto);

    /// <summary>Ofertas vigentes ahora que alcanzan al artículo; las de fidelidad no se anuncian porque no son para todo el mundo.</summary>
    private async Task<IReadOnlyList<DatosOfertaChequeador>> OfertasAsync(DatosArticuloChequeador articulo, int? sucursalId, CancellationToken cancelacion)
    {
        var ahora = reloj.Ahora();
        var candidatas = await contexto.Promociones.AsNoTracking()
            .Where(p => p.Activa && !p.SoloFidelidad && p.VigenteDesde <= ahora && p.VigenteHasta >= ahora)
            .ToListAsync(cancelacion);

        return candidatas
            .Where(p => (sucursalId is not { } sucursal || p.EstaVigente(sucursal, ahora)) && p.AplicaA(articulo.Id, articulo.DepartamentoId, articulo.CategoriaId, articulo.MarcaId))
            .Select(p => new DatosOfertaChequeador(p.Codigo, p.Nombre, Descripcion(p, articulo.PorcentajeImpuesto), DateOnly.FromDateTime(p.VigenteHasta.LocalDateTime)))
            .ToList();
    }

    /// <summary>Lo que dice la oferta en pantalla, en palabras del cliente: los montos, con su ITBIS, como los paga.</summary>
    private static string Descripcion(Promocion promocion, decimal porcentajeImpuesto)
    {
        var cultura = CultureInfo.GetCultureInfo("es-DO");
        var monto = ConImpuesto(promocion.Valor, porcentajeImpuesto).ToString("C2", cultura);
        return promocion.Tipo switch
        {
            TipoPromocion.Porcentaje => $"{promocion.Valor.ToString("0.##", cultura)}% de descuento",
            TipoPromocion.MontoPorUnidad => $"{monto} menos por unidad",
            TipoPromocion.PrecioEspecial => $"Precio especial {monto}",
            TipoPromocion.LlevaPaga => $"Lleve {promocion.CantidadLleva} y pague {promocion.CantidadPaga}",
            TipoPromocion.PrecioPorCantidad => $"{monto} llevando {promocion.CantidadMinima?.ToString("0.##", cultura)} o más",
            _ => promocion.Nombre,
        };
    }
}
