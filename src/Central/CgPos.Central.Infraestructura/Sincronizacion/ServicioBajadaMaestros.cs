using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Central.Infraestructura.Maestros;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.CargaInicial;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;
using CgPos.Dominio.Comun;

namespace CgPos.Central.Infraestructura.Sincronizacion;

internal sealed class ServicioBajadaMaestros(ContextoDatosCentral contexto, TimeProvider reloj) : IServicioBajadaMaestros
{
    public async Task<PaqueteBajadaMaestros> ObtenerAsync(CajaRemitente caja, long desde, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(caja);
        desde = Math.Max(0, desde);

        // Hasta la versión más alta ya confirmada: una transacción aún abierta con una versión menor no queda saltada.
        var hasta = Math.Max(desde, await contexto.Database
            .SqlQueryRaw<long>("SELECT CAST(MIN_ACTIVE_ROWVERSION() AS bigint) - 1 AS [Value]")
            .SingleAsync(cancelacion));

        var resolutor = new ResolutorCodigosCentral(contexto);
        await resolutor.PrepararAsync(cancelacion);

        PaqueteCargaInicial? organizacion = null;
        PaqueteMaestros? maestros = null;
        List<EstadoDgiiCarga>? estadosDgii = null;

        if (hasta > desde)
        {
            // Bajan todas las sucursales de la empresa (son pocas y es solo su ficha): la caja las necesita para el retiro en
            // otra tienda: el cliente puede pasar a buscar por cualquiera. La terminal sí es solo la suya.
            var sucursales = await EnRango(contexto.Sucursales.AsNoTracking(), desde, hasta).ToListAsync(cancelacion);
            var cajas = await EnRango(contexto.Cajas.AsNoTracking().Where(c => c.Id == caja.CajaId), desde, hasta).ToListAsync(cancelacion);
            var parametros = await EnRango(ParametrosDeCaja(caja), desde, hasta).ToListAsync(cancelacion);
            var empresaCambio = await EnRango(contexto.Empresas.AsNoTracking(), desde, hasta).AnyAsync(cancelacion);
            var roles = (await TablasMaestros.RolesCaja.CambiosAsync(contexto, resolutor, desde, hasta, cancelacion)).Cast<RolCarga>().ToList();
            var usuarios = (await TablasMaestros.UsuariosCaja.CambiosAsync(contexto, resolutor, desde, hasta, cancelacion)).Cast<UsuarioCarga>().ToList();

            if ((empresaCambio || sucursales.Count > 0 || cajas.Count > 0 || parametros.Count > 0 || roles.Count > 0 || usuarios.Count > 0)
                && await contexto.Empresas.AsNoTracking().SingleOrDefaultAsync(cancelacion) is { } empresa)
            {
                organizacion = new PaqueteCargaInicial(
                    new EmpresaCarga(empresa.Rnc, empresa.RazonSocial, empresa.NombreComercial, empresa.Direccion, empresa.Telefono),
                    sucursales.Select(s => new SucursalCarga(s.Codigo, s.Nombre, s.Direccion, s.Telefono, s.Activa)).ToList(),
                    cajas.Select(c => new CajaCarga(resolutor.CodigoSucursal(c.SucursalId), c.Codigo, c.Nombre, c.Habilitada, c.DireccionIp)).ToList(),
                    roles,
                    usuarios,
                    parametros.Select(p => Carga(p, resolutor)).ToList());
            }

            maestros = await MaestrosAsync(caja, resolutor, desde, hasta, cancelacion);

            // Resultados de la DGII de los e-CF de esta caja (RF-223): la caja los aplica a sus documentos.
            estadosDgii = await EnRango(contexto.ComprobantesRecibidos.AsNoTracking(), desde, hasta)
                .Where(c => c.CajaId == caja.CajaId && c.EstadoDgii != EstadoEnvioDgii.Pendiente)
                .Select(c => new EstadoDgiiCarga(c.Encf, c.EstadoDgii, c.EstadoDgiiEn, c.MensajeDgii, c.TrackId))
                .ToListAsync(cancelacion);
        }

        // Todos los parámetros vigentes de esta caja: así la caja borra los que se eliminaron en el Central, que por definición no viajan en el rango.
        var vigentes = (await ParametrosDeCaja(caja).ToListAsync(cancelacion))
            .Select(p => Carga(p, resolutor))
            .Select(p => new ParametroReferencia(p.Clave, p.SucursalCodigo, p.CajaCodigo))
            .ToList();

        var estado = await contexto.EstadosSincronizacionCaja.SingleOrDefaultAsync(e => e.CajaId == caja.CajaId, cancelacion);
        if (estado is null)
        {
            estado = EstadoSincronizacionCaja.Crear(caja.CajaId);
            contexto.EstadosSincronizacionCaja.Add(estado);
        }

        estado.RegistrarDescarga(reloj.Ahora(), desde, hasta);
        await contexto.SaveChangesAsync(cancelacion);

        return new PaqueteBajadaMaestros(desde, hasta, organizacion, maestros, estadosDgii is { Count: > 0 } ? estadosDgii : null, vigentes);
    }

