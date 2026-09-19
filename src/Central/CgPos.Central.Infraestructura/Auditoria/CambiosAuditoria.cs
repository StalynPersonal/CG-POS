using System.Text.Encodings.Web;
using System.Text.Json;
using CgPos.Dominio.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CgPos.Central.Infraestructura.Auditoria;

/// <summary>
/// Antes y después de cada campo que cambió al guardar. Se calcula sola con lo que EF ya sabe (el valor original y el nuevo),
/// así que no hay que acordarse de registrarlo en cada servicio: basta con que la operación deje su registro de auditoría.
/// </summary>
public static class CambiosAuditoria
{
    /// <summary>
    /// El JSON se guarda con los acentos tal cual (sin escapar), porque se busca por texto desde la pantalla de Auditoría.
    /// No es HTML: se lee desde la base y se muestra como dato.
    /// </summary>
    private static readonly JsonSerializerOptions Formato = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Campos que nunca se muestran: se dice que cambiaron, pero no su contenido.</summary>
    private static readonly string[] Sensibles =
        ["contrasena", "hash", "clave", "secreto", "token", "pin", "certificado", "firma", "xml"];

    /// <summary>Columnas del propio sistema: dicen quién y cuándo, y eso ya está en el registro de auditoría.</summary>
    private static readonly string[] Tecnicas = ["ModificadoEn", "ModificadoPor", "Version"];

    /// <summary>
    /// Adjunta a los registros de auditoría de este guardado lo que cambió en la base. Un registro recibe los cambios de su
    /// misma entidad (por ejemplo, la acción sobre "Empresa" recibe los de la tabla Empresas) y, si no hay ninguno de su
    /// entidad, recibe todo lo que cambió en ese guardado, que es lo que produjo esa misma acción.
    /// </summary>
    public static void Adjuntar(DbContext contexto)
    {
        contexto.ChangeTracker.DetectChanges();

        var registros = contexto.ChangeTracker.Entries<RegistroAuditoria>()
            .Where(entrada => entrada.State == EntityState.Added && entrada.Entity.Cambios is null)
            .Select(entrada => entrada.Entity)
            .ToList();
        if (registros.Count == 0)
            return;

        var cambios = contexto.ChangeTracker.Entries()
            .Where(entrada => entrada.Entity is not RegistroAuditoria)
            .Select(Describir)
            .OfType<EntidadCambiada>()
            .ToList();
        if (cambios.Count == 0)
            return;

        foreach (var registro in registros)
        {
            var suyos = cambios.Where(cambio => cambio.EsDe(registro.TipoEntidad)).ToList();
            var serializar = suyos.Count > 0 ? suyos : cambios;
            registro.AdjuntarCambios(JsonSerializer.Serialize(serializar, Formato));
        }
    }

    private static EntidadCambiada? Describir(EntityEntry entrada)
    {
        var operacion = entrada.State switch
        {
            EntityState.Added => "Creado",
            EntityState.Modified => "Modificado",
            EntityState.Deleted => "Eliminado",
            _ => null,
        };
        if (operacion is null)
            return null;

        var campos = entrada.Properties
            .Where(propiedad => !Tecnicas.Contains(propiedad.Metadata.Name, StringComparer.Ordinal))
            .Where(propiedad => !propiedad.Metadata.IsPrimaryKey() || entrada.State != EntityState.Added)
            .Select(propiedad => Describir(entrada.State, propiedad))
            .OfType<CampoCambiado>()
            .ToList();
        if (campos.Count == 0)
            return null;

        var tabla = entrada.Metadata.GetTableName() ?? entrada.Metadata.ShortName();
        return new EntidadCambiada(entrada.Metadata.ShortName(), tabla, Llave(entrada), operacion, campos);
    }

    private static CampoCambiado? Describir(EntityState estado, PropertyEntry propiedad)
    {
        var (antes, despues) = estado switch
        {
            EntityState.Added => (null, Texto(propiedad.CurrentValue)),
            EntityState.Deleted => (Texto(propiedad.OriginalValue), null),
            _ => (Texto(propiedad.OriginalValue), Texto(propiedad.CurrentValue)),
        };

        if (estado == EntityState.Modified && !propiedad.IsModified)
            return null;
        if (antes == despues)
            return null;
        if (estado == EntityState.Added && despues is null)
            return null;

        var nombre = propiedad.Metadata.Name;
        return EsSensible(nombre)
            ? new CampoCambiado(nombre, antes is null ? null : "(oculto)", despues is null ? null : "(oculto)")
            : new CampoCambiado(nombre, antes, despues);
    }

    private static bool EsSensible(string nombre) =>
        Sensibles.Any(sensible => nombre.Contains(sensible, StringComparison.OrdinalIgnoreCase));

    private static string? Llave(EntityEntry entrada)
    {
        var llaves = entrada.Metadata.FindPrimaryKey()?.Properties ?? [];
        var valores = llaves
            .Select(propiedad => Texto(entrada.Property(propiedad.Name).CurrentValue))
            .OfType<string>()
            .ToList();
        return valores.Count == 0 ? null : string.Join('-', valores);
    }

    /// <summary>Un valor largo se recorta: la auditoría muestra qué cambió, no guarda una copia del documento.</summary>
    private static string? Texto(object? valor) => valor switch
    {
        null => null,
        bool booleano => booleano ? "Sí" : "No",
        DateTimeOffset fecha => fecha.ToString("yyyy-MM-dd HH:mm:ss"),
        DateTime fecha => fecha.ToString("yyyy-MM-dd HH:mm:ss"),
        decimal numero => numero.ToString("0.####"),
        byte[] bytes => $"({bytes.Length} bytes)",
        _ => Recortar(valor.ToString()),
    };

    private static string? Recortar(string? texto) =>
        texto is { Length: > 400 } largo ? largo[..400] + "…" : texto;
}

/// <summary>Una entidad que cambió al guardar, con los campos que cambiaron.</summary>
public sealed record EntidadCambiada(string Entidad, string Tabla, string? EntidadId, string Operacion, IReadOnlyList<CampoCambiado> Campos)
{
    /// <summary>Si estos cambios son los de la entidad que dice el registro de auditoría ("Empresa", "Sucursal"…).</summary>
    public bool EsDe(string tipoEntidad) =>
        Entidad.Equals(tipoEntidad, StringComparison.OrdinalIgnoreCase) || Tabla.Equals(tipoEntidad, StringComparison.OrdinalIgnoreCase);
}

public sealed record CampoCambiado(string Campo, string? Antes, string? Despues);
