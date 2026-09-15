using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Organizacion;

internal sealed class ServicioParametrosCentral(ContextoDatosCentral contexto) : IParametrosCentral
{
    public Task<string?> ObtenerAsync(string clave, CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);

        return contexto.Parametros
            .AsNoTracking()
            .Where(p => p.Clave == clave && p.SucursalId == null && p.CajaId == null)
            .Select(p => p.Valor)
            .FirstOrDefaultAsync(cancelacion);
    }
}
