using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Fiscal;

namespace CgPos.Dominio.Clientes;

/// <summary>Cliente (RF-181): documento, datos de facturación y direcciones de envío.</summary>
public sealed class Cliente : Entidad
{
    public const int LargoMaximoCodigo = 20;
    public const int LargoMaximoDocumento = 20;
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoTelefono = 20;
    public const int LargoMaximoCorreo = 150;

    private readonly List<DireccionCliente> _direcciones = [];

    private Cliente()
    {
    }

    /// <summary>Código con que se sincroniza entre el Central y las cajas; lo asigna el Central y no cambia (el documento sí se puede corregir).</summary>
    public string Codigo { get; private set; } = string.Empty;

    public TipoDocumentoIdentidad TipoDocumento { get; private set; }

    /// <summary>Documento normalizado (sin guiones ni espacios).</summary>
    public string Documento { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;
    public TipoComprobante TipoComprobantePredeterminado { get; private set; } = TipoComprobante.FacturaConsumo;
    public bool ExoneradoItbis { get; private set; }

    /// <summary>Cliente gubernamental al que se aplica retención (RF-37).</summary>
    public bool AplicaRetencion { get; private set; }

    /// <summary>Lista de precio habitual del cliente; aplicar la lista por mayor requiere clave de supervisor (RF-77).</summary>
    public ListaPrecio ListaPrecioPredeterminada { get; private set; } = ListaPrecio.Detalle;

    public string? Telefono { get; private set; }
    public string? Correo { get; private set; }
    public bool Activo { get; private set; } = true;

    public IReadOnlyCollection<DireccionCliente> Direcciones => _direcciones;

    public static Cliente Crear(string codigo, TipoDocumentoIdentidad tipoDocumento, string documento, string nombre, Guid? id = null)
    {
        var cliente = new Cliente
        {
            Id = id ?? Guid.CreateVersion7(),
            Codigo = Validar.Texto(codigo, "Código del cliente", LargoMaximoCodigo).ToUpperInvariant(),
            TipoDocumento = tipoDocumento,
            Documento = ValidarDocumento(tipoDocumento, documento),
        };
        cliente.ActualizarContacto(nombre, null, null);
        return cliente;
    }

    /// <summary>
    /// Corrige el tipo y el número de documento (por ejemplo, uno mal digitado). Las facturas y demás documentos ya emitidos no cambian: guardan los datos
    /// del cliente con que se emitieron.
    /// </summary>
    public void CorregirDocumento(TipoDocumentoIdentidad tipoDocumento, string documento)
    {
        Documento = ValidarDocumento(tipoDocumento, documento);
        TipoDocumento = tipoDocumento;
    }

    public void ActualizarContacto(string nombre, string? telefono, string? correo)
    {
        Nombre = Validar.Texto(nombre, "Nombre del cliente", LargoMaximoNombre);
        Telefono = Validar.TextoOpcional(telefono, "Teléfono", LargoMaximoTelefono);
        Correo = Validar.TextoOpcional(correo, "Correo", LargoMaximoCorreo);
    }

    public void ConfigurarFacturacion(TipoComprobante tipoComprobante, bool exoneradoItbis, bool aplicaRetencion, ListaPrecio listaPrecio)
    {
        if (!Enum.IsDefined(tipoComprobante))
            throw new ArgumentOutOfRangeException(nameof(tipoComprobante), tipoComprobante, "Tipo de comprobante no válido.");

        TipoComprobantePredeterminado = tipoComprobante;
        ExoneradoItbis = exoneradoItbis;
        AplicaRetencion = aplicaRetencion;
        ListaPrecioPredeterminada = listaPrecio;
    }

    public DireccionCliente AgregarDireccion(string alias, string direccion, string? sector = null, string? ciudad = null,
        string? referencia = null, string? telefono = null, bool esPrincipal = false, Guid? id = null)
    {
        var nueva = DireccionCliente.Crear(Id, alias, direccion, sector, ciudad, referencia, telefono, id);
        _direcciones.Add(nueva);

        if (esPrincipal || _direcciones.Count == 1)
            MarcarPrincipal(nueva.Id);

        return nueva;
    }

    public void ActualizarDireccion(Guid direccionId, string alias, string direccion, string? sector, string? ciudad, string? referencia, string? telefono)
    {
        var existente = _direcciones.SingleOrDefault(d => d.Id == direccionId)
            ?? throw new ArgumentException("La dirección no pertenece al cliente.", nameof(direccionId));

        existente.Actualizar(alias, direccion, sector, ciudad, referencia, telefono);
    }

    public void QuitarDireccion(Guid direccionId)
    {
        var eraPrincipal = _direcciones.Any(d => d.Id == direccionId && d.EsPrincipal);
        _direcciones.RemoveAll(d => d.Id == direccionId);

        if (eraPrincipal && _direcciones.Count > 0)
            MarcarPrincipal(_direcciones[0].Id);
    }

    public void MarcarPrincipal(Guid direccionId)
    {
        if (_direcciones.All(d => d.Id != direccionId))
            throw new ArgumentException("La dirección no pertenece al cliente.", nameof(direccionId));

        foreach (var direccion in _direcciones)
            direccion.EstablecerPrincipal(direccion.Id == direccionId);
    }

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;

    private static string ValidarDocumento(TipoDocumentoIdentidad tipo, string documento)
    {
        var normalizado = DocumentoIdentidad.Normalizar(documento);

        var valido = tipo switch
        {
            TipoDocumentoIdentidad.Rnc => normalizado.Length == DocumentoIdentidad.LargoRnc && normalizado.All(char.IsAsciiDigit),
            TipoDocumentoIdentidad.Cedula => normalizado.Length == DocumentoIdentidad.LargoCedula && normalizado.All(char.IsAsciiDigit),
            TipoDocumentoIdentidad.Pasaporte => normalizado.Length is >= 5 and <= LargoMaximoDocumento,
            _ => false,
        };

        return valido
            ? normalizado
            : throw new ArgumentException($"El documento '{documento}' no tiene formato de {tipo switch
            {
                TipoDocumentoIdentidad.Rnc => "RNC (9 dígitos)",
                TipoDocumentoIdentidad.Cedula => "cédula (11 dígitos)",
                _ => "pasaporte",
            }}.", nameof(documento));
    }
}

public sealed class DireccionCliente : Entidad
{
    public const int LargoMaximoAlias = 50;
    public const int LargoMaximoDireccion = 250;
    public const int LargoMaximoLugar = 100;