    /// <summary>Catálogo, precios, promociones, fidelidad… cambiados en el rango; los rangos de e-CF solo los de esta caja. Nulo si nada cambió.</summary>
    private async Task<PaqueteMaestros?> MaestrosAsync(CajaRemitente caja, ResolutorCodigosCentral resolutor, long desde, long hasta, CancellationToken cancelacion)
    {
        async Task<List<T>?> Lista<T>(TablaMaestro tabla)
        {
            var cambios = (await tabla.CambiosAsync(contexto, resolutor, desde, hasta, cancelacion)).Cast<T>().ToList();
            return cambios.Count == 0 ? null : cambios;
        }

        var (sucursal, codigoCaja) = resolutor.CodigoCaja(caja.CajaId);
        var secuencias = (await Lista<SecuenciaEcfCarga>(TablasMaestros.SecuenciasEcf))?.Where(s => s.SucursalCodigo == sucursal && s.CajaCodigo == codigoCaja).ToList();

        // Un rango agotado no baja: la caja lo borra al terminarlo, y volver a mandárselo solo conseguiría que lo recreara
        // con la cuenta del Central y empezara a repetir números ya emitidos.
        if (secuencias is { Count: > 0 })
        {
            var agotados = await contexto.SecuenciasEcf.AsNoTracking()
                .Where(s => s.CajaId == caja.CajaId && s.Ultimo >= s.Hasta)
                .Select(s => new { s.TipoComprobante, s.Desde })
                .ToListAsync(cancelacion);

            secuencias = secuencias
                .Where(s => !agotados.Any(a => a.TipoComprobante == s.TipoComprobante && a.Desde == s.Desde))
                .ToList();
        }


        var paquete = new PaqueteMaestros(
            Departamentos: await Lista<DepartamentoCarga>(TablasMaestros.Departamentos),
            UnidadesMedida: await Lista<UnidadMedidaCarga>(TablasMaestros.UnidadesMedida),
            Impuestos: await Lista<ImpuestoCarga>(TablasMaestros.Impuestos),
            Articulos: await Lista<ArticuloCarga>(TablasMaestros.Articulos),
            Clientes: await Lista<ClienteCarga>(TablasMaestros.Clientes),
            FormasPago: await Lista<FormaPagoCarga>(TablasMaestros.FormasPago),
            Bancos: await Lista<BancoCarga>(TablasMaestros.Bancos),
            TiposTarjeta: await Lista<TipoTarjetaCarga>(TablasMaestros.TiposTarjeta),
            Denominaciones: await Lista<DenominacionCarga>(TablasMaestros.Denominaciones),
            Promociones: await Lista<PromocionCarga>(TablasMaestros.Promociones),
            MotivosDescuento: await Lista<MotivoDescuentoCarga>(TablasMaestros.MotivosDescuento),
            TopesDescuento: await Lista<TopeDescuentoCarga>(TablasMaestros.TopesDescuento),
            TasasCambio: await Lista<TasaCambioCarga>(TablasMaestros.TasasCambio),
            SecuenciasEcf: secuencias is { Count: > 0 } ? secuencias : null,
            MotivosDevolucion: await Lista<MotivoDevolucionCarga>(TablasMaestros.MotivosDevolucion),
            MotivosSuspension: await Lista<MotivoSuspensionCarga>(TablasMaestros.MotivosSuspension),
            Monedas: await Lista<MonedaCarga>(TablasMaestros.Monedas),
            NivelesFidelidad: await Lista<NivelFidelidadCarga>(TablasMaestros.NivelesFidelidad),
            ReglasAcumulacion: await Lista<ReglaAcumulacionCarga>(TablasMaestros.ReglasAcumulacion),
            MiembrosFidelidad: await Lista<MiembroFidelidadCarga>(TablasMaestros.MiembrosFidelidad),
            DescuentosTarjeta: await Lista<DescuentoTarjetaCarga>(TablasMaestros.DescuentosTarjeta),
            Categorias: await Lista<CategoriaCarga>(TablasMaestros.Categorias),
            Marcas: await Lista<MarcaCarga>(TablasMaestros.Marcas));

        return paquete == new PaqueteMaestros() ? null : paquete;
    }

    /// <summary>Parámetros que rigen a la caja: generales, de su sucursal y de ella (los del Central no bajan).</summary>
    private IQueryable<Parametro> ParametrosDeCaja(CajaRemitente caja) =>
        contexto.Parametros.AsNoTracking()
            .Where(p => !p.Clave.StartsWith(PublicadorMaestros.PrefijoParametrosCentral)
                && ((p.SucursalId == null && p.CajaId == null) || p.SucursalId == caja.SucursalId || p.CajaId == caja.CajaId));

    private static ParametroCarga Carga(Parametro parametro, ResolutorCodigosCentral resolutor)
    {
        if (parametro.CajaId is { } cajaId)
        {
            var (sucursal, caja) = resolutor.CodigoCaja(cajaId);
            return new ParametroCarga(parametro.Clave, parametro.Valor, parametro.Descripcion, sucursal, caja);
        }

        return new ParametroCarga(parametro.Clave, parametro.Valor, parametro.Descripcion, parametro.SucursalId is { } sucursalId ? resolutor.CodigoSucursal(sucursalId) : null);
    }

    private static IQueryable<T> EnRango<T>(IQueryable<T> consulta, long desde, long hasta) where T : class =>
        consulta.Where(e => EF.Property<long>(e, ContextoDatosCentral.ColumnaVersion) > desde && EF.Property<long>(e, ContextoDatosCentral.ColumnaVersion) <= hasta);
}
