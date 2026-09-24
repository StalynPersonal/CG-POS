namespace CgPos.Dominio.Sincronizacion;

public enum TipoMaestro
{
    Moneda,
    Departamento,
    UnidadMedida,
    Impuesto,
    Articulo,
    Cliente,
    FormaPago,
    Banco,
    TipoTarjeta,
    Denominacion,
    Promocion,
    MotivoDescuento,
    TopeDescuento,
    TasaCambio,
    SecuenciaEcf,
    MotivoDevolucion,
    MotivoSuspension,
    NivelFidelidad,
    ReglaAcumulacion,
    MiembroFidelidad,

    /// <summary>Descuento del banco por BIN de tarjeta (RF-98).</summary>
    DescuentoTarjeta,

    /// <summary>Rol de los usuarios de caja (con su nivel y permisos del catálogo de la caja).</summary>
    RolCaja,

    /// <summary>Usuario de caja con el hash de su clave y las cajas que opera.</summary>
    UsuarioCaja,

    /// <summary>Categoría de artículos dentro de un departamento.</summary>
    Categoria,

    Marca,
}
