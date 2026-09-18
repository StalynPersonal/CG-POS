using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.ListasBoda;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.ListasBoda;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.ListasBoda;

internal sealed class ServicioListasBoda(ContextoDatosCentral contexto, IParametrosCentral parametros, IAuditoriaCentral auditoria, TimeProvider reloj)
    : IServicioListasBoda
{
    private const string TipoEntidad = "ListaBoda";
    private const string PrefijoNumero = "LB";

    /// <summary>Si lo comprado se descuenta de las cantidades pedidas; lo decide el negocio en el Central.</summary>
    private Task<bool> DescuentaComprasAsync(CancellationToken cancelacion) =>
        parametros.ObtenerBooleanoOpcionalAsync(ClavesParametrosCentral.ListasBodaDescontarCompras, cancelacion);

    public async Task<DatosListaBodaParaCaja?> BuscarParaCajaAsync(string numero, CancellationToken cancelacion = default)
    {
        var buscado = (numero ?? string.Empty).Trim().ToUpperInvariant();
        if (buscado.Length == 0)
            return null;

        var lista = await contexto.ListasBoda.AsNoTracking().Include(l => l.Articulos).FirstOrDefaultAsync(l => l.Numero == buscado, cancelacion);
        return lista is null
            ? null
            : new DatosListaBodaParaCaja(lista.Numero, lista.Evento, lista.FechaEvento, lista.ClienteNombre, lista.Estado,
                await DescuentaComprasAsync(cancelacion), Articulos(lista));
    }

    public async Task<IReadOnlyList<DatosListaBoda>> ListarAsync(string? buscar, EstadoListaBoda? estado, CancellationToken cancelacion = default)
    {
        var consulta = contexto.ListasBoda.AsNoTracking().Include(l => l.Articulos).Include(l => l.Compras).AsQueryable();
        if (estado is { } filtro)
            consulta = consulta.Where(l => l.Estado == filtro);
        if (buscar is { Length: > 0 })
        {
            var texto = buscar.Trim();
            consulta = consulta.Where(l => l.Numero.Contains(texto) || l.Evento.Contains(texto) || l.ClienteNombre.Contains(texto)
                || l.ClienteDocumento.Contains(texto));
        }

        var listas = await consulta.OrderByDescending(l => l.FechaEvento).ThenBy(l => l.Numero).Take(200).ToListAsync(cancelacion);
        return await DatosAsync(listas, cancelacion);
    }

    public async Task<DatosListaBoda?> ObtenerAsync(int listaBodaId, CancellationToken cancelacion = default)
    {
        var lista = await contexto.ListasBoda.AsNoTracking().Include(l => l.Articulos).Include(l => l.Compras)
            .FirstOrDefaultAsync(l => l.Id == listaBodaId, cancelacion);
        return lista is null ? null : (await DatosAsync([lista], cancelacion))[0];
    }

    public async Task<ResultadoAdministracion> CrearAsync(SolicitudListaBoda solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (Invalida(solicitud) is { } problema)
            return ResultadoAdministracion.Error(problema);

        var numero = (solicitud.Numero ?? string.Empty).Trim().ToUpperInvariant();
        if (numero.Length == 0)
            numero = await SiguienteNumeroAsync(cancelacion);
        else if (await contexto.ListasBoda.AnyAsync(l => l.Numero == numero, cancelacion))
            return ResultadoAdministracion.Error($"Ya existe una lista con el número {numero}.");

        if (solicitud.SucursalId is { } sucursalId && !await contexto.Sucursales.AnyAsync(s => s.Id == sucursalId, cancelacion))
            return ResultadoAdministracion.Error("La sucursal indicada no existe.");

        var ahora = reloj.GetUtcNow();
        try
        {
            var lista = ListaBoda.Crear(numero, solicitud.Evento, solicitud.FechaEvento, solicitud.Lugar, solicitud.ClienteDocumento, solicitud.ClienteNombre,
                solicitud.ClienteTelefono, solicitud.ClienteCorreo, solicitud.SucursalId, solicitud.Observacion, ahora);
            contexto.ListasBoda.Add(lista);
            await contexto.SaveChangesAsync(cancelacion);

            lista.ReemplazarArticulos(solicitud.Articulos.Select(a => (a.ArticuloCodigo, a.Descripcion, a.Cantidad)), ahora);
            auditoria.Registrar(new EntradaAuditoria("ListasBoda.Creada", TipoEntidad, lista.Numero,
                Detalle: new { lista.Evento, lista.FechaEvento, lista.ClienteNombre, Articulos = solicitud.Articulos.Count }, Usuario: actor));
            await contexto.SaveChangesAsync(cancelacion);
            return ResultadoAdministracion.Correcto(lista.Id);
        }
        catch (ArgumentException excepcion)
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(ValidacionMaestros.MensajeError(excepcion));
        }
    }

    public async Task<ResultadoAdministracion> ActualizarAsync(int listaBodaId, SolicitudListaBoda solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (Invalida(solicitud) is { } problema)
            return ResultadoAdministracion.Error(problema);

        var lista = await contexto.ListasBoda.Include(l => l.Articulos).FirstOrDefaultAsync(l => l.Id == listaBodaId, cancelacion);
        if (lista is null)
            return ResultadoAdministracion.Inexistente("La lista no existe.");
        if (solicitud.SucursalId is { } sucursalId && !await contexto.Sucursales.AnyAsync(s => s.Id == sucursalId, cancelacion))
            return ResultadoAdministracion.Error("La sucursal indicada no existe.");

        var ahora = reloj.GetUtcNow();
        try
        {
            lista.Actualizar(solicitud.Evento, solicitud.FechaEvento, solicitud.Lugar, solicitud.ClienteDocumento, solicitud.ClienteNombre,
                solicitud.ClienteTelefono, solicitud.ClienteCorreo, solicitud.SucursalId, solicitud.Observacion, ahora);
            lista.ReemplazarArticulos(solicitud.Articulos.Select(a => (a.ArticuloCodigo, a.Descripcion, a.Cantidad)), ahora);
            auditoria.Registrar(new EntradaAuditoria("ListasBoda.Actualizada", TipoEntidad, lista.Numero,
                Detalle: new { lista.Evento, lista.FechaEvento, Articulos = solicitud.Articulos.Count }, Usuario: actor));
            await contexto.SaveChangesAsync(cancelacion);
            return ResultadoAdministracion.Correcto(lista.Id);
        }
        catch (ArgumentException excepcion)
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(ValidacionMaestros.MensajeError(excepcion));
        }
    }

    public async Task<ResultadoAdministracion> CambiarEstadoAsync(int listaBodaId, bool cerrar, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        var lista = await contexto.ListasBoda.FirstOrDefaultAsync(l => l.Id == listaBodaId, cancelacion);
        if (lista is null)
            return ResultadoAdministracion.Inexistente("La lista no existe.");

        var ahora = reloj.GetUtcNow();
        if (cerrar)
            lista.Cerrar(ahora);
        else
            lista.Reabrir(ahora);

        auditoria.Registrar(new EntradaAuditoria(cerrar ? "ListasBoda.Cerrada" : "ListasBoda.Reabierta", TipoEntidad, lista.Numero, Usuario: actor));
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(lista.Id);
    }

    private static string? Invalida(SolicitudListaBoda solicitud) =>
        string.IsNullOrWhiteSpace(solicitud.Evento) ? "Indique el evento de la lista."
        : string.IsNullOrWhiteSpace(solicitud.ClienteNombre) ? "Indique el nombre del cliente."
        : string.IsNullOrWhiteSpace(solicitud.ClienteDocumento) ? "Indique la cédula o el RNC del cliente."
        : solicitud.Articulos is not { Count: > 0 } ? "Agregue al menos un artículo a la lista."
        : null;

    /// <summary>Numeración propia del Central: LB seguido de un correlativo, para que el cliente la diga en caja.</summary>
    private async Task<string> SiguienteNumeroAsync(CancellationToken cancelacion)
    {
        var usados = await contexto.ListasBoda.AsNoTracking()
            .Where(l => l.Numero.StartsWith(PrefijoNumero))
            .Select(l => l.Numero)
            .ToListAsync(cancelacion);

        var siguiente = usados
            .Select(n => int.TryParse(n[PrefijoNumero.Length..], out var valor) ? valor : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"{PrefijoNumero}{siguiente:000000}";
    }

    private static List<DatosArticuloListaBoda> Articulos(ListaBoda lista) =>
        lista.Articulos
            .OrderBy(a => a.Descripcion)
            .Select(a => new DatosArticuloListaBoda(a.ArticuloCodigo, a.Descripcion, a.Cantidad, a.Comprado, a.Pendiente))
            .ToList();

    private async Task<IReadOnlyList<DatosListaBoda>> DatosAsync(IReadOnlyList<ListaBoda> listas, CancellationToken cancelacion)
    {
        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Nombre, cancelacion);
        var cajas = await contexto.Cajas.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Codigo, cancelacion);

        return listas.Select(lista => new DatosListaBoda(
            lista.Id,
            lista.Numero,
            lista.Evento,
            lista.FechaEvento,
            lista.Lugar,
            lista.ClienteDocumento,
            lista.ClienteNombre,
            lista.ClienteTelefono,
            lista.ClienteCorreo,
            lista.SucursalId,
            lista.SucursalId is { } id ? sucursales.GetValueOrDefault(id) : null,
            lista.Observacion,
            lista.Estado,
            lista.Compras.Sum(c => c.Monto),
            lista.CreadaEn,
            Articulos(lista),
            lista.Compras
                .OrderByDescending(c => c.Fecha)
                .Select(c => new DatosCompraListaBoda(c.VentaNumero, cajas.GetValueOrDefault(c.CajaId) ?? string.Empty, c.Monto, c.Fecha))
                .ToList())).ToList();
    }
}
