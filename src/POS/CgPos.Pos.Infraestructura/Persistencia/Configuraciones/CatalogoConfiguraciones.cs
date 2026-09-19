using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Clientes;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

internal sealed class DepartamentoConfiguracion : IEntityTypeConfiguration<Departamento>
{
    public void Configure(EntityTypeBuilder<Departamento> constructor)
    {
        constructor.ToTable("Departamentos");
        constructor.HasKey(f => f.Id);
                constructor.Property(f => f.Nombre).HasMaxLength(Departamento.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(f => f.Codigo).IsUnique();
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
    }
}

internal sealed class ArticuloConfiguracion : IEntityTypeConfiguration<Articulo>
{
    public void Configure(EntityTypeBuilder<Articulo> constructor)
    {
        constructor.ToTable("Articulos");
        constructor.HasKey(a => a.Id);

        constructor.Property(a => a.Codigo).HasMaxLength(Articulo.LargoMaximoCodigo).IsRequired();
        constructor.Property(a => a.Descripcion).HasMaxLength(Articulo.LargoMaximoDescripcion).IsRequired();
        constructor.Property(a => a.Referencia).HasMaxLength(Articulo.LargoMaximoReferencia);
        constructor.Property(a => a.RutaImagen).HasMaxLength(Articulo.LargoMaximoRutaImagen);

        constructor.HasIndex(a => a.Codigo).IsUnique();
        constructor.HasIndex(a => a.Descripcion);
        constructor.HasIndex(a => a.DepartamentoId);

        constructor.HasOne<Departamento>().WithMany().HasForeignKey(a => a.DepartamentoId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Categoria>().WithMany().HasForeignKey(a => a.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Marca>().WithMany().HasForeignKey(a => a.MarcaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<UnidadMedida>().WithMany().HasForeignKey(a => a.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Impuesto>().WithMany().HasForeignKey(a => a.ImpuestoId).OnDelete(DeleteBehavior.Restrict);

        constructor.HasMany(a => a.Codigos).WithOne().HasForeignKey(c => c.ArticuloId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(a => a.Codigos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CodigoArticuloConfiguracion : IEntityTypeConfiguration<CodigoArticulo>
{
    public void Configure(EntityTypeBuilder<CodigoArticulo> constructor)
    {
        constructor.ToTable("CodigosArticulo");
        // Llave compuesta: permite mover un código de un artículo a otro en la misma transacción.
        constructor.HasKey(c => new { c.ArticuloId, c.Codigo });
        constructor.Property(c => c.Codigo).HasMaxLength(Articulo.LargoMaximoCodigo);
        // Un código identifica a un solo artículo.
        constructor.HasIndex(c => c.Codigo).IsUnique();
    }
}

internal sealed class PrecioArticuloConfiguracion : IEntityTypeConfiguration<PrecioArticulo>
{
    public void Configure(EntityTypeBuilder<PrecioArticulo> constructor)
    {
        constructor.ToTable("PreciosArticulo");
        constructor.HasKey(p => p.Id);
        constructor.Property(p => p.Origen).HasMaxLength(PrecioArticulo.LargoMaximoOrigen).IsRequired();
        constructor.Property(p => p.UsuarioNombre).HasMaxLength(PrecioArticulo.LargoMaximoUsuario);

        constructor.HasOne<Articulo>().WithMany().HasForeignKey(p => p.ArticuloId).OnDelete(DeleteBehavior.Cascade);
        constructor.HasIndex(p => new { p.ArticuloId, p.Lista, p.VigenteDesde });
    }
}

internal sealed class ClienteConfiguracion : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> constructor)
    {
        constructor.ToTable("Clientes");
        constructor.HasKey(c => c.Id);

        constructor.Property(c => c.Codigo).HasMaxLength(Cliente.LargoMaximoCodigo).IsRequired();
        constructor.HasIndex(c => c.Codigo).IsUnique();
        constructor.Property(c => c.Documento).HasMaxLength(Cliente.LargoMaximoDocumento).IsUnicode(false).IsRequired();
        constructor.Property(c => c.Nombre).HasMaxLength(Cliente.LargoMaximoNombre).IsRequired();
        constructor.Property(c => c.Telefono).HasMaxLength(Cliente.LargoMaximoTelefono);
        constructor.Property(c => c.Correo).HasMaxLength(Cliente.LargoMaximoCorreo);
        constructor.Property(c => c.Contacto).HasMaxLength(Cliente.LargoMaximoNombre);
        constructor.Property(c => c.TelefonoAlterno).HasMaxLength(Cliente.LargoMaximoTelefono);

        constructor.HasIndex(c => new { c.TipoDocumento, c.Documento }).IsUnique();
        constructor.HasIndex(c => c.Documento);
        constructor.HasIndex(c => c.Nombre);

        constructor.HasMany(c => c.Direcciones).WithOne().HasForeignKey(d => d.ClienteId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(c => c.Direcciones).UsePropertyAccessMode(PropertyAccessMode.Field);
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

        // La dirección se sincroniza por su alias dentro del cliente.
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
    }
}
