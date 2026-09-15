using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Maestros;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Infraestructura.Sincronizacion;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Maestros;

internal sealed class ServicioMaestrosCentral(ContextoDatosCentral contexto, IPublicadorMaestros publicador) : IServicioMaestrosCentral
{
    public async Task<IReadOnlyList<DatosMaestroCentral<T>>> ListarAsync<T>(TipoMaestro tipo, CancellationToken cancelacion = default) =>
        (await contexto.MaestrosCentral.AsNoTracking()
            .Where(m => m.Tipo == tipo)
            .OrderBy(m => m.Codigo)
            .ThenByDescending(m => m.ModificadoEn)
            .ToListAsync(cancelacion))
        .Select(m => new DatosMaestroCentral<T>(FormatoMaestros.Leer<T>(m), m.ModificadoEn, m.ModificadoPor))
        .ToList();

    public async Task<ResultadoAdministracion> PublicarAsync(PaqueteMaestros paquete, Guid id, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        if (id == Guid.Empty)
            return ResultadoAdministracion.Error("El registro necesita un Id.");

        try
        {
            await publicador.PublicarAsync(paquete, actor.Nombre, cancelacion);
            return ResultadoAdministracion.Correcto(id);
        }
        catch (PublicacionInvalidaExcepcion excepcion)
        {
            return ResultadoAdministracion.Error(string.Join(" ", excepcion.Errores));
        }
    }
}
