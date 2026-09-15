using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Auditoria;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Pruebas.Infraestructura;

/// <summary>
/// Verifica contra SQL Server real que el mensaje del BandejaSalida y la auditoría se guardan
/// en la misma transacción que la operación: o se guarda todo o no se guarda nada.
/// </summary>
public class BandejaSalidaAuditoriaTransaccionPruebas(BaseDatosPruebas baseDatos) : IClassFixture<BaseDatosPruebas>
{
    private const string TipoDocumento = "DocumentoPrueba";

    [SkippableFact]
    public async Task Bandeja_de_salida_y_auditoria_se_guardan_juntos_en_un_solo_SaveChanges()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var documentoId = Guid.CreateVersion7();
        Guid mensajeId;

        await using (var ambito = baseDatos.Servicios!.CreateAsyncScope())
        {
            var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
            var outbox = ambito.ServiceProvider.GetRequiredService<IBandejaSalida>();
            var auditoria = ambito.ServiceProvider.GetRequiredService<IAuditoria>();

            mensajeId = outbox.Encolar("Prueba.DocumentoEmitido", documentoId, new { Numero = "E320000000001", Total = 850.00m });
            auditoria.Registrar(new EntradaAuditoria(
                "Prueba.DocumentoEmitido", TipoDocumento, documentoId.ToString(),
                Detalle: new { Total = 850.00m },
                Usuario: new UsuarioAuditoria(Guid.CreateVersion7(), "Cajero Prueba")));

            // Encolar y registrar no guardan por sí mismos.
            Assert.Equal(0, await contexto.BandejaSalida.CountAsync(m => m.AgregadoId == documentoId));

            await contexto.SaveChangesAsync();
        }

        await using (var ambito = baseDatos.Servicios!.CreateAsyncScope())
        {
            var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();

            var mensaje = await contexto.BandejaSalida.SingleAsync(m => m.Id == mensajeId);
            Assert.Equal(EstadoMensajeSalida.Pendiente, mensaje.Estado);
            Assert.Equal(documentoId, mensaje.AgregadoId);
            Assert.Contains("\"numero\":\"E320000000001\"", mensaje.Contenido);
            Assert.Equal(MensajeSalida.CalcularHash(mensaje.Contenido), mensaje.HashContenido);

            var registro = await contexto.Auditoria.SingleAsync(r => r.EntidadId == documentoId.ToString());
            Assert.Equal("Cajero Prueba", registro.UsuarioNombre);
            Assert.Contains("\"total\":850.00", registro.Detalle);
        }
    }

    [SkippableFact]
    public async Task Si_SaveChanges_falla_no_queda_ni_el_mensaje_ni_la_auditoria()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var documentoId = Guid.CreateVersion7();

        await using (var ambito = baseDatos.Servicios!.CreateAsyncScope())
        {
            var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
            var outbox = ambito.ServiceProvider.GetRequiredService<IBandejaSalida>();
            var auditoria = ambito.ServiceProvider.GetRequiredService<IAuditoria>();

            outbox.Encolar("Prueba.DocumentoEmitido", documentoId, new { Total = 1m });
            auditoria.Registrar(new EntradaAuditoria("Prueba.Valida", TipoDocumento, documentoId.ToString()));
            // Acción más larga que la columna: SQL Server rechaza el INSERT dentro del mismo SaveChanges.
            auditoria.Registrar(new EntradaAuditoria(new string('X', RegistroAuditoria.LargoMaximoAccion + 1), TipoDocumento, documentoId.ToString()));

            await Assert.ThrowsAsync<DbUpdateException>(() => contexto.SaveChangesAsync());
        }

        await AfirmarQueNoQuedoNadaAsync(documentoId);
    }

    [SkippableFact]
    public async Task Transaccion_explicita_revierte_lo_ya_guardado_si_un_paso_posterior_falla()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var documentoId = Guid.CreateVersion7();

        await using (var ambito = baseDatos.Servicios!.CreateAsyncScope())
        {
            var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
            var outbox = ambito.ServiceProvider.GetRequiredService<IBandejaSalida>();
            var auditoria = ambito.ServiceProvider.GetRequiredService<IAuditoria>();

            await using var transaccion = await contexto.Database.BeginTransactionAsync();

            outbox.Encolar("Prueba.DocumentoEmitido", documentoId, new { Total = 1m });
            await contexto.SaveChangesAsync(); // el mensaje ya se escribió dentro de la transacción

            auditoria.Registrar(new EntradaAuditoria(new string('X', RegistroAuditoria.LargoMaximoAccion + 1), TipoDocumento, documentoId.ToString()));
            await Assert.ThrowsAsync<DbUpdateException>(() => contexto.SaveChangesAsync());

            await transaccion.RollbackAsync();
        }

        await AfirmarQueNoQuedoNadaAsync(documentoId);
    }

    [SkippableFact]
    public async Task Contenido_grande_se_guarda_completo()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var documentoId = Guid.CreateVersion7();
        // Una factura con muchas líneas genera un JSON muy superior a 4,000 caracteres.
        var lineas = Enumerable.Range(1, 400).Select(i => new { Linea = i, Codigo = $"7891114{i:000000}", Descripcion = $"Artículo de prueba número {i}", Total = 720.34m }).ToList();
        Guid mensajeId;
        string contenidoOriginal;

        await using (var ambito = baseDatos.Servicios!.CreateAsyncScope())
        {
            var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
            var outbox = ambito.ServiceProvider.GetRequiredService<IBandejaSalida>();
            var auditoria = ambito.ServiceProvider.GetRequiredService<IAuditoria>();

            mensajeId = outbox.Encolar("Prueba.DocumentoEmitido", documentoId, new { Lineas = lineas });
            auditoria.Registrar(new EntradaAuditoria("Prueba.DocumentoEmitido", TipoDocumento, documentoId.ToString(), Detalle: new { Lineas = lineas }));
            contenidoOriginal = contexto.BandejaSalida.Local.Single(m => m.Id == mensajeId).Contenido;

            await contexto.SaveChangesAsync();
        }

        Assert.True(contenidoOriginal.Length > 20_000);

        await using (var ambito = baseDatos.Servicios!.CreateAsyncScope())
        {
            var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();

            var mensaje = await contexto.BandejaSalida.SingleAsync(m => m.Id == mensajeId);
            Assert.Equal(contenidoOriginal, mensaje.Contenido);
            Assert.Equal(MensajeSalida.CalcularHash(contenidoOriginal), mensaje.HashContenido);

            var registro = await contexto.Auditoria.SingleAsync(r => r.EntidadId == documentoId.ToString());
            Assert.Equal(contenidoOriginal.Length, registro.Detalle!.Length);
        }
    }

    private async Task AfirmarQueNoQuedoNadaAsync(Guid documentoId)
    {
        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();

        Assert.Equal(0, await contexto.BandejaSalida.CountAsync(m => m.AgregadoId == documentoId));
        Assert.Equal(0, await contexto.Auditoria.CountAsync(r => r.EntidadId == documentoId.ToString()));
    }
}
