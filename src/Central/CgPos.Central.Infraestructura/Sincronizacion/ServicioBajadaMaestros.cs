using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.CargaInicial;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Sincronizacion;

internal sealed class ServicioBajadaMaestros(ContextoDatosCentral contexto, TimeProvider reloj) : IServicioBajadaMaestros
{
    public async Task<PaqueteBajadaMaestros> ObtenerAsync(CajaRemitente caja, long desde, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(caja);
        desde = Math.Max(0, desde);

        // Hasta la versión más alta ya confirmada: una transacción aún abierta con una versión menor no queda saltada.
        var hasta = Math.Max(desde, await contexto.Database
            .SqlQueryRaw<long>("SELECT CAST(MIN_ACTIVE_ROWVERSION() AS bigint) - 1 AS [Value]")
            .SingleAsync(cancelacion));

        PaqueteCargaInicial? organizacion = null;
        Contratos.Catalogo.PaqueteMaestros? maestros = null;

        if (hasta > desde)
        {
            var filas = await EnRango(contexto.MaestrosCentral.AsNoTracking(), desde, hasta)
                .Where(m => m.CajaId == null || m.CajaId == caja.CajaId)
                .ToListAsync(cancelacion);
            var sucursales = await EnRango(contexto.Sucursales.AsNoTracking(), desde, hasta).ToListAsync(cancelacion);
            var cajas = await EnRango(contexto.Cajas.AsNoTracking(), desde, hasta).ToListAsync(cancelacion);
            var parametros = await EnRango(contexto.Parametros.AsNoTracking(), desde, hasta)
                .Where(p => !p.Clave.StartsWith(PublicadorMaestros.PrefijoParametrosCentral)
                    && ((p.SucursalId == null && p.CajaId == null) || p.SucursalId == caja.SucursalId || p.CajaId == caja.CajaId))
                .ToListAsync(cancelacion);
            var empresaCambio = await EnRango(contexto.Empresas.AsNoTracking(), desde, hasta).AnyAsync(cancelacion);

            var roles = FormatoMaestros.Filtrar<RolCarga>(filas, TipoMaestro.RolCaja);
            var usuarios = FormatoMaestros.Filtrar<UsuarioCarga>(filas, TipoMaestro.UsuarioCaja);

            if ((empresaCambio || sucursales.Count > 0 || cajas.Count > 0 || parametros.Count > 0 || roles.Count > 0 || usuarios.Count > 0)
                && await contexto.Empresas.AsNoTracking().SingleOrDefaultAsync(cancelacion) is { } empresa)
            {
                organizacion = new PaqueteCargaInicial(
                    new EmpresaCarga(empresa.Id, empresa.Rnc, empresa.RazonSocial, empresa.NombreComercial, empresa.Direccion, empresa.Telefono),
                    sucursales.Select(s => new SucursalCarga(s.Id, s.Codigo, s.Nombre, s.Direccion, s.Telefono, s.Activa)).ToList(),
                    cajas.Select(c => new CajaCarga(c.Id, c.SucursalId, c.Codigo, c.Nombre, c.Habilitada)).ToList(),
                    roles,
                    usuarios,
                    parametros.Select(p => new ParametroCarga(p.Id, p.Clave, p.Valor, p.Descripcion, p.SucursalId, p.CajaId)).ToList());
            }

            maestros = FormatoMaestros.Armar(filas);
        }

        var estado = await contexto.EstadosSincronizacionCaja.SingleOrDefaultAsync(e => e.CajaId == caja.CajaId, cancelacion);
        if (estado is null)
        {
            estado = EstadoSincronizacionCaja.Crear(caja.CajaId);
            contexto.EstadosSincronizacionCaja.Add(estado);
        }

        estado.RegistrarDescarga(reloj.GetUtcNow(), desde, hasta);
        await contexto.SaveChangesAsync(cancelacion);

        return new PaqueteBajadaMaestros(desde, hasta, organizacion, maestros);
    }

    private static IQueryable<T> EnRango<T>(IQueryable<T> consulta, long desde, long hasta) where T : class =>
        consulta.Where(e => EF.Property<long>(e, ContextoDatosCentral.ColumnaVersion) > desde && EF.Property<long>(e, ContextoDatosCentral.ColumnaVersion) <= hasta);
}
