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
        var cambiado = datos.Paquete(cajaDosHabilitada: false, permisosCajero: [CatalogoPermisos.RegistrarVenta], soloCajaUno: true, intentosMaximos: "5");
        var segunda = await AplicarAsync(cambiado);
        Assert.Equal(0, segunda.Creados);
        Assert.Equal(primera.Creados + primera.Actualizados, segunda.Actualizados);

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();

        var sucursal = await contexto.Sucursales.SingleAsync(s => s.Codigo == datos.CodigoSucursal);
        var cajas = await contexto.Cajas.Where(c => c.SucursalId == sucursal.Id).ToDictionaryAsync(c => c.Codigo);
        Assert.Equal(2, cajas.Count);
        Assert.False(cajas[datos.CodigoCajaDos].Habilitada);

        var cajero = await contexto.Roles.Include(r => r.PermisosAsignados).SingleAsync(r => r.Codigo == datos.CodigoRolCajero);
        Assert.Equal(new[] { CatalogoPermisos.RegistrarVenta }, cajero.PermisosAsignados.Select(p => p.PermisoCodigo));

        var usuario = await contexto.Usuarios.Include(u => u.CajasAsignadas).SingleAsync(u => u.Codigo == datos.CodigoCajero);
        Assert.True(usuario.PuedeOperarCaja(cajas[datos.CodigoCajaUno].Id));
        Assert.False(usuario.PuedeOperarCaja(cajas[datos.CodigoCajaDos].Id));

        // El parámetro de caja se guarda con la caja; su clave y su ámbito lo identifican.
        Assert.Equal("5", (await contexto.Parametros.SingleAsync(p => p.Clave == datos.ClaveParametro && p.CajaId == cajas[datos.CodigoCajaUno].Id)).Valor);
        Assert.Equal(CatalogoPermisos.Todos.Count, await contexto.Permisos.CountAsync());
    }

    [SkippableFact]
    public async Task Clave_se_guarda_como_hash_verificable_y_no_se_recalcula_si_no_cambia()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var datos = new DatosPrueba();

        await AplicarAsync(datos.Paquete());
        var hashInicial = await LeerClaveHashAsync(datos.CodigoCajero);

        await AplicarAsync(datos.Paquete());
        Assert.Equal(hashInicial, await LeerClaveHashAsync(datos.CodigoCajero));

        await AplicarAsync(datos.Paquete(claveCajero: "Nueva.9876"));
        var hashNuevo = await LeerClaveHashAsync(datos.CodigoCajero);
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
        var gerente = await contexto.Roles.Include(r => r.PermisosAsignados).SingleAsync(r => r.Codigo == datos.CodigoRolGerente);
        Assert.Equal(CatalogoPermisos.Todos.Count, gerente.PermisosAsignados.Count);
    }

    [SkippableFact]
    public async Task Paquete_con_errores_se_rechaza_completo_sin_guardar_nada()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var datos = new DatosPrueba();
        var paquete = datos.Paquete();
        var conErrores = paquete with
        {
            Usuarios =
            [
                .. paquete.Usuarios!,
                new UsuarioCarga($"X{datos.Sufijo}", "Sin rol", $"NOEXISTE{datos.Sufijo}", Clave: ""),
            ],
            Roles = [.. paquete.Roles!, new RolCarga($"MAL{datos.Sufijo}", "Rol malo", 1, ["Ventas.HacerMagia"])],
        };

        var error = await Assert.ThrowsAsync<CargaInicialInvalidaExcepcion>(() => AplicarAsync(conErrores));

        Assert.Contains(error.Errores, e => e.Contains("rol inexistente"));
        Assert.Contains(error.Errores, e => e.Contains("clave vacía"));
        Assert.Contains(error.Errores, e => e.Contains("Ventas.HacerMagia"));

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        Assert.False(await contexto.Sucursales.AnyAsync(s => s.Codigo == datos.CodigoSucursal));
        Assert.False(await contexto.Usuarios.AnyAsync(u => u.Codigo == datos.CodigoCajero));
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
            var ruta = Path.Combine(BuscarRaizRepositorio(), "pruebas", "CgPos.Central.Pruebas", "Datos", "carga-inicial.pruebas.json");

            ResultadoCargaInicial primera, segunda;
            await using (var ambito = aislada.Servicios!.CreateAsyncScope())
                primera = await ambito.ServiceProvider.GetRequiredService<ICargaInicial>().AplicarDesdeArchivoAsync(ruta);
            await using (var ambito = aislada.Servicios!.CreateAsyncScope())
                segunda = await ambito.ServiceProvider.GetRequiredService<ICargaInicial>().AplicarDesdeArchivoAsync(ruta);

            // Cajero, supervisor, gerente y el de una caja dedicada a devoluciones.
            Assert.Equal(4, primera.Usuarios);
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

    private async Task<string?> LeerClaveHashAsync(string codigoUsuario)
    {
        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        return await contexto.Usuarios.Where(u => u.Codigo == codigoUsuario).Select(u => u.ClaveHash).SingleAsync();
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
    /// Códigos únicos por prueba. Todas las pruebas de esta clase comparten la misma base,
    /// y una caja solo admite una empresa, por eso la empresa se reutiliza entre pruebas.
    /// </summary>
    private sealed class DatosPrueba
    {
        private static int _pruebas;

        public string Sufijo { get; } = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        public string CodigoSucursal { get; } =
            Interlocked.Increment(ref _pruebas).ToString("00", System.Globalization.CultureInfo.InvariantCulture);

        public string CodigoCajaUno => "01";
        public string CodigoCajaDos => "02";
        public string CodigoRolCajero => $"CAJ{Sufijo}";
        public string CodigoRolGerente => $"GER{Sufijo}";
        public string CodigoCajero => $"C{Sufijo}";
        public string ClaveParametro => $"Prueba.IntentosMaximos{Sufijo}";

        public PaqueteCargaInicial Paquete(
            bool cajaDosHabilitada = true,
            IReadOnlyList<string>? permisosCajero = null,
            bool soloCajaUno = false,
            string intentosMaximos = "3",
            string claveCajero = "Cajero.1111") =>
            new(
                new EmpresaCarga("999000004", "Empresa de Pruebas SRL"),
                Sucursales: [new SucursalCarga(CodigoSucursal, "Sucursal de prueba")],
                Cajas:
                [
                    new CajaCarga(CodigoSucursal, CodigoCajaUno, "Caja 01", DireccionIp: "10.12.1.101"),
                    new CajaCarga(CodigoSucursal, CodigoCajaDos, "Caja 02", cajaDosHabilitada, "10.12.1.102"),
                ],
                Roles:
                [
                    new RolCarga(CodigoRolCajero, "Cajero", 1, permisosCajero ?? [CatalogoPermisos.RegistrarVenta, CatalogoPermisos.AbrirTurno]),
                    new RolCarga(CodigoRolGerente, "Gerente", 3, ["*"]),
                ],
                Usuarios:
                [
                    new UsuarioCarga(CodigoCajero, "Cajero Prueba", CodigoRolCajero,
                        soloCajaUno ? [new CajaReferencia(CodigoSucursal, CodigoCajaUno)] : [new CajaReferencia(CodigoSucursal, CodigoCajaUno), new CajaReferencia(CodigoSucursal, CodigoCajaDos)],
                        Clave: claveCajero),
                    new UsuarioCarga($"G{Sufijo}", "Gerente Prueba", CodigoRolGerente, [new CajaReferencia(CodigoSucursal, CodigoCajaUno)], Clave: "Gerente.3333"),
                ],
                Parametros: [new ParametroCarga(ClaveParametro, intentosMaximos, SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno)]);
    }
}
