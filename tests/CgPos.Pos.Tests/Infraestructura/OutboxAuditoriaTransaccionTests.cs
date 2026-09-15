using CgPos.Contracts.Sincronizacion;
using CgPos.Domain.Auditoria;
using CgPos.Pos.Application.Abstracciones;
using CgPos.Pos.Application.Sincronizacion;
using CgPos.Pos.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Tests.Infraestructura;

/// <summary>
/// Verifica contra SQL Server real que el mensaje del Outbox y la auditoría se guardan
/// en la misma transacción que la operación: o se guarda todo o no se guarda nada.
/// </summary>
public class OutboxAuditoriaTransaccionTests(BaseDatosPruebasFixture baseDatos) : IClassFixture<BaseDatosPruebasFixture>
{
    private const string TipoDocumento = "DocumentoPrueba";

    [SkippableFact]
    public async Task Outbox_y_auditoria_se_guardan_juntos_en_un_solo_SaveChanges()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var documentoId = Guid.CreateVersion7();
        Guid mensajeId;

        await using (var scope = baseDatos.Servicios!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
            var auditoria = scope.ServiceProvider.GetRequiredService<IAuditoria>();

            mensajeId = outbox.Encolar("Prueba.DocumentoEmitido", documentoId, new { Numero = "E320000000001", Total = 850.00m });
            auditoria.Registrar(new EntradaAuditoria(
                "Prueba.DocumentoEmitido", TipoDocumento, documentoId.ToString(),
                Detalle: new { Total = 850.00m },
                Usuario: new UsuarioAuditoria(Guid.CreateVersion7(), "Cajero Prueba")));

            // Encolar y registrar no guardan por sí mismos.
            Assert.Equal(0, await db.Outbox.CountAsync(m => m.AgregadoId == documentoId));

            await db.SaveChangesAsync();
        }

        await using (var scope = baseDatos.Servicios!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

            var mensaje = await db.Outbox.SingleAsync(m => m.Id == mensajeId);
            Assert.Equal(EstadoMensajeOutbox.Pendiente, mensaje.Estado);
            Assert.Equal(documentoId, mensaje.AgregadoId);
            Assert.Contains("\"numero\":\"E320000000001\"", mensaje.Contenido);
            Assert.Equal(MensajeOutbox.CalcularHash(mensaje.Contenido), mensaje.HashContenido);

            var registro = await db.Auditoria.SingleAsync(r => r.EntidadId == documentoId.ToString());
            Assert.Equal("Cajero Prueba", registro.UsuarioNombre);
            Assert.Contains("\"total\":850.00", registro.Detalle);
        }
    }

    [SkippableFact]
    public async Task Si_SaveChanges_falla_no_queda_ni_el_mensaje_ni_la_auditoria()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var documentoId = Guid.CreateVersion7();

        await using (var scope = baseDatos.Servicios!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
            var auditoria = scope.ServiceProvider.GetRequiredService<IAuditoria>();

            outbox.Encolar("Prueba.DocumentoEmitido", documentoId, new { Total = 1m });
            auditoria.Registrar(new EntradaAuditoria("Prueba.Valida", TipoDocumento, documentoId.ToString()));
            // Acción más larga que la columna: SQL Server rechaza el INSERT dentro del mismo SaveChanges.
            auditoria.Registrar(new EntradaAuditoria(new string('X', RegistroAuditoria.LargoMaximoAccion + 1), TipoDocumento, documentoId.ToString()));

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await AfirmarQueNoQuedoNadaAsync(documentoId);
    }

    [SkippableFact]
    public async Task Transaccion_explicita_revierte_lo_ya_guardado_si_un_paso_posterior_falla()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var documentoId = Guid.CreateVersion7();

        await using (var scope = baseDatos.Servicios!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
            var auditoria = scope.ServiceProvider.GetRequiredService<IAuditoria>();

            await using var transaccion = await db.Database.BeginTransactionAsync();

            outbox.Encolar("Prueba.DocumentoEmitido", documentoId, new { Total = 1m });
            await db.SaveChangesAsync(); // el mensaje ya se escribió dentro de la transacción

            auditoria.Registrar(new EntradaAuditoria(new string('X', RegistroAuditoria.LargoMaximoAccion + 1), TipoDocumento, documentoId.ToString()));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

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

        await using (var scope = baseDatos.Servicios!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
            var auditoria = scope.ServiceProvider.GetRequiredService<IAuditoria>();

            mensajeId = outbox.Encolar("Prueba.DocumentoEmitido", documentoId, new { Lineas = lineas });
            auditoria.Registrar(new EntradaAuditoria("Prueba.DocumentoEmitido", TipoDocumento, documentoId.ToString(), Detalle: new { Lineas = lineas }));
            contenidoOriginal = db.Outbox.Local.Single(m => m.Id == mensajeId).Contenido;

            await db.SaveChangesAsync();
        }

        Assert.True(contenidoOriginal.Length > 20_000);

        await using (var scope = baseDatos.Servicios!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

            var mensaje = await db.Outbox.SingleAsync(m => m.Id == mensajeId);
            Assert.Equal(contenidoOriginal, mensaje.Contenido);
            Assert.Equal(MensajeOutbox.CalcularHash(contenidoOriginal), mensaje.HashContenido);

            var registro = await db.Auditoria.SingleAsync(r => r.EntidadId == documentoId.ToString());
            Assert.Equal(contenidoOriginal.Length, registro.Detalle!.Length);
        }
    }

    private async Task AfirmarQueNoQuedoNadaAsync(Guid documentoId)
    {
        await using var scope = baseDatos.Servicios!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        Assert.Equal(0, await db.Outbox.CountAsync(m => m.AgregadoId == documentoId));
        Assert.Equal(0, await db.Auditoria.CountAsync(r => r.EntidadId == documentoId.ToString()));
    }
}
