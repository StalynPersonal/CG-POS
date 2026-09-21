namespace CgPos.Contratos.Catalogo;

/// <summary>
/// Valida un paquete de maestros con las mismas reglas del dominio que aplica la caja al cargarlo, construyendo cada registro en memoria.
/// El Central lo usa antes de publicar: un maestro inválido detendría la sincronización de todas las cajas.
/// Las referencias entre maestros (departamento del artículo, moneda de la forma de pago…) las valida quien conoce lo ya publicado.
/// </summary>
public static class ValidacionMaestros
{
    public static IReadOnlyList<string> Validar(PaqueteMaestros paquete)
    {
        ArgumentNullException.ThrowIfNull(paquete);
        var errores = new List<string>();

        void Probar(string etiqueta, Action construir)
        {
            try
            {
                construir();
            }
            catch (Exception excepcion) when (excepcion is ArgumentException or InvalidOperationException)
            {
                errores.Add($"{etiqueta}: {MensajeError(excepcion)}");
            }
        }

        var r = ResolutorValidacion.Instancia;
        var ahora = DateTimeOffset.UnixEpoch;

        foreach (var d in paquete.Monedas ?? [])
            Probar($"Moneda '{d.Codigo}'", () => MapeoMaestros.Crear(d));

        foreach (var d in paquete.Departamentos ?? [])
            Probar($"Departamento {d.Codigo}", () => MapeoMaestros.Crear(d));

        foreach (var d in paquete.Categorias ?? [])
            Probar($"Categoría {d.Codigo}", () => MapeoMaestros.Crear(d, r));

        foreach (var d in paquete.Marcas ?? [])
            Probar($"Marca {d.Codigo}", () => MapeoMaestros.Crear(d));

        foreach (var d in paquete.UnidadesMedida ?? [])
            Probar($"Unidad de medida {d.Codigo}", () => MapeoMaestros.Crear(d));

        foreach (var d in paquete.Impuestos ?? [])
            Probar($"Impuesto '{d.Codigo}'", () => MapeoMaestros.Crear(d));

        foreach (var d in paquete.Articulos ?? [])
            Probar($"Artículo '{d.Codigo}'", () => MapeoMaestros.Crear(d, r));

        foreach (var d in paquete.Clientes ?? [])
            Probar($"Cliente '{d.Codigo}'", () => MapeoMaestros.Crear(d));

        foreach (var d in paquete.FormasPago ?? [])
            Probar($"Forma de pago '{d.Codigo}'", () => MapeoMaestros.Crear(d));

        foreach (var d in paquete.Bancos ?? [])
            Probar($"Banco '{d.Codigo}'", () => MapeoMaestros.Crear(d));

        foreach (var d in paquete.TiposTarjeta ?? [])
            Probar($"Tipo de tarjeta {d.Codigo}", () => MapeoMaestros.Crear(d));

        foreach (var d in paquete.Denominaciones ?? [])
            Probar($"Denominación {d.Moneda} {d.Valor}", () => MapeoMaestros.Crear(d));

        foreach (var d in paquete.Promociones ?? [])
            Probar($"Promoción '{d.Codigo}'", () => MapeoMaestros.Crear(d, r));

        foreach (var d in paquete.MotivosDescuento ?? [])
            Probar($"Motivo de descuento {d.Codigo}", () => MapeoMaestros.Crear(d));

        foreach (var d in paquete.TopesDescuento ?? [])
            Probar($"Tope de descuento {d.Codigo}", () => MapeoMaestros.Crear(d, r));

        foreach (var d in paquete.TasasCambio ?? [])
            Probar($"Tasa de cambio {d.Moneda}", () => MapeoMaestros.Crear(d));

        foreach (var d in paquete.SecuenciasEcf ?? [])
            Probar($"Rango de e-CF {d.TipoComprobante} {d.Desde}-{d.Hasta}", () => MapeoMaestros.Crear(d, r));

        foreach (var d in paquete.MotivosDevolucion ?? [])
            Probar($"Motivo de devolución {d.Codigo}", () => MapeoMaestros.Crear(d));

        foreach (var d in paquete.NivelesFidelidad ?? [])
            Probar($"Nivel de fidelidad {d.Codigo}", () => MapeoMaestros.Crear(d));

        foreach (var d in paquete.ReglasAcumulacion ?? [])
            Probar($"Regla de acumulación {d.Codigo}", () => MapeoMaestros.Crear(d, r));

        foreach (var d in paquete.MiembrosFidelidad ?? [])
            Probar($"Miembro de fidelidad '{d.Cedula}'", () => MapeoMaestros.Crear(d, r, ahora));

        foreach (var d in paquete.DescuentosTarjeta ?? [])
            Probar($"Descuento por tarjeta '{d.Codigo}'", () => MapeoMaestros.Crear(d, r));

        return errores;
    }

    /// <summary>Mensaje de una regla del dominio para mostrar al usuario, sin el "(Parameter 'x')" ni el valor que .NET agrega a los errores de argumento.</summary>
    public static string MensajeError(Exception excepcion)
    {
        ArgumentNullException.ThrowIfNull(excepcion);
        var mensaje = excepcion.Message;
        if (excepcion is ArgumentException { ParamName: { } parametro }
            && mensaje.IndexOf($" (Parameter '{parametro}')", StringComparison.Ordinal) is var corte and >= 0)
            mensaje = mensaje[..corte];

        return mensaje;
    }
}
