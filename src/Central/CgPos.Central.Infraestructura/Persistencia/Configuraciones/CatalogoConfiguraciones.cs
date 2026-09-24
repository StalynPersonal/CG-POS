using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Clientes;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Fidelidad;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Promociones;
using CgPos.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CgPos.Central.Infraestructura.Persistencia.Configuraciones;

/// <summary>Columnas comunes de las tablas de maestros: versión de fila para la bajada a las cajas y quién lo cambió por última vez.</summary>
internal static class ColumnasMaestro
{
    public const string ModificadoEn = "ModificadoEn";
    public const string ModificadoPor = "ModificadoPor";
    public const int LargoMaximoUsuario = 150;

    /// <summary>Anota cuándo y quién cambió un maestro: la fila recibe versión nueva y baja otra vez a las cajas.</summary>
    public static void Marcar(ContextoDatosCentral contexto, object entidad, DateTimeOffset ahora, string usuario)
    {
        var entrada = contexto.Entry(entidad);
        entrada.Property(ModificadoEn).CurrentValue = ahora;
        entrada.Property(ModificadoPor).CurrentValue = usuario.Length > LargoMaximoUsuario ? usuario[..LargoMaximoUsuario] : usuario;
    }

    public static void Configurar<T>(EntityTypeBuilder<T> constructor) where T : class
    {
        constructor.Property<long>(ContextoDatosCentral.ColumnaVersion).IsRowVersion().HasConversion<byte[]>();
        constructor.HasIndex(ContextoDatosCentral.ColumnaVersion);
        ConfigurarMarcas(constructor);
    }

    /// <summary>Solo las columnas de quién y cuándo, para lo que se administra a mano y no es un maestro que baja a las cajas.</summary>
    public static void ConfigurarMarcas(EntityTypeBuilder constructor)
    {
        constructor.Property<DateTimeOffset>(ModificadoEn).HasPrecision(3);
        constructor.Property<string>(ModificadoPor).HasMaxLength(LargoMaximoUsuario).IsRequired();
    }
}

