using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Pos.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "EntityFrameworkHiLoSequence",
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "Almacenes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SucursalId = table.Column<int>(type: "int", nullable: false),
                    Direccion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Almacenes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    OcurridoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TipoEntidad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntidadId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Detalle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AutorizadoPorId = table.Column<int>(type: "int", nullable: true),
                    AutorizadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutorizacionesOtorgadas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Permiso = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    CajaId = table.Column<int>(type: "int", nullable: false),
                    SolicitanteId = table.Column<int>(type: "int", nullable: false),
                    SolicitanteNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SupervisorId = table.Column<int>(type: "int", nullable: false),
                    SupervisorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ConcedidaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    VenceEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    UsadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    UsadaEnTipoEntidad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UsadaEnEntidadId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutorizacionesOtorgadas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bancos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RutaLogo = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bancos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BandejaSalida",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoMensaje = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Referencia = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HashContenido = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    ProximoIntentoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    EnviadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    ConfirmadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    UltimoError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BandejaSalida", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TipoDocumento = table.Column<int>(type: "int", nullable: false),
                    Documento = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TipoComprobantePredeterminado = table.Column<int>(type: "int", nullable: false),
                    ExoneradoItbis = table.Column<bool>(type: "bit", nullable: false),
                    AplicaRetencion = table.Column<bool>(type: "bit", nullable: false),
                    ListaPrecioPredeterminada = table.Column<int>(type: "int", nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContribuyentesDgii",
                columns: table => new
                {
                    Documento = table.Column<string>(type: "varchar(11)", unicode: false, maxLength: 11, nullable: false),
                    RazonSocial = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    NombreComercial = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RegimenPago = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContribuyentesDgii", x => x.Documento);
                });

            migrationBuilder.CreateTable(
                name: "Denominaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Moneda = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Denominaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PermiteDescuentoManual = table.Column<bool>(type: "bit", nullable: false),
                    EsNoCodificada = table.Column<bool>(type: "bit", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departamentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DescuentosTarjeta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Bines = table.Column<string>(type: "varchar(400)", unicode: false, maxLength: 400, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MontoMinimo = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MontoMaximo = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    BancoId = table.Column<int>(type: "int", nullable: true),
                    VigenteDesde = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    VigenteHasta = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Dias = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DescuentosTarjeta", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentosElectronicos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    VentaId = table.Column<int>(type: "int", nullable: false),
                    CajaId = table.Column<int>(type: "int", nullable: false),
                    TipoComprobante = table.Column<int>(type: "int", nullable: false),
                    Encf = table.Column<string>(type: "varchar(13)", unicode: false, maxLength: 13, nullable: false),
                    FechaEmision = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    FechaFirma = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    CodigoSeguridad = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    MontoTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HashXml = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    RutaXml = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    UrlTimbre = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    EstadoActualizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    MensajeEstado = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentosElectronicos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Empresas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Rnc = table.Column<string>(type: "char(9)", unicode: false, fixedLength: true, maxLength: 9, nullable: false),
                    RazonSocial = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NombreComercial = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FormasPago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Moneda = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    AbreGaveta = table.Column<bool>(type: "bit", nullable: false),
                    PermiteDevuelta = table.Column<bool>(type: "bit", nullable: false),
                    RequiereReferencia = table.Column<bool>(type: "bit", nullable: false),
                    RequiereBanco = table.Column<bool>(type: "bit", nullable: false),
                    PermiteComprobanteFiscal = table.Column<bool>(type: "bit", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormasPago", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Impuestos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Porcentaje = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IndicadorFacturacion = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Impuestos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Marcas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marcas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarcasSincronizacion",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Valor = table.Column<long>(type: "bigint", nullable: false),
                    Texto = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    ActualizadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarcasSincronizacion", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "MiembrosFidelidad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Cedula = table.Column<string>(type: "char(11)", unicode: false, fixedLength: true, maxLength: 11, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NivelId = table.Column<int>(type: "int", nullable: true),
                    SaldoSincronizado = table.Column<int>(type: "int", nullable: false),
                    SaldoSincronizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    PuntosPorVencer = table.Column<int>(type: "int", nullable: false),
                    ProximoVencimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    InscritoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    InscritoEnCaja = table.Column<bool>(type: "bit", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MiembrosFidelidad", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Monedas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Simbolo = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Monedas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MotivosDescuento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotivosDescuento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MotivosDevolucion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotivosDevolucion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NivelesFidelidad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    FactorAcumulacion = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NivelesFidelidad", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperacionesTerminal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CajaId = table.Column<int>(type: "int", nullable: false),
                    TurnoId = table.Column<int>(type: "int", nullable: false),
                    VentaId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Aprobacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UltimosDigitos = table.Column<string>(type: "varchar(4)", unicode: false, maxLength: 4, nullable: true),
                    Marca = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Mensaje = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReferenciaTerminal = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    OperacionAnuladaId = table.Column<int>(type: "int", nullable: true),
                    UsadaEnCobro = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacionesTerminal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendientesEntrega",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    VentaId = table.Column<int>(type: "int", nullable: false),
                    VentaNumero = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    SucursalId = table.Column<int>(type: "int", nullable: false),
                    CajaId = table.Column<int>(type: "int", nullable: false),
                    Metodo = table.Column<int>(type: "int", nullable: false),
                    AlmacenId = table.Column<int>(type: "int", nullable: true),
                    AlmacenNombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Sector = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ciudad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Referencia = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Transportista = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CostoEnvio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FechaComprometida = table.Column<DateOnly>(type: "date", nullable: true),
                    Comentario = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ClienteDocumento = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ClienteNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    VendidoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    AutorizadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    ActualizadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendientesEntrega", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permisos",
                columns: table => new
                {
                    Codigo = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Modulo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permisos", x => x.Codigo);
                });

            migrationBuilder.CreateTable(
                name: "Promociones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CantidadLleva = table.Column<int>(type: "int", nullable: true),
                    CantidadPaga = table.Column<int>(type: "int", nullable: true),
                    CantidadMinima = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    LimitePorCliente = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    VigenteDesde = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    VigenteHasta = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Dias = table.Column<int>(type: "int", nullable: false),
                    HoraDesde = table.Column<TimeOnly>(type: "time", nullable: true),
                    HoraHasta = table.Column<TimeOnly>(type: "time", nullable: true),
                    SoloFidelidad = table.Column<bool>(type: "bit", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    Articulos = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false),
                    Categorias = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false),
                    Departamentos = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false),
                    Marcas = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false),
                    Sucursales = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promociones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReglasAcumulacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    ReferenciaId = table.Column<int>(type: "int", nullable: true),
                    DiaSemana = table.Column<int>(type: "int", nullable: true),
                    MontoBase = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Puntos = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VigenteDesde = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    VigenteHasta = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReglasAcumulacion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Nivel = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecuenciasCaja",
                columns: table => new
                {
                    CajaId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Ultimo = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecuenciasCaja", x => new { x.CajaId, x.Tipo });
                });

            migrationBuilder.CreateTable(
                name: "SecuenciasEcf",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CajaId = table.Column<int>(type: "int", nullable: false),
                    TipoComprobante = table.Column<int>(type: "int", nullable: false),
                    Desde = table.Column<long>(type: "bigint", nullable: false),
                    Hasta = table.Column<long>(type: "bigint", nullable: false),
                    Ultimo = table.Column<long>(type: "bigint", nullable: false),
                    VenceEn = table.Column<DateOnly>(type: "date", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecuenciasEcf", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TasasCambio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Moneda = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Tasa = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VigenteDesde = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasasCambio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TiposTarjeta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposTarjeta", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TopesDescuento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<int>(type: "int", nullable: false),
                    Nivel = table.Column<int>(type: "int", nullable: false),
                    DepartamentoId = table.Column<int>(type: "int", nullable: true),
                    CategoriaId = table.Column<int>(type: "int", nullable: true),
                    MarcaId = table.Column<int>(type: "int", nullable: true),
                    ArticuloId = table.Column<int>(type: "int", nullable: true),
                    PorcentajeMaximo = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    MontoMaximo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopesDescuento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnidadesMedida",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<int>(type: "int", nullable: false),
                    Abreviatura = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PermiteDecimales = table.Column<bool>(type: "bit", nullable: false),
                    Decimales = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnidadesMedida", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DireccionesCliente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Direccion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ciudad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Referencia = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EsPrincipal = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DireccionesCliente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DireccionesCliente_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Categorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DepartamentoId = table.Column<int>(type: "int", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categorias_Departamentos_DepartamentoId",
                        column: x => x.DepartamentoId,
                        principalTable: "Departamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistorialEstadosEcf",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    DocumentoElectronicoId = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialEstadosEcf", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosEcf_DocumentosElectronicos_DocumentoElectronicoId",
                        column: x => x.DocumentoElectronicoId,
                        principalTable: "DocumentosElectronicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sucursales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Direccion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sucursales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sucursales_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosPuntos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    MiembroId = table.Column<int>(type: "int", nullable: false),
                    Cedula = table.Column<string>(type: "char(11)", unicode: false, fixedLength: true, maxLength: 11, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Puntos = table.Column<int>(type: "int", nullable: false),
                    VentaId = table.Column<int>(type: "int", nullable: true),
                    DevolucionId = table.Column<int>(type: "int", nullable: true),
                    Documento = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    CajaId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    VenceEn = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosPuntos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosPuntos_MiembrosFidelidad_MiembroId",
                        column: x => x.MiembroId,
                        principalTable: "MiembrosFidelidad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EntregasPendiente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    PendienteEntregaId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    RecibeNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RecibeCedula = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntregasPendiente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntregasPendiente_PendientesEntrega_PendienteEntregaId",
                        column: x => x.PendienteEntregaId,
                        principalTable: "PendientesEntrega",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LineasPendienteEntrega",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    PendienteEntregaId = table.Column<int>(type: "int", nullable: false),
                    NumeroLineaVenta = table.Column<int>(type: "int", nullable: false),
                    ArticuloId = table.Column<int>(type: "int", nullable: false),
                    CodigoInterno = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnidadMedidaCodigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DecimalesCantidad = table.Column<int>(type: "int", nullable: false),
                    Serializado = table.Column<bool>(type: "bit", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CantidadEntregada = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Serial = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineasPendienteEntrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LineasPendienteEntrega_PendientesEntrega_PendienteEntregaId",
                        column: x => x.PendienteEntregaId,
                        principalTable: "PendientesEntrega",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolesPermisos",
                columns: table => new
                {
                    RolId = table.Column<int>(type: "int", nullable: false),
                    PermisoCodigo = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolesPermisos", x => new { x.RolId, x.PermisoCodigo });
                    table.ForeignKey(
                        name: "FK_RolesPermisos_Permisos_PermisoCodigo",
                        column: x => x.PermisoCodigo,
                        principalTable: "Permisos",
                        principalColumn: "Codigo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolesPermisos_Roles_RolId",
                        column: x => x.RolId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RolId = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    ClaveHash = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: true),
                    IntentosFallidos = table.Column<int>(type: "int", nullable: false),
                    BloqueadoHasta = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    UltimoIngresoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Roles_RolId",
                        column: x => x.RolId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Articulos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Referencia = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DepartamentoId = table.Column<int>(type: "int", nullable: false),
                    CategoriaId = table.Column<int>(type: "int", nullable: true),
                    MarcaId = table.Column<int>(type: "int", nullable: true),
                    UnidadMedidaId = table.Column<int>(type: "int", nullable: false),
                    ImpuestoId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Costo = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PrecioMinimo = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CantidadMinimaMayor = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Tara = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    RutaImagen = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    EsServicio = table.Column<bool>(type: "bit", nullable: false),
                    MostrarEnCatalogo = table.Column<bool>(type: "bit", nullable: false),
                    VentaEnPos = table.Column<bool>(type: "bit", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articulos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Articulos_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Articulos_Departamentos_DepartamentoId",
                        column: x => x.DepartamentoId,
                        principalTable: "Departamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Articulos_Impuestos_ImpuestoId",
                        column: x => x.ImpuestoId,
                        principalTable: "Impuestos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Articulos_Marcas_MarcaId",
                        column: x => x.MarcaId,
                        principalTable: "Marcas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Articulos_UnidadesMedida_UnidadMedidaId",
                        column: x => x.UnidadMedidaId,
                        principalTable: "UnidadesMedida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cajas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SucursalId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Habilitada = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cajas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cajas_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LineasEntregaPendiente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    EntregaPendienteId = table.Column<int>(type: "int", nullable: false),
                    NumeroLineaVenta = table.Column<int>(type: "int", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Serial = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineasEntregaPendiente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LineasEntregaPendiente_EntregasPendiente_EntregaPendienteId",
                        column: x => x.EntregaPendienteId,
                        principalTable: "EntregasPendiente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CodigosArticulo",
                columns: table => new
                {
                    ArticuloId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodigosArticulo", x => new { x.ArticuloId, x.Codigo });
                    table.ForeignKey(
                        name: "FK_CodigosArticulo_Articulos_ArticuloId",
                        column: x => x.ArticuloId,
                        principalTable: "Articulos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PreciosArticulo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ArticuloId = table.Column<int>(type: "int", nullable: false),
                    Lista = table.Column<int>(type: "int", nullable: false),
                    Precio = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VigenteDesde = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    RegistradoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreciosArticulo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreciosArticulo_Articulos_ArticuloId",
                        column: x => x.ArticuloId,
                        principalTable: "Articulos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parametros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Valor = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    SucursalId = table.Column<int>(type: "int", nullable: true),
                    CajaId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parametros", x => x.Id);
                    table.CheckConstraint("CK_Parametros_UnSoloAmbito", "[SucursalId] IS NULL OR [CajaId] IS NULL");
                    table.ForeignKey(
                        name: "FK_Parametros_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Parametros_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Turnos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CajaId = table.Column<int>(type: "int", nullable: false),
                    SucursalId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<long>(type: "bigint", nullable: false),
                    FechaOperacion = table.Column<DateOnly>(type: "date", nullable: false),
                    UsuarioAperturaId = table.Column<int>(type: "int", nullable: false),
                    UsuarioAperturaNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UsuarioActualId = table.Column<int>(type: "int", nullable: false),
                    UsuarioActualNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FondoInicial = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    AbiertoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    CerradoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turnos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Turnos_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosCajas",
                columns: table => new
                {
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    CajaId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosCajas", x => new { x.UsuarioId, x.CajaId });
                    table.ForeignKey(
                        name: "FK_UsuariosCajas_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsuariosCajas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CierresTurno",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    TurnoId = table.Column<int>(type: "int", nullable: false),
                    CajaId = table.Column<int>(type: "int", nullable: false),
                    SucursalId = table.Column<int>(type: "int", nullable: false),
                    TurnoNumero = table.Column<long>(type: "bigint", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    FechaOperacion = table.Column<DateOnly>(type: "date", nullable: false),
                    AbiertoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Ciego = table.Column<bool>(type: "bit", nullable: false),
                    FondoInicial = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FondoEnCuadre = table.Column<bool>(type: "bit", nullable: false),
                    CantidadVentas = table.Column<int>(type: "int", nullable: false),
                    TotalVentas = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalRetiros = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Moneda = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    TotalEsperado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDeclarado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Diferencia = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CerradoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    ReabiertoPorId = table.Column<int>(type: "int", nullable: true),
                    ReabiertoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReabiertoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    MotivoReapertura = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresTurno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CierresTurno_Turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosCaja",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    TurnoId = table.Column<int>(type: "int", nullable: false),
                    CajaId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Moneda = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UsuarioAnteriorId = table.Column<int>(type: "int", nullable: true),
                    UsuarioAnteriorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AutorizadoPorId = table.Column<int>(type: "int", nullable: true),
                    AutorizadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosCaja", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosCaja_Turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ventas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    NumeroTransaccion = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Secuencia = table.Column<long>(type: "bigint", nullable: false),
                    SucursalId = table.Column<int>(type: "int", nullable: false),
                    CajaId = table.Column<int>(type: "int", nullable: false),
                    TurnoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Moneda = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    SimboloMoneda = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    IniciadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    ActualizadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    AnuladaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AnuladaPorId = table.Column<int>(type: "int", nullable: true),
                    AnuladaPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    ClienteTipoDocumento = table.Column<int>(type: "int", nullable: true),
                    ClienteDocumento = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ClienteNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    TipoComprobante = table.Column<int>(type: "int", nullable: false),
                    PorcentajeRetencion = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    LimiteCompra = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FidelidadMiembroId = table.Column<int>(type: "int", nullable: true),
                    FidelidadCedula = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    FidelidadNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FidelidadNivel = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PuntosAcumulados = table.Column<int>(type: "int", nullable: false),
                    PuntosCanjeados = table.Column<int>(type: "int", nullable: false),
                    PuestaEnEsperaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    DescuentoFacturaTipo = table.Column<int>(type: "int", nullable: true),
                    DescuentoFacturaValor = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    DescuentoFacturaLineas = table.Column<string>(type: "varchar(2000)", unicode: false, maxLength: 2000, nullable: true),
                    MotivoDescuentoFactura = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DescuentoFacturaAutorizadoPorId = table.Column<int>(type: "int", nullable: true),
                    DescuentoFacturaAutorizadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CobradaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    CobradaPorId = table.Column<int>(type: "int", nullable: true),
                    CobradaPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    TotalCobrado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Devuelta = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RedondeoEfectivo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ventas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ventas_Turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CierresTurnoDenominaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CierreTurnoId = table.Column<int>(type: "int", nullable: false),
                    DenominacionId = table.Column<int>(type: "int", nullable: false),
                    Moneda = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Importe = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresTurnoDenominaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CierresTurnoDenominaciones_CierresTurno_CierreTurnoId",
                        column: x => x.CierreTurnoId,
                        principalTable: "CierresTurno",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CierresTurnoFormasPago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CierreTurnoId = table.Column<int>(type: "int", nullable: false),
                    FormaPagoId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Moneda = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Transacciones = table.Column<int>(type: "int", nullable: false),
                    Esperado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Declarado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Diferencia = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresTurnoFormasPago", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CierresTurnoFormasPago_CierresTurno_CierreTurnoId",
                        column: x => x.CierreTurnoId,
                        principalTable: "CierresTurno",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DestinosEntregaVenta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    VentaId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Metodo = table.Column<int>(type: "int", nullable: false),
                    AlmacenId = table.Column<int>(type: "int", nullable: true),
                    AlmacenNombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Sector = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ciudad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Referencia = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Transportista = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CostoEnvio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FechaComprometida = table.Column<DateOnly>(type: "date", nullable: true),
                    Comentario = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    AutorizadoPorId = table.Column<int>(type: "int", nullable: true),
                    AutorizadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DestinosEntregaVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DestinosEntregaVenta_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Devoluciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    SucursalId = table.Column<int>(type: "int", nullable: false),
                    CajaId = table.Column<int>(type: "int", nullable: false),
                    TurnoId = table.Column<int>(type: "int", nullable: true),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    VentaOrigenId = table.Column<int>(type: "int", nullable: false),
                    VentaOrigenNumero = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    VentaOrigenCobradaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    TipoComprobanteOrigen = table.Column<int>(type: "int", nullable: false),
                    EncfOrigen = table.Column<string>(type: "varchar(13)", unicode: false, maxLength: 13, nullable: true),
                    ClienteTipoDocumento = table.Column<int>(type: "int", nullable: true),
                    ClienteDocumento = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ClienteNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MotivoCodigo = table.Column<int>(type: "int", nullable: false),
                    MotivoNombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    AutorizadoPorId = table.Column<int>(type: "int", nullable: true),
                    AutorizadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    RetieneImpuesto = table.Column<bool>(type: "bit", nullable: false),
                    EsInterna = table.Column<bool>(type: "bit", nullable: false),
                    EsTotal = table.Column<bool>(type: "bit", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Impuesto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ImpuestoRetenido = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Saldo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Moneda = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    SimboloMoneda = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    PuntosReversados = table.Column<int>(type: "int", nullable: false),
                    FechaEmision = table.Column<DateOnly>(type: "date", nullable: false),
                    CreadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Encf = table.Column<string>(type: "varchar(13)", unicode: false, maxLength: 13, nullable: true),
                    Reembolso = table.Column<int>(type: "int", nullable: false),
                    ReembolsoReferencia = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReembolsoDetalle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReembolsadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devoluciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devoluciones_Ventas_VentaOrigenId",
                        column: x => x.VentaOrigenId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LineasVenta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Serial = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SerialPendiente = table.Column<bool>(type: "bit", nullable: false),
                    PromocionId = table.Column<int>(type: "int", nullable: true),
                    PromocionCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PromocionNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PromocionDescripcion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DescuentoPromocion = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PromocionDesactivada = table.Column<bool>(type: "bit", nullable: false),
                    DescuentoManual = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DescuentoManualTipo = table.Column<int>(type: "int", nullable: true),
                    DescuentoManualValor = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MotivoDescuento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DescuentoAutorizadoPorId = table.Column<int>(type: "int", nullable: true),
                    DescuentoAutorizadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DescuentoFactura = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VentaId = table.Column<int>(type: "int", nullable: false),
                    NumeroLinea = table.Column<int>(type: "int", nullable: false),
                    ArticuloId = table.Column<int>(type: "int", nullable: false),
                    CodigoInterno = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CodigoLeido = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TipoArticulo = table.Column<int>(type: "int", nullable: false),
                    DepartamentoId = table.Column<int>(type: "int", nullable: false),
                    CategoriaId = table.Column<int>(type: "int", nullable: true),
                    MarcaId = table.Column<int>(type: "int", nullable: true),
                    PermiteDescuentoManual = table.Column<bool>(type: "bit", nullable: false),
                    UnidadMedidaCodigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PermiteDecimales = table.Column<bool>(type: "bit", nullable: false),
                    DecimalesCantidad = table.Column<int>(type: "int", nullable: false),
                    ImpuestoId = table.Column<int>(type: "int", nullable: false),
                    PorcentajeImpuesto = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IndicadorFacturacion = table.Column<int>(type: "int", nullable: false),
                    EsServicio = table.Column<bool>(type: "bit", nullable: false),
                    PrecioDetalle = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PrecioMayor = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CantidadMinimaMayor = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PrecioMinimo = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Lista = table.Column<int>(type: "int", nullable: false),
                    MotivoPrecio = table.Column<int>(type: "int", nullable: false),
                    ImporteEtiqueta = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    LeidaDeBalanza = table.Column<bool>(type: "bit", nullable: false),
                    EsReverso = table.Column<bool>(type: "bit", nullable: false),
                    LineaAnuladaNumero = table.Column<int>(type: "int", nullable: true),
                    Anulada = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineasVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LineasVenta_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PagosVenta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    VentaId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    FormaPagoId = table.Column<int>(type: "int", nullable: false),
                    FormaPagoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FormaPagoNombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Moneda = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    MontoRecibido = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TasaCambio = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MontoAplicado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Referencia = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    BancoId = table.Column<int>(type: "int", nullable: true),
                    BancoNombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TipoTarjetaId = table.Column<int>(type: "int", nullable: true),
                    TipoTarjetaNombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UltimosDigitos = table.Column<string>(type: "varchar(4)", unicode: false, maxLength: 4, nullable: true),
                    AprobacionManual = table.Column<bool>(type: "bit", nullable: false),
                    ParaConciliar = table.Column<bool>(type: "bit", nullable: false),
                    OperacionTerminalId = table.Column<int>(type: "int", nullable: true),
                    PermiteDevuelta = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagosVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagosVenta_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LineasDestinoEntrega",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    DestinoEntregaId = table.Column<int>(type: "int", nullable: false),
                    NumeroLinea = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineasDestinoEntrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LineasDestinoEntrega_DestinosEntregaVenta_DestinoEntregaId",
                        column: x => x.DestinoEntregaId,
                        principalTable: "DestinosEntregaVenta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConsumosNotaCredito",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    DevolucionId = table.Column<int>(type: "int", nullable: false),
                    VentaId = table.Column<int>(type: "int", nullable: false),
                    VentaNumero = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    CajaId = table.Column<int>(type: "int", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SaldoRestante = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumosNotaCredito", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumosNotaCredito_Devoluciones_DevolucionId",
                        column: x => x.DevolucionId,
                        principalTable: "Devoluciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LineasDevolucion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    DevolucionId = table.Column<int>(type: "int", nullable: false),
                    NumeroLineaOrigen = table.Column<int>(type: "int", nullable: false),
                    ArticuloId = table.Column<int>(type: "int", nullable: false),
                    CodigoInterno = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CodigoLeido = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnidadMedidaCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DecimalesCantidad = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PorcentajeImpuesto = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    IndicadorFacturacion = table.Column<int>(type: "int", nullable: false),
                    EsServicio = table.Column<bool>(type: "bit", nullable: false),
                    ImporteFactura = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Base = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Impuesto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ImpuestoRetenido = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Importe = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Serial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineasDevolucion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LineasDevolucion_Devoluciones_DevolucionId",
                        column: x => x.DevolucionId,
                        principalTable: "Devoluciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Almacenes_Codigo",
                table: "Almacenes",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articulos_CategoriaId",
                table: "Articulos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Articulos_Codigo",
                table: "Articulos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articulos_DepartamentoId",
                table: "Articulos",
                column: "DepartamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Articulos_Descripcion",
                table: "Articulos",
                column: "Descripcion");

            migrationBuilder.CreateIndex(
                name: "IX_Articulos_ImpuestoId",
                table: "Articulos",
                column: "ImpuestoId");

            migrationBuilder.CreateIndex(
                name: "IX_Articulos_MarcaId",
                table: "Articulos",
                column: "MarcaId");

            migrationBuilder.CreateIndex(
                name: "IX_Articulos_UnidadMedidaId",
                table: "Articulos",
                column: "UnidadMedidaId");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Accion",
                table: "Auditoria",
                column: "Accion");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_OcurridoEn",
                table: "Auditoria",
                column: "OcurridoEn");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_TipoEntidad_EntidadId",
                table: "Auditoria",
                columns: new[] { "TipoEntidad", "EntidadId" });

            migrationBuilder.CreateIndex(
                name: "IX_AutorizacionesOtorgadas_SolicitanteId_ConcedidaEn",
                table: "AutorizacionesOtorgadas",
                columns: new[] { "SolicitanteId", "ConcedidaEn" });

            migrationBuilder.CreateIndex(
                name: "IX_Bancos_Codigo",
                table: "Bancos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BandejaSalida_Estado_ProximoIntentoEn",
                table: "BandejaSalida",
                columns: new[] { "Estado", "ProximoIntentoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_BandejaSalida_Referencia",
                table: "BandejaSalida",
                column: "Referencia");

            migrationBuilder.CreateIndex(
                name: "IX_Cajas_SucursalId_Codigo",
                table: "Cajas",
                columns: new[] { "SucursalId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_Codigo",
                table: "Categorias",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_DepartamentoId",
                table: "Categorias",
                column: "DepartamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_CierresTurno_CajaId_CerradoEn",
                table: "CierresTurno",
                columns: new[] { "CajaId", "CerradoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_CierresTurno_TurnoId_Numero",
                table: "CierresTurno",
                columns: new[] { "TurnoId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CierresTurnoDenominaciones_CierreTurnoId",
                table: "CierresTurnoDenominaciones",
                column: "CierreTurnoId");

            migrationBuilder.CreateIndex(
                name: "IX_CierresTurnoFormasPago_CierreTurnoId",
                table: "CierresTurnoFormasPago",
                column: "CierreTurnoId");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Codigo",
                table: "Clientes",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Documento",
                table: "Clientes",
                column: "Documento");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Nombre",
                table: "Clientes",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_TipoDocumento_Documento",
                table: "Clientes",
                columns: new[] { "TipoDocumento", "Documento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodigosArticulo_Codigo",
                table: "CodigosArticulo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsumosNotaCredito_DevolucionId",
                table: "ConsumosNotaCredito",
                column: "DevolucionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumosNotaCredito_VentaId",
                table: "ConsumosNotaCredito",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_Denominaciones_Moneda_Valor_Tipo",
                table: "Denominaciones",
                columns: new[] { "Moneda", "Valor", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departamentos_Codigo",
                table: "Departamentos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DescuentosTarjeta_Codigo",
                table: "DescuentosTarjeta",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DestinosEntregaVenta_VentaId_Numero",
                table: "DestinosEntregaVenta",
                columns: new[] { "VentaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devoluciones_CajaId_Numero",
                table: "Devoluciones",
                columns: new[] { "CajaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devoluciones_Encf",
                table: "Devoluciones",
                column: "Encf",
                unique: true,
                filter: "[Encf] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Devoluciones_VentaOrigenId",
                table: "Devoluciones",
                column: "VentaOrigenId");

            migrationBuilder.CreateIndex(
                name: "IX_DireccionesCliente_ClienteId_Alias",
                table: "DireccionesCliente",
                columns: new[] { "ClienteId", "Alias" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosElectronicos_CajaId_Estado",
                table: "DocumentosElectronicos",
                columns: new[] { "CajaId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosElectronicos_Encf",
                table: "DocumentosElectronicos",
                column: "Encf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosElectronicos_VentaId",
                table: "DocumentosElectronicos",
                column: "VentaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_Rnc",
                table: "Empresas",
                column: "Rnc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntregasPendiente_PendienteEntregaId",
                table: "EntregasPendiente",
                column: "PendienteEntregaId");

            migrationBuilder.CreateIndex(
                name: "IX_FormasPago_Codigo",
                table: "FormasPago",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosEcf_DocumentoElectronicoId",
                table: "HistorialEstadosEcf",
                column: "DocumentoElectronicoId");

            migrationBuilder.CreateIndex(
                name: "IX_Impuestos_Codigo",
                table: "Impuestos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LineasDestinoEntrega_DestinoEntregaId",
                table: "LineasDestinoEntrega",
                column: "DestinoEntregaId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasDevolucion_DevolucionId",
                table: "LineasDevolucion",
                column: "DevolucionId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasEntregaPendiente_EntregaPendienteId",
                table: "LineasEntregaPendiente",
                column: "EntregaPendienteId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasPendienteEntrega_PendienteEntregaId",
                table: "LineasPendienteEntrega",
                column: "PendienteEntregaId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasVenta_PromocionId",
                table: "LineasVenta",
                column: "PromocionId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasVenta_VentaId_NumeroLinea",
                table: "LineasVenta",
                columns: new[] { "VentaId", "NumeroLinea" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Marcas_Codigo",
                table: "Marcas",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MiembrosFidelidad_Cedula",
                table: "MiembrosFidelidad",
                column: "Cedula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Monedas_Codigo",
                table: "Monedas",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MotivosDescuento_Codigo",
                table: "MotivosDescuento",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MotivosDevolucion_Codigo",
                table: "MotivosDevolucion",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_TurnoId_Tipo_Numero",
                table: "MovimientosCaja",
                columns: new[] { "TurnoId", "Tipo", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosPuntos_MiembroId_Fecha",
                table: "MovimientosPuntos",
                columns: new[] { "MiembroId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosPuntos_VentaId",
                table: "MovimientosPuntos",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_NivelesFidelidad_Codigo",
                table: "NivelesFidelidad",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperacionesTerminal_CajaId_TurnoId_Fecha",
                table: "OperacionesTerminal",
                columns: new[] { "CajaId", "TurnoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_OperacionesTerminal_VentaId",
                table: "OperacionesTerminal",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosVenta_OperacionTerminalId",
                table: "PagosVenta",
                column: "OperacionTerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosVenta_VentaId_Numero",
                table: "PagosVenta",
                columns: new[] { "VentaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parametros_CajaId",
                table: "Parametros",
                column: "CajaId");

            migrationBuilder.CreateIndex(
                name: "IX_Parametros_Clave_SucursalId_CajaId",
                table: "Parametros",
                columns: new[] { "Clave", "SucursalId", "CajaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parametros_SucursalId",
                table: "Parametros",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_PendientesEntrega_Estado_CreadoEn",
                table: "PendientesEntrega",
                columns: new[] { "Estado", "CreadoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_PendientesEntrega_Numero",
                table: "PendientesEntrega",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendientesEntrega_VentaId",
                table: "PendientesEntrega",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_PreciosArticulo_ArticuloId_Lista_VigenteDesde",
                table: "PreciosArticulo",
                columns: new[] { "ArticuloId", "Lista", "VigenteDesde" });

            migrationBuilder.CreateIndex(
                name: "IX_Promociones_Activa_VigenteHasta",
                table: "Promociones",
                columns: new[] { "Activa", "VigenteHasta" });

            migrationBuilder.CreateIndex(
                name: "IX_Promociones_Codigo",
                table: "Promociones",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReglasAcumulacion_Codigo",
                table: "ReglasAcumulacion",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Codigo",
                table: "Roles",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolesPermisos_PermisoCodigo",
                table: "RolesPermisos",
                column: "PermisoCodigo");

            migrationBuilder.CreateIndex(
                name: "IX_SecuenciasEcf_CajaId_TipoComprobante_Activa_Desde",
                table: "SecuenciasEcf",
                columns: new[] { "CajaId", "TipoComprobante", "Activa", "Desde" });

            migrationBuilder.CreateIndex(
                name: "IX_SecuenciasEcf_TipoComprobante_Desde",
                table: "SecuenciasEcf",
                columns: new[] { "TipoComprobante", "Desde" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sucursales_EmpresaId_Codigo",
                table: "Sucursales",
                columns: new[] { "EmpresaId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasasCambio_Moneda_VigenteDesde",
                table: "TasasCambio",
                columns: new[] { "Moneda", "VigenteDesde" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TiposTarjeta_Codigo",
                table: "TiposTarjeta",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopesDescuento_Codigo",
                table: "TopesDescuento",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopesDescuento_Nivel_DepartamentoId_ArticuloId",
                table: "TopesDescuento",
                columns: new[] { "Nivel", "DepartamentoId", "ArticuloId" });

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_CajaId_Abierto",
                table: "Turnos",
                column: "CajaId",
                unique: true,
                filter: "[Estado] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_CajaId_Numero",
                table: "Turnos",
                columns: new[] { "CajaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesMedida_Codigo",
                table: "UnidadesMedida",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Codigo",
                table: "Usuarios",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_RolId",
                table: "Usuarios",
                column: "RolId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosCajas_CajaId",
                table: "UsuariosCajas",
                column: "CajaId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_CajaId_Secuencia",
                table: "Ventas",
                columns: new[] { "CajaId", "Secuencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_NumeroTransaccion",
                table: "Ventas",
                column: "NumeroTransaccion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_TurnoId_Estado",
                table: "Ventas",
                columns: new[] { "TurnoId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_TurnoId_UsuarioId_Estado",
                table: "Ventas",
                columns: new[] { "TurnoId", "UsuarioId", "Estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Almacenes");

            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropTable(
                name: "AutorizacionesOtorgadas");

            migrationBuilder.DropTable(
                name: "Bancos");

            migrationBuilder.DropTable(
                name: "BandejaSalida");

            migrationBuilder.DropTable(
                name: "CierresTurnoDenominaciones");

            migrationBuilder.DropTable(
                name: "CierresTurnoFormasPago");

            migrationBuilder.DropTable(
                name: "CodigosArticulo");

            migrationBuilder.DropTable(
                name: "ConsumosNotaCredito");

            migrationBuilder.DropTable(
                name: "ContribuyentesDgii");

            migrationBuilder.DropTable(
                name: "Denominaciones");

            migrationBuilder.DropTable(
                name: "DescuentosTarjeta");

            migrationBuilder.DropTable(
                name: "DireccionesCliente");

            migrationBuilder.DropTable(
                name: "FormasPago");

            migrationBuilder.DropTable(
                name: "HistorialEstadosEcf");

            migrationBuilder.DropTable(
                name: "LineasDestinoEntrega");

            migrationBuilder.DropTable(
                name: "LineasDevolucion");

            migrationBuilder.DropTable(
                name: "LineasEntregaPendiente");

            migrationBuilder.DropTable(
                name: "LineasPendienteEntrega");

            migrationBuilder.DropTable(
                name: "LineasVenta");

            migrationBuilder.DropTable(
                name: "MarcasSincronizacion");

            migrationBuilder.DropTable(
                name: "Monedas");

            migrationBuilder.DropTable(
                name: "MotivosDescuento");

            migrationBuilder.DropTable(
                name: "MotivosDevolucion");

            migrationBuilder.DropTable(
                name: "MovimientosCaja");

            migrationBuilder.DropTable(
                name: "MovimientosPuntos");

            migrationBuilder.DropTable(
                name: "NivelesFidelidad");

            migrationBuilder.DropTable(
                name: "OperacionesTerminal");

            migrationBuilder.DropTable(
                name: "PagosVenta");

            migrationBuilder.DropTable(
                name: "Parametros");

            migrationBuilder.DropTable(
                name: "PreciosArticulo");

            migrationBuilder.DropTable(
                name: "Promociones");

            migrationBuilder.DropTable(
                name: "ReglasAcumulacion");

            migrationBuilder.DropTable(
                name: "RolesPermisos");

            migrationBuilder.DropTable(
                name: "SecuenciasCaja");

            migrationBuilder.DropTable(
                name: "SecuenciasEcf");

            migrationBuilder.DropTable(
                name: "TasasCambio");

            migrationBuilder.DropTable(
                name: "TiposTarjeta");

            migrationBuilder.DropTable(
                name: "TopesDescuento");

            migrationBuilder.DropTable(
                name: "UsuariosCajas");

            migrationBuilder.DropTable(
                name: "CierresTurno");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropTable(
                name: "DocumentosElectronicos");

            migrationBuilder.DropTable(
                name: "DestinosEntregaVenta");

            migrationBuilder.DropTable(
                name: "Devoluciones");

            migrationBuilder.DropTable(
                name: "EntregasPendiente");

            migrationBuilder.DropTable(
                name: "MiembrosFidelidad");

            migrationBuilder.DropTable(
                name: "Articulos");

            migrationBuilder.DropTable(
                name: "Permisos");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Ventas");

            migrationBuilder.DropTable(
                name: "PendientesEntrega");

            migrationBuilder.DropTable(
                name: "Categorias");

            migrationBuilder.DropTable(
                name: "Impuestos");

            migrationBuilder.DropTable(
                name: "Marcas");

            migrationBuilder.DropTable(
                name: "UnidadesMedida");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Turnos");

            migrationBuilder.DropTable(
                name: "Departamentos");

            migrationBuilder.DropTable(
                name: "Cajas");

            migrationBuilder.DropTable(
                name: "Sucursales");

            migrationBuilder.DropTable(
                name: "Empresas");

            migrationBuilder.DropSequence(
                name: "EntityFrameworkHiLoSequence");
        }
    }
}
