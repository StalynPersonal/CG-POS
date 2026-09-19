using CgPos.Central.Infraestructura.Persistencia.Configuraciones;
using CgPos.Dominio.Auditoria;
using CgPos.Dominio.Comun;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Persistencia;

/// <summary>
/// Llena solas las columnas «ModificadoEn» y «ModificadoPor» de las filas que se crean o cambian en este guardado.
///
/// Quién lo hizo sale del registro de auditoría que la misma operación está guardando, que es el que sabe el usuario;
/// si la operación no deja auditoría (procesos del propio Central), la fila queda como «Sistema». Así ningún servicio
/// tiene que acordarse de marcarla, y el que ya la marcó a mano conserva su valor.
/// </summary>
internal static class MarcasModificacion
{
    private const string Sistema = "Sistema";

    /// <summary>
    /// Campos que se mueven solos al operar (entrar al sistema, fallar una clave) y no son un cambio hecho por alguien:
    /// si es lo único que cambió, la fila conserva quién la modificó de verdad y la auditoría no se llena de ingresos.
    /// </summary>
    private static readonly string[] Automaticos =
        ["UltimoIngresoEn", "IntentosFallidos", "BloqueadoHasta", "UltimaRecepcionEn", "UltimaDescargaEn", "UltimoUsoEn"];

    public static void Aplicar(DbContext contexto, TimeProvider reloj)
    {
        contexto.ChangeTracker.DetectChanges();

        var auditoria = contexto.ChangeTracker.Entries<RegistroAuditoria>()
            .Where(entrada => entrada.State == EntityState.Added)
            .Select(entrada => entrada.Entity)
            .FirstOrDefault();

        var ahora = auditoria?.OcurridoEn ?? reloj.Ahora();
        var usuario = auditoria?.UsuarioNombre is { Length: > 0 } nombre ? nombre : Sistema;

        foreach (var entrada in contexto.ChangeTracker.Entries())
        {
            if (entrada.State is not (EntityState.Added or EntityState.Modified))
                continue;

            var propiedades = entrada.Metadata.GetProperties().Select(propiedad => propiedad.Name).ToList();
            if (!propiedades.Contains(ColumnasMaestro.ModificadoEn) || !propiedades.Contains(ColumnasMaestro.ModificadoPor))
                continue;

            // Entrar al sistema mueve la fecha del último ingreso, pero nadie «modificó» al usuario: no se marca.
            if (entrada.State == EntityState.Modified
                && entrada.Properties.Where(propiedad => propiedad.IsModified)
                    .All(propiedad => Automaticos.Contains(propiedad.Metadata.Name, StringComparer.Ordinal)))
                continue;

            // Lo que el servicio ya marcó a mano (los maestros que bajan a las cajas) se respeta: al crear, porque ya
            // trae un valor; al modificar, porque el valor que va a guardarse es distinto del que había.
            var quien = entrada.Property(ColumnasMaestro.ModificadoPor);
            var marcadoAMano = entrada.State == EntityState.Added
                ? quien.CurrentValue is string puesto && puesto.Length > 0
                : !Equals(quien.CurrentValue, quien.OriginalValue);
            if (marcadoAMano)
                continue;

            entrada.Property(ColumnasMaestro.ModificadoEn).CurrentValue = ahora;
            quien.CurrentValue = usuario.Length > ColumnasMaestro.LargoMaximoUsuario ? usuario[..ColumnasMaestro.LargoMaximoUsuario] : usuario;
        }
    }
}
