using CgPos.Contratos.CargaInicial;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.CargaInicial;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Pruebas.Infraestructura;

/// <summary>Carga inicial contra SQL Server real. Cada prueba usa su propia empresa/datos para no interferir.</summary>
public class CargaInicialPruebas(BaseDatosPruebas baseDatos) : IClassFixture<BaseDatosPruebas>
{
    [SkippableFact]
    public async Task Aplicar_dos_veces_no_duplica_y_actualiza_los_cambios()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var datos = new DatosPrueba();

        var primera = await AplicarAsync(datos.Paquete());
        // Todo es nuevo salvo, quizá, la empresa: la comparten las pruebas de esta clase y otra pudo haberla creado antes.
        Assert.InRange(primera.Actualizados, 0, 1);
        Assert.True(primera.Creados >= 8);

        // Segunda carga: caja deshabilitada, rol sin un permiso, usuario sin la caja 02 y parámetro con otro valor.
        var cambiado = datos.Paquete(cajaDosHabilitada: false, permisosCajero: [CatalogoPermisos.RegistrarVenta], cajasCajero: [datos.CajaUno], intentosMaximos: "5");
        var segunda = await AplicarAsync(cambiado);
        Assert.Equal(0, segunda.Creados);
        Assert.Equal(primera.Creados + primera.Actualizados, segunda.Actualizados);

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();

        Assert.Equal(2, await contexto.Cajas.CountAsync(c => c.SucursalId == datos.Sucursal));
        Assert.False((await contexto.Cajas.SingleAsync(c => c.Id == datos.CajaDos)).Habilitada);

        var cajero = await contexto.Roles.Include(r => r.PermisosAsignados).SingleAsync(r => r.Id == datos.RolCajero);
        Assert.Equal(new[] { CatalogoPermisos.RegistrarVenta }, cajero.PermisosAsignados.Select(p => p.PermisoCodigo));

        var usuario = await contexto.Usuarios.Include(u => u.CajasAsignadas).SingleAsync(u => u.Id == datos.UsuarioCajero);
        Assert.True(usuario.PuedeOperarCaja(datos.CajaUno));
        Assert.False(usuario.PuedeOperarCaja(datos.CajaDos));

