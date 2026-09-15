using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Pos.Infraestructura.Tickets;

internal static class ConsultasTicket
{
    /// <summary>Empresa, sucursal, caja, mensaje al pie configurado y zona horaria del equipo, para el encabezado de tickets y reportes de caja.</summary>
    public static async Task<EncabezadoTicket> EncabezadoTicketAsync(this ContextoDatosPos contexto, IParametros parametros, TimeZoneInfo zonaHoraria, Guid cajaId,
        CancellationToken cancelacion)
    {
        var datos = await (
                from caja in contexto.Cajas
                join sucursal in contexto.Sucursales on caja.SucursalId equals sucursal.Id
                join empresa in contexto.Empresas on sucursal.EmpresaId equals empresa.Id
                where caja.Id == cajaId
                select new { Empresa = empresa.NombreComercial ?? empresa.RazonSocial, empresa.Rnc, EmpresaDireccion = empresa.Direccion, empresa.Telefono,
                    Sucursal = sucursal.Nombre, SucursalDireccion = sucursal.Direccion, Caja = caja.Codigo })
            .AsNoTracking()
            .SingleAsync(cancelacion);

        var mensajePie = await parametros.ObtenerAsync(ClavesParametros.MensajePieTicket, cajaId, cancelacion);
        return new EncabezadoTicket(datos.Empresa, datos.Rnc, datos.EmpresaDireccion, datos.Telefono, datos.Sucursal, datos.SucursalDireccion, datos.Caja,
            string.IsNullOrWhiteSpace(mensajePie) ? null : mensajePie, zonaHoraria);
    }
}
