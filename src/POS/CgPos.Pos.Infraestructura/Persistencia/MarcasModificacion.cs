using CgPos.Dominio.Auditoria;
using CgPos.Dominio.Comun;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Pos.Infraestructura.Persistencia;

/// <summary>
/// Llena solas las columnas «ModificadoEn» y «ModificadoPor» de la organización de la caja (empresa, sucursal, caja y
/// parámetros). Quién lo hizo sale del registro de auditoría que la misma operación guarda; cuando es la sincronización
/// la que escribe, queda «Central», que es de donde vienen esos datos.
/// </summary>
internal static class MarcasModificacion
{
    public const string ModificadoEn = "ModificadoEn";
    public const string ModificadoPor = "ModificadoPor";
    public const int LargoMaximoUsuario = 150;

    private const string Central = "Central";

    public static void Aplicar(DbContext contexto, TimeProvider reloj)
    {
        contexto.ChangeTracker.DetectChanges();

        var auditoria = contexto.ChangeTracker.Entries<RegistroAuditoria>()
            .Where(entrada => entrada.State == EntityState.Added)
            .Select(entrada => entrada.Entity)
            .FirstOrDefault();

        var ahora = auditoria?.OcurridoEn ?? reloj.Ahora();
        var usuario = auditoria?.UsuarioNombre is { Length: > 0 } nombre ? nombre : Central;

        foreach (var entrada in contexto.ChangeTracker.Entries())
        {
            if (entrada.State is not (EntityState.Added or EntityState.Modified))
                continue;

            var propiedades = entrada.Metadata.GetProperties().Select(propiedad => propiedad.Name).ToList();
            if (!propiedades.Contains(ModificadoEn) || !propiedades.Contains(ModificadoPor))
                continue;

            entrada.Property(ModificadoEn).CurrentValue = ahora;
            entrada.Property(ModificadoPor).CurrentValue =
                usuario.Length > LargoMaximoUsuario ? usuario[..LargoMaximoUsuario] : usuario;
        }
    }
}