    private DireccionCliente()
    {
    }

    public Guid ClienteId { get; private set; }
    public string Alias { get; private set; } = string.Empty;
    public string Direccion { get; private set; } = string.Empty;
    public string? Sector { get; private set; }
    public string? Ciudad { get; private set; }
    public string? Referencia { get; private set; }
    public string? Telefono { get; private set; }
    public bool EsPrincipal { get; private set; }

    internal static DireccionCliente Crear(Guid clienteId, string alias, string direccion, string? sector, string? ciudad,
        string? referencia, string? telefono, Guid? id)
    {
        var nueva = new DireccionCliente { Id = id ?? Guid.CreateVersion7(), ClienteId = clienteId };
        nueva.Actualizar(alias, direccion, sector, ciudad, referencia, telefono);
        return nueva;
    }

    internal void Actualizar(string alias, string direccion, string? sector, string? ciudad, string? referencia, string? telefono)
    {
        // Se valida todo antes de asignar, para no dejar la dirección a medio actualizar.
        var aliasValido = Validar.Texto(alias, "Alias de la dirección", LargoMaximoAlias);
        var direccionValida = Validar.Texto(direccion, "Dirección", LargoMaximoDireccion);
        var sectorValido = Validar.TextoOpcional(sector, "Sector", LargoMaximoLugar);
        var ciudadValida = Validar.TextoOpcional(ciudad, "Ciudad", LargoMaximoLugar);
        var referenciaValida = Validar.TextoOpcional(referencia, "Referencia", LargoMaximoDireccion);
        var telefonoValido = Validar.TextoOpcional(telefono, "Teléfono", Cliente.LargoMaximoTelefono);

        Alias = aliasValido;
        Direccion = direccionValida;
        Sector = sectorValido;
        Ciudad = ciudadValida;
        Referencia = referenciaValida;
        Telefono = telefonoValido;
    }

    internal void EstablecerPrincipal(bool esPrincipal) => EsPrincipal = esPrincipal;
}