        Assert.Equal("5", (await contexto.Parametros.SingleAsync(p => p.Id == datos.Parametro)).Valor);
        Assert.Equal(CatalogoPermisos.Todos.Count, await contexto.Permisos.CountAsync());
    }

    [SkippableFact]
    public async Task Clave_se_guarda_como_hash_verificable_y_no_se_recalcula_si_no_cambia()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var datos = new DatosPrueba();

        await AplicarAsync(datos.Paquete());
        var hashInicial = await LeerClaveHashAsync(datos.UsuarioCajero);

        await AplicarAsync(datos.Paquete());
        Assert.Equal(hashInicial, await LeerClaveHashAsync(datos.UsuarioCajero));

        await AplicarAsync(datos.Paquete(claveCajero: "Nueva.9876"));
        var hashNuevo = await LeerClaveHashAsync(datos.UsuarioCajero);
        Assert.NotEqual(hashInicial, hashNuevo);

        var hash = baseDatos.Servicios!.GetRequiredService<IHashCredenciales>();
        Assert.True(hash.VerificarClave("Nueva.9876", hashNuevo!));
        Assert.False(hash.VerificarClave("Cajero.1111", hashNuevo!));
        Assert.DoesNotContain("Nueva.9876", hashNuevo);
    }

    [SkippableFact]
    public async Task Gerente_con_asterisco_recibe_todos_los_permisos()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var datos = new DatosPrueba();

        await AplicarAsync(datos.Paquete());

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        var gerente = await contexto.Roles.Include(r => r.PermisosAsignados).SingleAsync(r => r.Id == datos.RolGerente);
        Assert.Equal(CatalogoPermisos.Todos.Count, gerente.PermisosAsignados.Count);
    }

    [SkippableFact]
    public async Task Paquete_con_errores_se_rechaza_completo_sin_guardar_nada()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var datos = new DatosPrueba();
        var paquete = datos.Paquete();
        var rolInexistente = Guid.CreateVersion7();
        var conErrores = paquete with
        {
            Usuarios =
            [
                .. paquete.Usuarios!,
                new UsuarioCarga(Guid.CreateVersion7(), $"X{datos.Sufijo}", "Sin rol", rolInexistente, Clave: ""),
            ],
            Roles = [.. paquete.Roles!, new RolCarga(Guid.CreateVersion7(), $"MAL{datos.Sufijo}", "Rol malo", 1, ["Ventas.HacerMagia"])],
        };

        var error = await Assert.ThrowsAsync<CargaInicialInvalidaExcepcion>(() => AplicarAsync(conErrores));

        Assert.Contains(error.Errores, e => e.Contains("rol inexistente"));
        Assert.Contains(error.Errores, e => e.Contains("clave vacía"));
        Assert.Contains(error.Errores, e => e.Contains("Ventas.HacerMagia"));

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        Assert.False(await contexto.Sucursales.AnyAsync(s => s.Id == datos.Sucursal));
        Assert.False(await contexto.Usuarios.AnyAsync(u => u.Id == datos.UsuarioCajero));
    }

    [SkippableFact]
    public async Task Archivo_de_desarrollo_del_repositorio_es_valido()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);

        // Se aplica en una base aparte: este archivo tiene su propia empresa y no debe mezclarse con las otras pruebas.
        var aislada = new BaseDatosPruebas();
        await aislada.InitializeAsync();
        try
        {
            var ruta = Path.Combine(BuscarRaizRepositorio(), "datos", "carga-inicial.desarrollo.json");

            ResultadoCargaInicial primera, segunda;
            await using (var ambito = aislada.Servicios!.CreateAsyncScope())
                primera = await ambito.ServiceProvider.GetRequiredService<ICargaInicial>().AplicarDesdeArchivoAsync(ruta);
            await using (var ambito = aislada.Servicios!.CreateAsyncScope())
                segunda = await ambito.ServiceProvider.GetRequiredService<ICargaInicial>().AplicarDesdeArchivoAsync(ruta);

            Assert.Equal(3, primera.Usuarios);
            Assert.Equal(0, segunda.Creados);
        }
        finally
        {
            await aislada.DisposeAsync();
        }
    }

    private async Task<ResultadoCargaInicial> AplicarAsync(PaqueteCargaInicial paquete)
    {
        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        return await ambito.ServiceProvider.GetRequiredService<ICargaInicial>().AplicarAsync(paquete);
    }

    private async Task<string?> LeerClaveHashAsync(Guid usuarioId)
    {
        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        return await contexto.Usuarios.Where(u => u.Id == usuarioId).Select(u => u.ClaveHash).SingleAsync();
    }

    private static string BuscarRaizRepositorio()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CgPos.slnx")))
                return dir.FullName;
        }

        throw new InvalidOperationException("No se encontró la raíz del repositorio (CgPos.slnx).");
    }

    /// <summary>
    /// Ids y códigos únicos por prueba. Todas las pruebas de esta clase comparten la misma base,
    /// y una caja solo admite una empresa, por eso la empresa se reutiliza entre pruebas.
    /// </summary>
    private sealed class DatosPrueba
    {
        public static readonly Guid Empresa = Guid.CreateVersion7();

        public string Sufijo { get; } = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        public Guid Sucursal { get; } = Guid.CreateVersion7();
        public Guid CajaUno { get; } = Guid.CreateVersion7();
        public Guid CajaDos { get; } = Guid.CreateVersion7();
        public Guid RolCajero { get; } = Guid.CreateVersion7();
        public Guid RolGerente { get; } = Guid.CreateVersion7();
        public Guid UsuarioCajero { get; } = Guid.CreateVersion7();
        public Guid UsuarioGerente { get; } = Guid.CreateVersion7();
        public Guid Parametro { get; } = Guid.CreateVersion7();

        public PaqueteCargaInicial Paquete(
            bool cajaDosHabilitada = true,
            IReadOnlyList<string>? permisosCajero = null,
            IReadOnlyList<Guid>? cajasCajero = null,
            string intentosMaximos = "3",
            string claveCajero = "Cajero.1111") =>
            new(
                new EmpresaCarga(Empresa, "999000002", "Empresa de Pruebas SRL"),
                Sucursales: [new SucursalCarga(Sucursal, $"S{Sufijo}", "Sucursal de prueba")],
                Cajas:
                [
                    new CajaCarga(CajaUno, Sucursal, "01", "Caja 01"),
                    new CajaCarga(CajaDos, Sucursal, "02", "Caja 02", cajaDosHabilitada),
                ],
                Roles:
                [
                    new RolCarga(RolCajero, $"CAJ{Sufijo}", "Cajero", 1, permisosCajero ?? [CatalogoPermisos.RegistrarVenta, CatalogoPermisos.AbrirTurno]),
                    new RolCarga(RolGerente, $"GER{Sufijo}", "Gerente", 3, ["*"]),
                ],
                Usuarios:
                [
                    new UsuarioCarga(UsuarioCajero, $"C{Sufijo}", "Cajero Prueba", RolCajero, cajasCajero ?? [CajaUno, CajaDos], Clave: claveCajero),
                    new UsuarioCarga(UsuarioGerente, $"G{Sufijo}", "Gerente Prueba", RolGerente, [CajaUno], Clave: "Gerente.3333"),
                ],
                Parametros: [new ParametroCarga(Parametro, $"Prueba.IntentosMaximos{Sufijo}", intentosMaximos, CajaId: CajaUno)]);
    }
}