internal sealed class DepartamentoConfiguracion : IEntityTypeConfiguration<Departamento>
{
    public void Configure(EntityTypeBuilder<Departamento> constructor)
    {
        constructor.ToTable("Departamentos");
        constructor.HasKey(d => d.Id);
        constructor.Property(d => d.Nombre).HasMaxLength(Departamento.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(d => d.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class CategoriaConfiguracion : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> constructor)
    {
        constructor.ToTable("Categorias");
        constructor.HasKey(c => c.Id);
        constructor.Property(c => c.Nombre).HasMaxLength(Categoria.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(c => c.Codigo).IsUnique();
        constructor.HasOne<Departamento>().WithMany().HasForeignKey(c => c.DepartamentoId).OnDelete(DeleteBehavior.Restrict);
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class MarcaConfiguracion : IEntityTypeConfiguration<Marca>
{
    public void Configure(EntityTypeBuilder<Marca> constructor)
    {
        constructor.ToTable("Marcas");
        constructor.HasKey(m => m.Id);
        constructor.Property(m => m.Nombre).HasMaxLength(Marca.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(m => m.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class UnidadMedidaConfiguracion : IEntityTypeConfiguration<UnidadMedida>
{
    public void Configure(EntityTypeBuilder<UnidadMedida> constructor)
    {
        constructor.ToTable("UnidadesMedida");
        constructor.HasKey(u => u.Id);
        constructor.Property(u => u.Abreviatura).HasMaxLength(UnidadMedida.LargoMaximoAbreviatura).IsRequired();
        constructor.Property(u => u.Nombre).HasMaxLength(UnidadMedida.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(u => u.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class ImpuestoConfiguracion : IEntityTypeConfiguration<Impuesto>
{
    public void Configure(EntityTypeBuilder<Impuesto> constructor)
    {
        constructor.ToTable("Impuestos");
        constructor.HasKey(i => i.Id);
        constructor.Property(i => i.Codigo).HasMaxLength(Impuesto.LargoMaximoCodigo).IsRequired();
        constructor.Property(i => i.Nombre).HasMaxLength(Impuesto.LargoMaximoNombre).IsRequired();
        constructor.Property(i => i.Porcentaje).HasPrecision(5, 2);
        constructor.HasIndex(i => i.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

/// <summary>El artículo del Central guarda también los precios que se publican (con impuesto) y desde cuándo rigen.</summary>
internal sealed class ArticuloConfiguracion : IEntityTypeConfiguration<Articulo>
{
    public const string PrecioDetalle = "PrecioDetalle";
    public const string PrecioMayor = "PrecioMayor";
    public const string PreciosVigentesDesde = "PreciosVigentesDesde";

    public void Configure(EntityTypeBuilder<Articulo> constructor)
    {
        constructor.ToTable("Articulos");
        constructor.HasKey(a => a.Id);

        constructor.Property(a => a.Codigo).HasMaxLength(Articulo.LargoMaximoCodigo).IsRequired();
        constructor.Property(a => a.Descripcion).HasMaxLength(Articulo.LargoMaximoDescripcion).IsRequired();
        constructor.Property(a => a.Referencia).HasMaxLength(Articulo.LargoMaximoReferencia);
        constructor.Property(a => a.RutaImagen).HasMaxLength(Articulo.LargoMaximoRutaImagen);
        constructor.Property<decimal>(PrecioDetalle).HasPrecision(18, 2);
        constructor.Property<decimal?>(PrecioMayor).HasPrecision(18, 2);
        constructor.Property<DateTimeOffset?>(PreciosVigentesDesde).HasPrecision(3);

        constructor.HasIndex(a => a.Codigo).IsUnique();
        constructor.HasIndex(a => a.Descripcion);

        // El chequeador de la tienda busca lo que el cliente escanea por código interno, por código de barras y por esta
        // referencia, todo en una consulta: sin índice aquí habría que recorrer los artículos enteros en cada consulta.
        constructor.HasIndex(a => a.Referencia);

        constructor.HasOne<Departamento>().WithMany().HasForeignKey(a => a.DepartamentoId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Categoria>().WithMany().HasForeignKey(a => a.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Marca>().WithMany().HasForeignKey(a => a.MarcaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<UnidadMedida>().WithMany().HasForeignKey(a => a.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Impuesto>().WithMany().HasForeignKey(a => a.ImpuestoId).OnDelete(DeleteBehavior.Restrict);

        constructor.HasMany(a => a.Codigos).WithOne().HasForeignKey(c => c.ArticuloId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(a => a.Codigos).UsePropertyAccessMode(PropertyAccessMode.Field);
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class CodigoArticuloConfiguracion : IEntityTypeConfiguration<CodigoArticulo>
{
    public void Configure(EntityTypeBuilder<CodigoArticulo> constructor)
    {
        constructor.ToTable("CodigosArticulo");
        constructor.HasKey(c => new { c.ArticuloId, c.Codigo });
        constructor.Property(c => c.Codigo).HasMaxLength(Articulo.LargoMaximoCodigo);
        constructor.HasIndex(c => c.Codigo).IsUnique();
    }
}

internal sealed class ClienteConfiguracion : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> constructor)
    {
        constructor.ToTable("Clientes");
        constructor.HasKey(c => c.Id);

        constructor.Property(c => c.Codigo).HasMaxLength(Cliente.LargoMaximoCodigo).IsRequired();
        constructor.Property(c => c.Documento).HasMaxLength(Cliente.LargoMaximoDocumento).IsUnicode(false).IsRequired();
        constructor.Property(c => c.Nombre).HasMaxLength(Cliente.LargoMaximoNombre).IsRequired();
        constructor.Property(c => c.Telefono).HasMaxLength(Cliente.LargoMaximoTelefono);
        constructor.Property(c => c.Correo).HasMaxLength(Cliente.LargoMaximoCorreo);
        constructor.Property(c => c.Contacto).HasMaxLength(Cliente.LargoMaximoNombre);
        constructor.Property(c => c.TelefonoAlterno).HasMaxLength(Cliente.LargoMaximoTelefono);

        constructor.HasIndex(c => c.Codigo).IsUnique();
        constructor.HasIndex(c => new { c.TipoDocumento, c.Documento }).IsUnique();
        constructor.HasIndex(c => c.Nombre);

        constructor.HasMany(c => c.Direcciones).WithOne().HasForeignKey(d => d.ClienteId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(c => c.Direcciones).UsePropertyAccessMode(PropertyAccessMode.Field);
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class DireccionClienteConfiguracion : IEntityTypeConfiguration<DireccionCliente>
{
    public void Configure(EntityTypeBuilder<DireccionCliente> constructor)
    {
        constructor.ToTable("DireccionesCliente");
        constructor.HasKey(d => d.Id);
        constructor.Property(d => d.Alias).HasMaxLength(DireccionCliente.LargoMaximoAlias).IsRequired();
        constructor.Property(d => d.Direccion).HasMaxLength(DireccionCliente.LargoMaximoDireccion).IsRequired();
        constructor.Property(d => d.Sector).HasMaxLength(DireccionCliente.LargoMaximoLugar);
        constructor.Property(d => d.Ciudad).HasMaxLength(DireccionCliente.LargoMaximoLugar);
        constructor.Property(d => d.Referencia).HasMaxLength(DireccionCliente.LargoMaximoDireccion);
        constructor.Property(d => d.Telefono).HasMaxLength(Cliente.LargoMaximoTelefono);
        constructor.HasIndex(d => new { d.ClienteId, d.Alias }).IsUnique();
    }
}

internal sealed class MonedaConfiguracion : IEntityTypeConfiguration<Moneda>
{
    public void Configure(EntityTypeBuilder<Moneda> constructor)
    {
        constructor.ToTable("Monedas");
        constructor.HasKey(m => m.Id);
        constructor.Property(m => m.Codigo).HasMaxLength(Moneda.LargoCodigo).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(m => m.Nombre).HasMaxLength(Moneda.LargoMaximoNombre).IsRequired();
        constructor.Property(m => m.Simbolo).HasMaxLength(Moneda.LargoMaximoSimbolo).IsRequired();
        constructor.HasIndex(m => m.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class FormaPagoConfiguracion : IEntityTypeConfiguration<FormaPago>
{
    public void Configure(EntityTypeBuilder<FormaPago> constructor)
    {
        constructor.ToTable("FormasPago");
        constructor.HasKey(f => f.Id);
        constructor.Property(f => f.Codigo).HasMaxLength(FormaPago.LargoMaximoCodigo).IsRequired();
        constructor.Property(f => f.Nombre).HasMaxLength(FormaPago.LargoMaximoNombre).IsRequired();
        constructor.Property(f => f.Moneda).HasMaxLength(3).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.HasIndex(f => f.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class BancoConfiguracion : IEntityTypeConfiguration<Banco>
{
    public void Configure(EntityTypeBuilder<Banco> constructor)
    {
        constructor.ToTable("Bancos");
        constructor.HasKey(b => b.Id);
        constructor.Property(b => b.Codigo).HasMaxLength(Banco.LargoMaximoCodigo).IsRequired();
        constructor.Property(b => b.Nombre).HasMaxLength(Banco.LargoMaximoNombre).IsRequired();
        constructor.Property(b => b.RutaLogo).HasMaxLength(Banco.LargoMaximoRutaLogo);
        constructor.HasIndex(b => b.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class TipoTarjetaConfiguracion : IEntityTypeConfiguration<TipoTarjeta>
{
    public void Configure(EntityTypeBuilder<TipoTarjeta> constructor)
    {
        constructor.ToTable("TiposTarjeta");
        constructor.HasKey(t => t.Id);
        constructor.Property(t => t.Nombre).HasMaxLength(TipoTarjeta.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(t => t.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class DenominacionConfiguracion : IEntityTypeConfiguration<Denominacion>
{
    public void Configure(EntityTypeBuilder<Denominacion> constructor)
    {
        constructor.ToTable("Denominaciones");
        constructor.HasKey(d => d.Id);
        constructor.Property(d => d.Moneda).HasMaxLength(3).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(d => d.Valor).HasPrecision(18, 2);
        constructor.HasIndex(d => new { d.Moneda, d.Valor, d.Tipo }).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class TasaCambioConfiguracion : IEntityTypeConfiguration<TasaCambio>
{
    public void Configure(EntityTypeBuilder<TasaCambio> constructor)
    {
        constructor.ToTable("TasasCambio");
        constructor.HasKey(t => t.Id);
        constructor.Property(t => t.Moneda).HasMaxLength(3).IsUnicode(false).IsRequired();
        constructor.Property(t => t.Tasa).HasPrecision(18, 4);
        constructor.HasIndex(t => new { t.Moneda, t.VigenteDesde }).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class DescuentoTarjetaConfiguracion : IEntityTypeConfiguration<DescuentoTarjeta>
{
    public void Configure(EntityTypeBuilder<DescuentoTarjeta> constructor)
    {
        constructor.ToTable("DescuentosTarjeta");
        constructor.HasKey(d => d.Id);
        constructor.Property(d => d.Codigo).HasMaxLength(DescuentoTarjeta.LargoMaximoCodigo).IsUnicode(false).IsRequired();
        constructor.Property(d => d.Nombre).HasMaxLength(DescuentoTarjeta.LargoMaximoNombre).IsRequired();
        constructor.Property(d => d.Bines).HasMaxLength(DescuentoTarjeta.LargoMaximoBines).IsUnicode(false).IsRequired();
        constructor.HasIndex(d => d.Codigo).IsUnique();
        constructor.HasOne<Banco>().WithMany().HasForeignKey(d => d.BancoId).OnDelete(DeleteBehavior.Restrict);
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class PromocionConfiguracion : IEntityTypeConfiguration<Promocion>
{
    // Las listas de artículos, departamentos, categorías, marcas y sucursales se guardan como Ids separados por coma (SQL Server 2014 no lee JSON).
    private static readonly ValueConverter<List<int>, string> ConversorIds = new(
        lista => string.Join(",", lista),
        texto => string.IsNullOrEmpty(texto)
            ? new List<int>()
            : texto.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(parte => int.Parse(parte, System.Globalization.CultureInfo.InvariantCulture)).ToList());

    private static readonly ValueComparer<List<int>> ComparadorIds = new(
        (a, b) => a!.SequenceEqual(b!),
        lista => lista.Aggregate(0, (hash, id) => HashCode.Combine(hash, id)),
        lista => lista.ToList());

    public void Configure(EntityTypeBuilder<Promocion> constructor)
    {
        constructor.ToTable("Promociones");
        constructor.HasKey(p => p.Id);
        constructor.Property(p => p.Codigo).HasMaxLength(Promocion.LargoMaximoCodigo).IsRequired();
        constructor.Property(p => p.Nombre).HasMaxLength(Promocion.LargoMaximoNombre).IsRequired();
        constructor.Property(p => p.Valor).HasPrecision(18, 2);
        constructor.Property(p => p.CantidadMinima).HasPrecision(18, 3);
        constructor.Property(p => p.LimitePorCliente).HasPrecision(18, 3);
        constructor.Ignore(p => p.DescripcionCorta);
        constructor.Ignore(p => p.Articulos);
        constructor.Ignore(p => p.Departamentos);
        constructor.Ignore(p => p.Categorias);
        constructor.Ignore(p => p.Marcas);
        constructor.Ignore(p => p.Sucursales);

        foreach (var (campo, columna) in new[] { ("_articulos", "Articulos"), ("_departamentos", "Departamentos"), ("_categorias", "Categorias"), ("_marcas", "Marcas"), ("_sucursales", "Sucursales") })
        {
            var propiedad = constructor.Property<List<int>>(campo).HasColumnName(columna).HasConversion(ConversorIds, ComparadorIds).IsUnicode(false).IsRequired();
            propiedad.Metadata.SetMaxLength(null);
        }

        constructor.HasIndex(p => p.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class MotivoDescuentoConfiguracion : IEntityTypeConfiguration<MotivoDescuento>
{
    public void Configure(EntityTypeBuilder<MotivoDescuento> constructor)
    {
        constructor.ToTable("MotivosDescuento");
        constructor.HasKey(m => m.Id);
        constructor.Property(m => m.Nombre).HasMaxLength(MotivoDescuento.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(m => m.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class TopeDescuentoConfiguracion : IEntityTypeConfiguration<TopeDescuento>
{
    public void Configure(EntityTypeBuilder<TopeDescuento> constructor)
    {
        constructor.ToTable("TopesDescuento");
        constructor.HasKey(t => t.Id);
        constructor.Property(t => t.PorcentajeMaximo).HasPrecision(5, 2);
        constructor.Property(t => t.MontoMaximo).HasPrecision(18, 2);
        constructor.Ignore(t => t.EsGeneral);
        constructor.HasIndex(t => t.Codigo).IsUnique();
        constructor.HasOne<Departamento>().WithMany().HasForeignKey(t => t.DepartamentoId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Categoria>().WithMany().HasForeignKey(t => t.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Marca>().WithMany().HasForeignKey(t => t.MarcaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Articulo>().WithMany().HasForeignKey(t => t.ArticuloId).OnDelete(DeleteBehavior.Restrict);
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class SecuenciaEcfConfiguracion : IEntityTypeConfiguration<SecuenciaEcf>
{
    public void Configure(EntityTypeBuilder<SecuenciaEcf> constructor)
    {
        constructor.ToTable("SecuenciasEcf");
        constructor.HasKey(s => s.Id);

        // Una letra: la que la DGII use en ese momento. Se guarda con el rango para no tocar lo ya emitido si cambia.
        constructor.Property(s => s.Serie).HasMaxLength(1).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Ignore(s => s.Total);
        constructor.Ignore(s => s.Restantes);
        constructor.Ignore(s => s.PorcentajeRestante);
        constructor.HasIndex(s => new { s.TipoComprobante, s.Desde }).IsUnique();
        constructor.HasOne<Caja>().WithMany().HasForeignKey(s => s.CajaId).OnDelete(DeleteBehavior.Restrict);
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class MotivoSuspensionConfiguracion : IEntityTypeConfiguration<CgPos.Dominio.Turnos.MotivoSuspension>
{
    public void Configure(EntityTypeBuilder<CgPos.Dominio.Turnos.MotivoSuspension> constructor)
    {
        constructor.ToTable("MotivosSuspension");
        constructor.HasKey(m => m.Id);
        constructor.Property(m => m.Nombre).HasMaxLength(CgPos.Dominio.Turnos.MotivoSuspension.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(m => m.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class MotivoDevolucionConfiguracion : IEntityTypeConfiguration<MotivoDevolucion>
{
    public void Configure(EntityTypeBuilder<MotivoDevolucion> constructor)
    {
        constructor.ToTable("MotivosDevolucion");
        constructor.HasKey(m => m.Id);
        constructor.Property(m => m.Nombre).HasMaxLength(MotivoDevolucion.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(m => m.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class NivelFidelidadConfiguracion : IEntityTypeConfiguration<NivelFidelidad>
{
    public void Configure(EntityTypeBuilder<NivelFidelidad> constructor)
    {
        constructor.ToTable("NivelesFidelidad");
        constructor.HasKey(n => n.Id);
        constructor.Property(n => n.Nombre).HasMaxLength(NivelFidelidad.LargoMaximoNombre).IsRequired();
        constructor.Property(n => n.FactorAcumulacion).HasPrecision(9, 4);
        constructor.HasIndex(n => n.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class ReglaAcumulacionConfiguracion : IEntityTypeConfiguration<ReglaAcumulacion>
{
    public void Configure(EntityTypeBuilder<ReglaAcumulacion> constructor)
    {
        constructor.ToTable("ReglasAcumulacion");
        constructor.HasKey(r => r.Id);
        constructor.Property(r => r.Nombre).HasMaxLength(ReglaAcumulacion.LargoMaximoNombre).IsRequired();
        constructor.Property(r => r.MontoBase).HasPrecision(18, 2);
        constructor.Property(r => r.Puntos).HasPrecision(18, 4);
        constructor.HasIndex(r => r.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class MiembroFidelidadConfiguracion : IEntityTypeConfiguration<MiembroFidelidad>
{
    public void Configure(EntityTypeBuilder<MiembroFidelidad> constructor)
    {
        constructor.ToTable("MiembrosFidelidad");
        constructor.HasKey(m => m.Id);
        constructor.Property(m => m.Cedula).HasMaxLength(DocumentoIdentidad.LargoCedula).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(m => m.Nombre).HasMaxLength(MiembroFidelidad.LargoMaximoNombre).IsRequired();
        constructor.Property(m => m.Telefono).HasMaxLength(MiembroFidelidad.LargoMaximoTelefono);
        constructor.Property(m => m.Correo).HasMaxLength(MiembroFidelidad.LargoMaximoCorreo);
        constructor.HasIndex(m => m.Cedula).IsUnique();
        constructor.HasIndex(m => m.Nombre);
        constructor.HasOne<NivelFidelidad>().WithMany().HasForeignKey(m => m.NivelId).OnDelete(DeleteBehavior.Restrict);
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class RolCajaConfiguracion : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> constructor)
    {
        constructor.ToTable("RolesCaja");
        constructor.HasKey(r => r.Id);
        constructor.Property(r => r.Codigo).HasMaxLength(Rol.LargoMaximoCodigo).IsRequired();
        constructor.Property(r => r.Nombre).HasMaxLength(Rol.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(r => r.Codigo).IsUnique();
        constructor.HasMany(r => r.PermisosAsignados).WithOne().HasForeignKey(rp => rp.RolId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(r => r.PermisosAsignados).UsePropertyAccessMode(PropertyAccessMode.Field);
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class RolPermisoCajaConfiguracion : IEntityTypeConfiguration<RolPermiso>
{
    public void Configure(EntityTypeBuilder<RolPermiso> constructor)
    {
        constructor.ToTable("RolesCajaPermisos");
        constructor.HasKey(rp => new { rp.RolId, rp.PermisoCodigo });
        constructor.Property(rp => rp.PermisoCodigo).HasMaxLength(Permiso.LargoMaximoCodigo).IsUnicode(false);
    }
}

/// <summary>Usuarios de caja: entran con su código y su clave, que baja a las cajas solo como hash.</summary>
internal sealed class UsuarioCajaConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> constructor)
    {
        constructor.ToTable("UsuariosCaja");
        constructor.HasKey(u => u.Id);
        constructor.Property(u => u.Codigo).HasMaxLength(Usuario.LargoMaximoCodigo).IsRequired();
        constructor.Property(u => u.Nombre).HasMaxLength(Usuario.LargoMaximoNombre).IsRequired();
        constructor.Property(u => u.ClaveHash).HasMaxLength(Usuario.LargoMaximoHashClave).IsUnicode(false);
        constructor.HasIndex(u => u.Codigo).IsUnique();
        constructor.HasOne<Rol>().WithMany().HasForeignKey(u => u.RolId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasMany(u => u.CajasAsignadas).WithOne().HasForeignKey(uc => uc.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(u => u.CajasAsignadas).UsePropertyAccessMode(PropertyAccessMode.Field);

        // El bloqueo por intentos es de cada caja: el Central no lo lleva.
        constructor.Ignore(u => u.IntentosFallidos);
        constructor.Ignore(u => u.BloqueadoHasta);

        // El último acceso tampoco va en esta fila: lo lleva AccesosUsuarioCaja, para que un ingreso no cambie la versión
        // del usuario y lo reparta otra vez a todas las cajas.
        constructor.Ignore(u => u.UltimoIngresoEn);
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class AccesoUsuarioCajaConfiguracion : IEntityTypeConfiguration<AccesoUsuarioCaja>
{
    public void Configure(EntityTypeBuilder<AccesoUsuarioCaja> constructor)
    {
        constructor.ToTable("AccesosUsuarioCaja");
        constructor.HasKey(a => a.Id);

        // Uno por usuario: siempre el último, no el historial. El historial de ingresos está en la auditoría de cada caja.
        constructor.HasIndex(a => a.UsuarioId).IsUnique();
        constructor.HasOne<Usuario>().WithMany().HasForeignKey(a => a.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        constructor.HasOne<Caja>().WithMany().HasForeignKey(a => a.CajaId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class UsuarioCajaAsignadaConfiguracion : IEntityTypeConfiguration<UsuarioCaja>
{
    public void Configure(EntityTypeBuilder<UsuarioCaja> constructor)
    {
        constructor.ToTable("UsuariosCajaCajas");
        constructor.HasKey(uc => new { uc.UsuarioId, uc.CajaId });
        constructor.HasOne<Caja>().WithMany().HasForeignKey(uc => uc.CajaId).OnDelete(DeleteBehavior.Restrict);
    }
}
