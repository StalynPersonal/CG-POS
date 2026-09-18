using System.Net;
using System.Net.Http.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Seguridad;

namespace CgPos.Central.Pruebas.Api;

/// <summary>
/// Auditoría del Central (M02): cada cambio queda con su antes y su después, la consulta se filtra y se pagina, y solo la
/// ve quien tiene el permiso de consultarla.
/// </summary>
[Collection(ColeccionCentral.Nombre)]
public class ApiAuditoriaPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task Un_cambio_queda_con_su_antes_y_su_despues_y_la_consulta_se_filtra_y_pagina()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        // Se hace un cambio real: se renombra una sucursal recién creada.
        // El nombre lleva una marca única: la base de pruebas conserva lo de corridas anteriores.
        var codigo = Random.Shared.Next(70, 99).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        var marca = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var nombreOriginal = $"Sucursal auditoría {marca}";
        var nombreNuevo = $"Sucursal auditoría {marca} renombrada";
        var creacion = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/organizacion/sucursales",
            new SolicitudSucursal(codigo, nombreOriginal, "Calle 1, Santiago", "809-555-0000"));
        Assert.True(creacion.Cuerpo!.Exitosa, creacion.Cuerpo.Mensaje);
        var sucursalId = creacion.Cuerpo.Id!.Value;

        var cambio = await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/organizacion/sucursales/{sucursalId}",
            new SolicitudSucursal(codigo, nombreNuevo, "Calle 1, Santiago", "809-555-0000"));
        Assert.True(cambio.Cuerpo!.Exitosa, cambio.Cuerpo.Mensaje);

        // El movimiento se encuentra buscando por el nombre nuevo, y trae el nombre viejo y el nuevo.
        var pagina = await ConsultarAsync(cliente, admin, $"?buscar={Uri.EscapeDataString(nombreNuevo)}&soloConCambios=true&tamano=25");
        var registro = Assert.Single(pagina.Elementos, movimiento => movimiento.TipoEntidad == "Sucursal");
        Assert.False(string.IsNullOrWhiteSpace(registro.UsuarioNombre), "El movimiento debe decir quién lo hizo.");
        var entidad = Assert.Single(registro.Cambios);
        Assert.Equal("Modificado", entidad.Operacion);
        var nombre = Assert.Single(entidad.Campos, campo => campo.Campo == "Nombre");
        Assert.Equal(nombreOriginal, nombre.Antes);
        Assert.Equal(nombreNuevo, nombre.Despues);

        // La creación de la sucursal aparece con todos sus campos como valores nuevos.
        var creaciones = await ConsultarAsync(cliente, admin, $"?buscar={Uri.EscapeDataString(nombreOriginal)}&soloConCambios=true&tamano=50");
        Assert.Contains(creaciones.Elementos,
            movimiento => movimiento.Cambios.Any(cambios => cambios.Operacion == "Creado"
                && cambios.Campos.Any(campo => campo.Campo == "Nombre" && campo.Antes is null && campo.Despues == nombreOriginal)));

        // La paginación: una página de uno trae un solo movimiento y el total de todos los que cumplen el filtro.
        var primera = await ConsultarAsync(cliente, admin, "?soloConCambios=true&pagina=0&tamano=1");
        Assert.Single(primera.Elementos);
        Assert.True(primera.Total > 1, "Debe haber más movimientos que el de la página pedida.");

        var segunda = await ConsultarAsync(cliente, admin, "?soloConCambios=true&pagina=1&tamano=1");
        Assert.Single(segunda.Elementos);
        Assert.NotEqual(primera.Elementos[0].Id, segunda.Elementos[0].Id);

        // Un filtro que no cumple nadie devuelve la página vacía, no un error.
        var vacia = await ConsultarAsync(cliente, admin, "?accion=Accion.Que.No.Existe");
        Assert.Empty(vacia.Elementos);
        Assert.Equal(0, vacia.Total);

        // Los filtros de la pantalla se llenan con lo que hay en la auditoría.
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/manager/auditoria/opciones", admin));
        respuesta.EnsureSuccessStatusCode();
        var opciones = (await respuesta.Content.ReadFromJsonAsync<OpcionesAuditoria>(OpcionesJson.Predeterminadas))!;
        Assert.Contains("Sucursal", opciones.TiposEntidad);
        Assert.NotEmpty(opciones.Acciones);
    }

    [SkippableFact]
    public async Task Las_contrasenas_no_se_muestran_y_sin_el_permiso_no_se_consulta_la_auditoria()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        var codigo = $"AUD{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        await central.CrearUsuarioAsync(codigo, "Sin.Auditoria#2026", false, CatalogoPermisosCentral.ConsultarReportes);

        // El hash de la contraseña se audita como cambiado, pero nunca se muestra su contenido.
        var usuarios = await ConsultarAsync(cliente, admin, "?tipoEntidad=UsuarioCentral&soloConCambios=true&tamano=50");
        var campos = usuarios.Elementos.SelectMany(movimiento => movimiento.Cambios).SelectMany(cambio => cambio.Campos).ToList();
        Assert.All(campos.Where(campo => campo.Campo.Contains("Contrasena", StringComparison.OrdinalIgnoreCase)),
            campo => Assert.All(new[] { campo.Antes, campo.Despues }, valor => Assert.True(valor is null or "(oculto)", $"Se mostró '{valor}'.")));

        var token = (await CentralEnPruebas.IngresarAsync(cliente, codigo, "Sin.Auditoria#2026")).Cuerpo!.TokenAcceso;
        using var sinPermiso = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/manager/auditoria", token));
        Assert.Equal(HttpStatusCode.Forbidden, sinPermiso.StatusCode);
    }

    private static async Task<PaginaAuditoria> ConsultarAsync(HttpClient cliente, string token, string consulta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/manager/auditoria" + consulta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<PaginaAuditoria>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> EnviarAsync(
        HttpClient cliente, string token, HttpMethod metodo, string ruta, object? cuerpo = null)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(metodo, ruta, token, cuerpo));
        var datos = respuesta.Content.Headers.ContentType?.MediaType == "application/json"
            ? await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas)
            : null;
        return (respuesta.StatusCode, datos);
    }
}
