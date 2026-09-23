/*
    CG-POS · Base de datos de la caja
    Estructura completa de una caja. No lleva datos: todo baja del Central en la primera sincronización.

    Cómo usarlo:
        sqlcmd -S .\SQLEXPRESS -E -i estructura_base_datos_pos.sql
    o ábralo en SQL Server Management Studio y ejecútelo.

    Crea la base «CgPosCaja» si no existe, la secuencia de Id, todas las tablas con sus llaves,
    índices y restricciones. Volver a ejecutarlo sobre una base que ya tiene las tablas da error:
    es para crear la base desde cero.

    Generado desde el modelo del sistema con scripts/base-datos/generar-estructura-sql.py.
    No editar a mano: se edita el modelo y se vuelve a generar.
*/

IF DB_ID(N'CgPosCaja') IS NULL
BEGIN
    PRINT 'Creando la base CgPosCaja...';
    EXEC (N'CREATE DATABASE [CgPosCaja]');
END
GO

ALTER DATABASE [CgPosCaja] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO

USE [CgPosCaja];
GO

/* Los índices filtrados y las restricciones exigen estas opciones; sqlcmd las trae apagadas. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

CREATE SEQUENCE [SecuenciaArticulos] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaAuditoria] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaBancos] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaCajas] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaCategorias] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaCierresTurno] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaCierresTurnoFormasPago] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaClientes] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaConfiguracionCaja] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaConsumosNotaCredito] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaDenominaciones] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaDepartamentos] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaDescuentosTarjeta] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaDestinosEntregaVenta] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaDestinosEntregaVentaGuardadas] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaDestinosEntregaVentaTemp] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaDevoluciones] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaDireccionesCliente] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaDocumentosElectronicos] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaEmpresas] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaFacturasConsultadas] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaFormasPago] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaHistorialEstadosEcf] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaImpuestos] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaLineasDestinoEntrega] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaLineasDestinoEntregaGuardadas] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaLineasDestinoEntregaTemp] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaLineasDevolucion] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaLineasFacturaConsultada] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaLineasPendienteEntrega] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaLineasVenta] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaLineasVentaGuardadas] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaLineasVentaTemp] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaLotesTarjetas] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaLotesTarjetasAprobaciones] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaMarcas] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaMiembrosFidelidad] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaMonedas] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaMotivosDescuento] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaMotivosDevolucion] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaMovimientosCaja] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaMovimientosPuntos] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaNivelesFidelidad] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaOperacionesTerminal] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaPagosVenta] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaParametros] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaPendientesEntrega] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaPromociones] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaReglasAcumulacion] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaRoles] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaSecuenciasEcf] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaSucursales] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaTasasCambio] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaTiposTarjeta] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaTopesDescuento] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaTurnos] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaUnidadesMedida] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaUsuarios] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaVentas] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaVentasGuardadas] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaVentasTemp] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE TABLE [Auditoria] (
    [Id] int NOT NULL,
    [OcurridoEn] datetimeoffset(3) NOT NULL,
    [Accion] nvarchar(100) NOT NULL,
    [TipoEntidad] nvarchar(100) NOT NULL,
    [EntidadId] nvarchar(64) NULL,
    [Detalle] nvarchar(max) NULL,
    [Cambios] nvarchar(max) NULL,
    [Motivo] nvarchar(500) NULL,
    [UsuarioId] int NULL,
    [UsuarioNombre] nvarchar(150) NULL,
    [AutorizadoPorId] int NULL,
    [AutorizadoPorNombre] nvarchar(150) NULL,
    CONSTRAINT [PK_Auditoria] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AutorizacionesOtorgadas] (
    [Id] uniqueidentifier NOT NULL,
    [Permiso] varchar(100) NOT NULL,
    [CajaId] int NOT NULL,
    [SolicitanteId] int NOT NULL,
    [SolicitanteNombre] nvarchar(150) NOT NULL,
    [SupervisorId] int NOT NULL,
    [SupervisorNombre] nvarchar(150) NOT NULL,
    [Motivo] nvarchar(500) NOT NULL,
    [ConcedidaEn] datetimeoffset(3) NOT NULL,
    [VenceEn] datetimeoffset(3) NOT NULL,
    [UsadaEn] datetimeoffset(3) NULL,
    [UsadaEnTipoEntidad] nvarchar(100) NULL,
    [UsadaEnEntidadId] nvarchar(64) NULL,
    CONSTRAINT [PK_AutorizacionesOtorgadas] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Bancos] (
    [Id] int NOT NULL,
    [Codigo] nvarchar(20) NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [RutaLogo] nvarchar(260) NULL,
    [Activo] bit NOT NULL,
    CONSTRAINT [PK_Bancos] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [BandejaSalida] (
    [Id] uniqueidentifier NOT NULL,
    [TipoMensaje] nvarchar(100) NOT NULL,
    [Referencia] varchar(40) NOT NULL,
    [Contenido] nvarchar(max) NOT NULL,
    [HashContenido] char(64) NOT NULL,
    [Estado] int NOT NULL,
    [Intentos] int NOT NULL,
    [CreadoEn] datetimeoffset(3) NOT NULL,
    [ProximoIntentoEn] datetimeoffset(3) NULL,
    [EnviadoEn] datetimeoffset(3) NULL,
    [ConfirmadoEn] datetimeoffset(3) NULL,
    [UltimoError] nvarchar(2000) NULL,
    CONSTRAINT [PK_BandejaSalida] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Clientes] (
    [Id] int NOT NULL,
    [Codigo] nvarchar(20) NOT NULL,
    [TipoDocumento] int NOT NULL,
    [Documento] varchar(20) NOT NULL,
    [Nombre] nvarchar(150) NOT NULL,
    [TipoComprobantePredeterminado] int NOT NULL,
    [ExoneradoItbis] bit NOT NULL,
    [AplicaRetencion] bit NOT NULL,
    [ListaPrecioPredeterminada] int NOT NULL,
    [Telefono] nvarchar(20) NULL,
    [TelefonoAlterno] nvarchar(20) NULL,
    [Correo] nvarchar(150) NULL,
    [Contacto] nvarchar(150) NULL,
    [Activo] bit NOT NULL,
    CONSTRAINT [PK_Clientes] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ConfiguracionCaja] (
    [Id] int NOT NULL,
    [SucursalCodigo] char(2) NOT NULL,
    [CajaCodigo] char(2) NOT NULL,
    [DireccionIp] varchar(45) NOT NULL,
    [UrlCentral] varchar(250) NOT NULL,
    [SecretoCifrado] varchar(500) NOT NULL,
    [ConfiguradaEn] datetimeoffset(3) NOT NULL,
    [ConfiguradaPor] nvarchar(250) NULL,
    [RechazadaEn] datetimeoffset(3) NULL,
    [MotivoRechazo] nvarchar(250) NULL,
    CONSTRAINT [PK_ConfiguracionCaja] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Denominaciones] (
    [Id] int NOT NULL,
    [Moneda] char(3) NOT NULL,
    [Valor] decimal(18,2) NOT NULL,
    [Tipo] int NOT NULL,
    [Activa] bit NOT NULL,
    CONSTRAINT [PK_Denominaciones] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Departamentos] (
    [Id] int NOT NULL,
    [Codigo] int NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [PermiteDescuentoManual] bit NOT NULL,
    [EsNoCodificada] bit NOT NULL,
    [Activa] bit NOT NULL,
    CONSTRAINT [PK_Departamentos] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [DescuentosTarjeta] (
    [Id] int NOT NULL,
    [Codigo] varchar(30) NOT NULL,
    [Nombre] nvarchar(150) NOT NULL,
    [Bines] varchar(400) NOT NULL,
    [Tipo] int NOT NULL,
    [Valor] decimal(18,4) NOT NULL,
    [MontoMinimo] decimal(18,4) NULL,
    [MontoMaximo] decimal(18,4) NULL,
    [BancoId] int NULL,
    [VigenteDesde] datetimeoffset(3) NOT NULL,
    [VigenteHasta] datetimeoffset(3) NOT NULL,
    [Dias] int NOT NULL,
    [Activo] bit NOT NULL,
    CONSTRAINT [PK_DescuentosTarjeta] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [DocumentosElectronicos] (
    [Id] int NOT NULL,
    [VentaId] int NOT NULL,
    [NumeroDocumento] varchar(40) NOT NULL,
    [TipoOrigen] int NOT NULL,
    [CajaId] int NOT NULL,
    [TipoComprobante] int NOT NULL,
    [Encf] varchar(13) NOT NULL,
    [FechaEmision] datetimeoffset(3) NOT NULL,
    [FechaFirma] datetimeoffset(3) NOT NULL,
    [CodigoSeguridad] varchar(20) NOT NULL,
    [MontoTotal] decimal(18,2) NOT NULL,
    [HashXml] varchar(64) NOT NULL,
    [RutaXml] nvarchar(400) NOT NULL,
    [UrlTimbre] nvarchar(600) NOT NULL,
    [Estado] int NOT NULL,
    [EstadoActualizadoEn] datetimeoffset(3) NOT NULL,
    [MensajeEstado] nvarchar(500) NULL,
    CONSTRAINT [PK_DocumentosElectronicos] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Empresas] (
    [Id] int NOT NULL,
    [Rnc] varchar(11) NOT NULL,
    [RazonSocial] nvarchar(150) NOT NULL,
    [NombreComercial] nvarchar(150) NULL,
    [Direccion] nvarchar(250) NULL,
    [Telefono] nvarchar(20) NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    CONSTRAINT [PK_Empresas] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [FacturasConsultadas] (
    [Id] int NOT NULL,
    [Numero] varchar(40) NOT NULL,
    [Encf] varchar(13) NULL,
    [SucursalCodigo] varchar(50) NOT NULL,
    [CajaCodigo] varchar(50) NOT NULL,
    [TipoComprobante] int NOT NULL,
    [CobradaEn] datetimeoffset(3) NOT NULL,
    [ClienteTipoDocumento] int NULL,
    [ClienteDocumento] varchar(20) NULL,
    [ClienteNombre] nvarchar(200) NULL,
    [Moneda] varchar(3) NOT NULL,
    [SimboloMoneda] nvarchar(5) NOT NULL,
    [Total] decimal(18,2) NOT NULL,
    [ConsultadaEn] datetimeoffset(3) NOT NULL,
    [VentaLocalId] int NULL,
    CONSTRAINT [PK_FacturasConsultadas] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [FormasPago] (
    [Id] int NOT NULL,
    [Codigo] nvarchar(20) NOT NULL,
    [Nombre] nvarchar(50) NOT NULL,
    [Tipo] int NOT NULL,
    [Moneda] char(3) NOT NULL,
    [Orden] int NOT NULL,
    [AbreGaveta] bit NOT NULL,
    [PermiteDevuelta] bit NOT NULL,
    [RequiereReferencia] bit NOT NULL,
    [RequiereBanco] bit NOT NULL,
    [PermiteComprobanteFiscal] bit NOT NULL,
    [Activa] bit NOT NULL,
    CONSTRAINT [PK_FormasPago] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Impuestos] (
    [Id] int NOT NULL,
    [Codigo] nvarchar(20) NOT NULL,
    [Nombre] nvarchar(50) NOT NULL,
    [Porcentaje] decimal(5,2) NOT NULL,
    [IndicadorFacturacion] int NOT NULL,
    [Activo] bit NOT NULL,
    CONSTRAINT [PK_Impuestos] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Marcas] (
    [Id] int NOT NULL,
    [Codigo] int NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [Activa] bit NOT NULL,
    CONSTRAINT [PK_Marcas] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [MarcasSincronizacion] (
    [Clave] varchar(100) NOT NULL,
    [Valor] bigint NOT NULL,
    [Texto] varchar(100) NULL,
    [ActualizadaEn] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_MarcasSincronizacion] PRIMARY KEY ([Clave])
);
GO


CREATE TABLE [MiembrosFidelidad] (
    [Id] int NOT NULL,
    [Cedula] char(11) NOT NULL,
    [Nombre] nvarchar(150) NOT NULL,
    [Telefono] nvarchar(20) NULL,
    [Correo] nvarchar(150) NULL,
    [NivelId] int NULL,
    [SaldoSincronizado] int NOT NULL,
    [SaldoSincronizadoEn] datetimeoffset(3) NULL,
    [PuntosPorVencer] int NOT NULL,
    [ProximoVencimiento] date NULL,
    [InscritoEn] datetimeoffset(3) NOT NULL,
    [InscritoEnCaja] bit NOT NULL,
    [Activo] bit NOT NULL,
    CONSTRAINT [PK_MiembrosFidelidad] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Monedas] (
    [Id] int NOT NULL,
    [Codigo] char(3) NOT NULL,
    [Nombre] nvarchar(50) NOT NULL,
    [Simbolo] nvarchar(5) NOT NULL,
    [Activa] bit NOT NULL,
    CONSTRAINT [PK_Monedas] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [MotivosDescuento] (
    [Id] int NOT NULL,
    [Codigo] int NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [Activo] bit NOT NULL,
    CONSTRAINT [PK_MotivosDescuento] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [MotivosDevolucion] (
    [Id] int NOT NULL,
    [Codigo] int NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [Activo] bit NOT NULL,
    CONSTRAINT [PK_MotivosDevolucion] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [NivelesFidelidad] (
    [Id] int NOT NULL,
    [Codigo] int NOT NULL,
    [Nombre] nvarchar(60) NOT NULL,
    [Orden] int NOT NULL,
    [FactorAcumulacion] decimal(9,4) NOT NULL,
    [Activo] bit NOT NULL,
    CONSTRAINT [PK_NivelesFidelidad] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [OperacionesTerminal] (
    [Id] int NOT NULL,
    [CajaId] int NOT NULL,
    [TurnoId] int NOT NULL,
    [VentaId] int NOT NULL,
    [UsuarioId] int NOT NULL,
    [Tipo] int NOT NULL,
    [Estado] int NOT NULL,
    [Monto] decimal(18,2) NOT NULL,
    [Aprobacion] nvarchar(20) NULL,
    [UltimosDigitos] varchar(4) NULL,
    [Marca] nvarchar(30) NULL,
    [Mensaje] nvarchar(200) NULL,
    [ReferenciaTerminal] varchar(40) NULL,
    [Fecha] datetimeoffset(3) NOT NULL,
    [OperacionAnuladaId] int NULL,
    [UsadaEnCobro] bit NOT NULL,
    CONSTRAINT [PK_OperacionesTerminal] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [PendientesEntrega] (
    [Id] int NOT NULL,
    [Numero] varchar(40) NOT NULL,
    [VentaId] int NOT NULL,
    [VentaNumero] varchar(40) NOT NULL,
    [SucursalId] int NOT NULL,
    [CajaId] int NOT NULL,
    [Metodo] int NOT NULL,
    [SucursalRetiroId] int NULL,
    [SucursalRetiroNombre] nvarchar(150) NULL,
    [Direccion] nvarchar(250) NULL,
    [Sector] nvarchar(100) NULL,
    [Ciudad] nvarchar(100) NULL,
    [Referencia] nvarchar(250) NULL,
    [Telefono] nvarchar(20) NULL,
    [Transportista] nvarchar(100) NULL,
    [CostoEnvio] decimal(18,2) NULL,
    [FechaComprometida] date NULL,
    [Comentario] nvarchar(250) NULL,
    [ClienteDocumento] varchar(20) NULL,
    [ClienteNombre] nvarchar(150) NULL,
    [VendidoPorNombre] nvarchar(150) NOT NULL,
    [AutorizadoPorNombre] nvarchar(150) NULL,
    [Estado] int NOT NULL,
    [CreadoEn] datetimeoffset(3) NOT NULL,
    [ActualizadoEn] datetimeoffset(3) NOT NULL,
    [ActualizadoPorNombre] nvarchar(150) NOT NULL,
    [MotivoAnulacion] nvarchar(250) NULL,
    CONSTRAINT [PK_PendientesEntrega] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Permisos] (
    [Codigo] varchar(100) NOT NULL,
    [Modulo] nvarchar(50) NOT NULL,
    [Descripcion] nvarchar(250) NOT NULL,
    CONSTRAINT [PK_Permisos] PRIMARY KEY ([Codigo])
);
GO


CREATE TABLE [Promociones] (
    [Id] int NOT NULL,
    [Codigo] nvarchar(30) NOT NULL,
    [Nombre] nvarchar(150) NOT NULL,
    [Tipo] int NOT NULL,
    [Valor] decimal(18,2) NOT NULL,
    [CantidadLleva] int NULL,
    [CantidadPaga] int NULL,
    [CantidadMinima] decimal(18,3) NULL,
    [LimitePorCliente] decimal(18,3) NULL,
    [VigenteDesde] datetimeoffset(3) NOT NULL,
    [VigenteHasta] datetimeoffset(3) NOT NULL,
    [Dias] int NOT NULL,
    [HoraDesde] time NULL,
    [HoraHasta] time NULL,
    [SoloFidelidad] bit NOT NULL,
    [Activa] bit NOT NULL,
    [Articulos] varchar(max) NOT NULL,
    [Categorias] varchar(max) NOT NULL,
    [Departamentos] varchar(max) NOT NULL,
    [Marcas] varchar(max) NOT NULL,
    [Sucursales] varchar(max) NOT NULL,
    CONSTRAINT [PK_Promociones] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ReglasAcumulacion] (
    [Id] int NOT NULL,
    [Codigo] int NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [Tipo] int NOT NULL,
    [ReferenciaId] int NULL,
    [DiaSemana] int NULL,
    [MontoBase] decimal(18,2) NOT NULL,
    [Puntos] decimal(18,4) NOT NULL,
    [VigenteDesde] datetimeoffset(3) NULL,
    [VigenteHasta] datetimeoffset(3) NULL,
    [Activa] bit NOT NULL,
    CONSTRAINT [PK_ReglasAcumulacion] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Roles] (
    [Id] int NOT NULL,
    [Codigo] nvarchar(30) NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [Nivel] int NOT NULL,
    [Activo] bit NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [SecuenciasCaja] (
    [CajaId] int NOT NULL,
    [Tipo] varchar(30) NOT NULL,
    [Ultimo] bigint NOT NULL,
    [SucursalCodigo] char(2) NOT NULL,
    [CajaCodigo] char(2) NOT NULL,
    CONSTRAINT [PK_SecuenciasCaja] PRIMARY KEY ([CajaId], [Tipo])
);
GO


CREATE TABLE [SecuenciasEcf] (
    [Id] int NOT NULL,
    [CajaId] int NOT NULL,
    [Serie] char(1) NOT NULL,
    [TipoComprobante] int NOT NULL,
    [Desde] bigint NOT NULL,
    [Hasta] bigint NOT NULL,
    [Ultimo] bigint NOT NULL,
    [VenceEn] date NOT NULL,
    [Activa] bit NOT NULL,
    [CajaCodigo] char(2) NOT NULL,
    [SucursalCodigo] char(2) NOT NULL,
    CONSTRAINT [PK_SecuenciasEcf] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [TasasCambio] (
    [Id] int NOT NULL,
    [Moneda] varchar(3) NOT NULL,
    [Tasa] decimal(18,4) NOT NULL,
    [VigenteDesde] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_TasasCambio] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [TiposTarjeta] (
    [Id] int NOT NULL,
    [Codigo] int NOT NULL,
    [Nombre] nvarchar(50) NOT NULL,
    [Activo] bit NOT NULL,
    CONSTRAINT [PK_TiposTarjeta] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [TopesDescuento] (
    [Id] int NOT NULL,
    [Codigo] int NOT NULL,
    [Nivel] int NOT NULL,
    [DepartamentoId] int NULL,
    [CategoriaId] int NULL,
    [MarcaId] int NULL,
    [ArticuloId] int NULL,
    [PorcentajeMaximo] decimal(5,2) NULL,
    [MontoMaximo] decimal(18,2) NULL,
    CONSTRAINT [PK_TopesDescuento] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [UnidadesMedida] (
    [Id] int NOT NULL,
    [Codigo] int NOT NULL,
    [Abreviatura] nvarchar(10) NOT NULL,
    [Nombre] nvarchar(50) NOT NULL,
    [PermiteDecimales] bit NOT NULL,
    [Decimales] int NOT NULL,
    CONSTRAINT [PK_UnidadesMedida] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [DireccionesCliente] (
    [Id] int NOT NULL,
    [ClienteId] int NOT NULL,
    [Alias] nvarchar(50) NOT NULL,
    [Direccion] nvarchar(250) NOT NULL,
    [Sector] nvarchar(100) NULL,
    [Ciudad] nvarchar(100) NULL,
    [Referencia] nvarchar(250) NULL,
    [Telefono] nvarchar(20) NULL,
    [EsPrincipal] bit NOT NULL,
    CONSTRAINT [PK_DireccionesCliente] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DireccionesCliente_Clientes_ClienteId] FOREIGN KEY ([ClienteId]) REFERENCES [Clientes] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Categorias] (
    [Id] int NOT NULL,
    [Codigo] int NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [DepartamentoId] int NOT NULL,
    [Activa] bit NOT NULL,
    CONSTRAINT [PK_Categorias] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Categorias_Departamentos_DepartamentoId] FOREIGN KEY ([DepartamentoId]) REFERENCES [Departamentos] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [HistorialEstadosEcf] (
    [Id] int NOT NULL,
    [DocumentoElectronicoId] int NOT NULL,
    [Estado] int NOT NULL,
    [Fecha] datetimeoffset(3) NOT NULL,
    [Mensaje] nvarchar(500) NULL,
    CONSTRAINT [PK_HistorialEstadosEcf] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_HistorialEstadosEcf_DocumentosElectronicos_DocumentoElectronicoId] FOREIGN KEY ([DocumentoElectronicoId]) REFERENCES [DocumentosElectronicos] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Sucursales] (
    [Id] int NOT NULL,
    [EmpresaId] int NOT NULL,
    [Codigo] char(2) NOT NULL,
    [Nombre] nvarchar(150) NOT NULL,
    [Direccion] nvarchar(250) NULL,
    [Telefono] nvarchar(20) NULL,
    [Activa] bit NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    CONSTRAINT [PK_Sucursales] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Sucursales_Empresas_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [LineasFacturaConsultada] (
    [Id] int NOT NULL,
    [FacturaConsultadaId] int NOT NULL,
    [NumeroLinea] int NOT NULL,
    [ArticuloId] int NOT NULL,
    [CodigoInterno] nvarchar(50) NOT NULL,
    [CodigoLeido] nvarchar(50) NOT NULL,
    [Descripcion] nvarchar(200) NOT NULL,
    [TipoArticulo] int NOT NULL,
    [UnidadMedidaCodigo] nvarchar(20) NOT NULL,
    [DecimalesCantidad] int NOT NULL,
    [Cantidad] decimal(18,4) NOT NULL,
    [ImporteConImpuesto] decimal(18,2) NOT NULL,
    [PorcentajeImpuesto] decimal(9,4) NOT NULL,
    [IndicadorFacturacion] int NOT NULL,
    [EsServicio] bit NOT NULL,
    [Serial] nvarchar(100) NULL,
    [Devuelta] decimal(18,4) NOT NULL,
    CONSTRAINT [PK_LineasFacturaConsultada] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LineasFacturaConsultada_FacturasConsultadas_FacturaConsultadaId] FOREIGN KEY ([FacturaConsultadaId]) REFERENCES [FacturasConsultadas] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [MovimientosPuntos] (
    [Id] int NOT NULL,
    [MiembroId] int NOT NULL,
    [Cedula] char(11) NOT NULL,
    [Tipo] int NOT NULL,
    [Puntos] int NOT NULL,
    [VentaId] int NULL,
    [DevolucionId] int NULL,
    [Documento] varchar(40) NOT NULL,
    [CajaId] int NOT NULL,
    [Fecha] datetimeoffset(3) NOT NULL,
    [VenceEn] date NULL,
    CONSTRAINT [PK_MovimientosPuntos] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MovimientosPuntos_MiembrosFidelidad_MiembroId] FOREIGN KEY ([MiembroId]) REFERENCES [MiembrosFidelidad] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [LineasPendienteEntrega] (
    [Id] int NOT NULL,
    [PendienteEntregaId] int NOT NULL,
    [NumeroLineaVenta] int NOT NULL,
    [ArticuloId] int NOT NULL,
    [CodigoInterno] nvarchar(30) NOT NULL,
    [Descripcion] nvarchar(200) NOT NULL,
    [UnidadMedidaCodigo] nvarchar(10) NOT NULL,
    [DecimalesCantidad] int NOT NULL,
    [Serializado] bit NOT NULL,
    [Cantidad] decimal(18,3) NOT NULL,
    [CantidadEntregada] decimal(18,3) NOT NULL,
    [Serial] nvarchar(50) NULL,
    CONSTRAINT [PK_LineasPendienteEntrega] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LineasPendienteEntrega_PendientesEntrega_PendienteEntregaId] FOREIGN KEY ([PendienteEntregaId]) REFERENCES [PendientesEntrega] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [RolesPermisos] (
    [RolId] int NOT NULL,
    [PermisoCodigo] varchar(100) NOT NULL,
    CONSTRAINT [PK_RolesPermisos] PRIMARY KEY ([RolId], [PermisoCodigo]),
    CONSTRAINT [FK_RolesPermisos_Permisos_PermisoCodigo] FOREIGN KEY ([PermisoCodigo]) REFERENCES [Permisos] ([Codigo]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RolesPermisos_Roles_RolId] FOREIGN KEY ([RolId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Usuarios] (
    [Id] int NOT NULL,
    [Codigo] nvarchar(20) NOT NULL,
    [Nombre] nvarchar(150) NOT NULL,
    [RolId] int NOT NULL,
    [Activo] bit NOT NULL,
    [ClaveHash] varchar(256) NULL,
    [IntentosFallidos] int NOT NULL,
    [BloqueadoHasta] datetimeoffset(3) NULL,
    [UltimoIngresoEn] datetimeoffset(3) NULL,
    CONSTRAINT [PK_Usuarios] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Usuarios_Roles_RolId] FOREIGN KEY ([RolId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Articulos] (
    [Id] int NOT NULL,
    [Codigo] nvarchar(30) NOT NULL,
    [Descripcion] nvarchar(200) NOT NULL,
    [Referencia] nvarchar(50) NULL,
    [DepartamentoId] int NOT NULL,
    [CategoriaId] int NULL,
    [MarcaId] int NULL,
    [UnidadMedidaId] int NOT NULL,
    [ImpuestoId] int NOT NULL,
    [Tipo] int NOT NULL,
    [Costo] decimal(18,4) NULL,
    [PrecioMinimo] decimal(18,4) NULL,
    [CantidadMinimaMayor] decimal(18,4) NULL,
    [PesoEmpaque] decimal(18,4) NULL,
    [RutaImagen] nvarchar(260) NULL,
    [EsServicio] bit NOT NULL,
    [MostrarEnCatalogo] bit NOT NULL,
    [VentaEnPos] bit NOT NULL,
    [Activo] bit NOT NULL,
    [PrecioDetalle] decimal(18,2) NOT NULL,
    [PrecioMayor] decimal(18,2) NULL,
    [PreciosVigentesDesde] datetimeoffset(3) NULL,
    CONSTRAINT [PK_Articulos] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Articulos_Categorias_CategoriaId] FOREIGN KEY ([CategoriaId]) REFERENCES [Categorias] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Articulos_Departamentos_DepartamentoId] FOREIGN KEY ([DepartamentoId]) REFERENCES [Departamentos] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Articulos_Impuestos_ImpuestoId] FOREIGN KEY ([ImpuestoId]) REFERENCES [Impuestos] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Articulos_Marcas_MarcaId] FOREIGN KEY ([MarcaId]) REFERENCES [Marcas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Articulos_UnidadesMedida_UnidadMedidaId] FOREIGN KEY ([UnidadMedidaId]) REFERENCES [UnidadesMedida] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Cajas] (
    [Id] int NOT NULL,
    [SucursalId] int NOT NULL,
    [SucursalCodigo] char(2) NOT NULL,
    [Codigo] char(2) NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [DireccionIp] varchar(45) NOT NULL,
    [Habilitada] bit NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    CONSTRAINT [PK_Cajas] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Cajas_Sucursales_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [Sucursales] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [CodigosArticulo] (
    [ArticuloId] int NOT NULL,
    [Codigo] nvarchar(30) NOT NULL,
    [Tipo] int NOT NULL,
    CONSTRAINT [PK_CodigosArticulo] PRIMARY KEY ([ArticuloId], [Codigo]),
    CONSTRAINT [FK_CodigosArticulo_Articulos_ArticuloId] FOREIGN KEY ([ArticuloId]) REFERENCES [Articulos] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Parametros] (
    [Id] int NOT NULL,
    [Clave] nvarchar(100) NOT NULL,
    [Valor] nvarchar(1000) NOT NULL,
    [Descripcion] nvarchar(250) NULL,
    [SucursalId] int NULL,
    [CajaId] int NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    CONSTRAINT [PK_Parametros] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Parametros_UnSoloAmbito] CHECK ([SucursalId] IS NULL OR [CajaId] IS NULL),
    CONSTRAINT [FK_Parametros_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Parametros_Sucursales_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [Sucursales] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Turnos] (
    [Id] int NOT NULL,
    [CajaId] int NOT NULL,
    [SucursalId] int NOT NULL,
    [SucursalCodigo] varchar(2) NOT NULL,
    [SucursalNombre] nvarchar(150) NOT NULL,
    [CajaCodigo] varchar(2) NOT NULL,
    [Numero] bigint NOT NULL,
    [FechaOperacion] date NOT NULL,
    [UsuarioAperturaId] int NOT NULL,
    [UsuarioAperturaNombre] nvarchar(150) NOT NULL,
    [UsuarioActualId] int NOT NULL,
    [UsuarioActualNombre] nvarchar(150) NOT NULL,
    [FondoInicial] decimal(18,2) NOT NULL,
    [Estado] int NOT NULL,
    [AbiertoEn] datetimeoffset(3) NOT NULL,
    [CerradoEn] datetimeoffset(3) NULL,
    CONSTRAINT [PK_Turnos] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Turnos_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [UsuariosCajas] (
    [UsuarioId] int NOT NULL,
    [CajaId] int NOT NULL,
    CONSTRAINT [PK_UsuariosCajas] PRIMARY KEY ([UsuarioId], [CajaId]),
    CONSTRAINT [FK_UsuariosCajas_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UsuariosCajas_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [CierresTurno] (
    [Id] int NOT NULL,
    [TurnoId] int NOT NULL,
    [CajaId] int NOT NULL,
    [SucursalId] int NOT NULL,
    [SucursalCodigo] varchar(2) NOT NULL,
    [SucursalNombre] nvarchar(150) NOT NULL,
    [CajaCodigo] varchar(2) NOT NULL,
    [TurnoNumero] bigint NOT NULL,
    [Numero] int NOT NULL,
    [FechaOperacion] date NOT NULL,
    [AbiertoEn] datetimeoffset(3) NOT NULL,
    [FondoInicial] decimal(18,2) NOT NULL,
    [FondoEnCuadre] bit NOT NULL,
    [CantidadVentas] int NOT NULL,
    [TotalVentas] decimal(18,2) NOT NULL,
    [TotalRetiros] decimal(18,2) NOT NULL,
    [Moneda] varchar(3) NOT NULL,
    [TotalEsperado] decimal(18,2) NOT NULL,
    [UsuarioId] int NOT NULL,
    [UsuarioNombre] nvarchar(150) NOT NULL,
    [CerradoEn] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_CierresTurno] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CierresTurno_Turnos_TurnoId] FOREIGN KEY ([TurnoId]) REFERENCES [Turnos] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [LotesTarjetas] (
    [Id] int NOT NULL,
    [TurnoId] int NOT NULL,
    [CajaId] int NOT NULL,
    [NumeroLote] varchar(30) NULL,
    [TransaccionesCaja] int NOT NULL,
    [MontoCaja] decimal(18,2) NOT NULL,
    [TransaccionesTerminal] int NOT NULL,
    [MontoTerminal] decimal(18,2) NOT NULL,
    [Diferencia] decimal(18,2) NOT NULL,
    [DetalleDelTerminal] bit NOT NULL,
    [UsuarioNombre] nvarchar(150) NOT NULL,
    [CerradoEn] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_LotesTarjetas] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LotesTarjetas_Turnos_TurnoId] FOREIGN KEY ([TurnoId]) REFERENCES [Turnos] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [MovimientosCaja] (
    [Id] int NOT NULL,
    [TurnoId] int NOT NULL,
    [CajaId] int NOT NULL,
    [Tipo] int NOT NULL,
    [Numero] int NOT NULL,
    [Monto] decimal(18,2) NOT NULL,
    [Moneda] varchar(3) NULL,
    [Motivo] nvarchar(250) NULL,
    [UsuarioId] int NOT NULL,
    [UsuarioNombre] nvarchar(150) NOT NULL,
    [UsuarioAnteriorId] int NULL,
    [UsuarioAnteriorNombre] nvarchar(150) NULL,
    [AutorizadoPorId] int NULL,
    [AutorizadoPorNombre] nvarchar(150) NULL,
    [Fecha] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_MovimientosCaja] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MovimientosCaja_Turnos_TurnoId] FOREIGN KEY ([TurnoId]) REFERENCES [Turnos] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Ventas] (
    [Id] int NOT NULL,
    [NumeroTransaccion] varchar(40) NOT NULL,
    [Secuencia] bigint NOT NULL,
    [SucursalId] int NOT NULL,
    [CajaId] int NOT NULL,
    [TurnoId] int NOT NULL,
    [SucursalCodigo] varchar(2) NOT NULL,
    [SucursalNombre] nvarchar(150) NOT NULL,
    [CajaCodigo] varchar(2) NOT NULL,
    [TurnoNumero] bigint NOT NULL,
    [UsuarioId] int NOT NULL,
    [UsuarioNombre] nvarchar(150) NOT NULL,
    [Moneda] varchar(3) NOT NULL,
    [SimboloMoneda] nvarchar(5) NOT NULL,
    [Estado] int NOT NULL,
    [IniciadaEn] datetimeoffset(3) NOT NULL,
    [ActualizadaEn] datetimeoffset(3) NOT NULL,
    [AnuladaEn] datetimeoffset(3) NULL,
    [MotivoAnulacion] nvarchar(500) NULL,
    [AnuladaPorId] int NULL,
    [AnuladaPorNombre] nvarchar(150) NULL,
    [ClienteId] int NULL,
    [ClienteTipoDocumento] int NULL,
    [ClienteDocumento] varchar(20) NULL,
    [ClienteNombre] nvarchar(150) NULL,
    [TipoComprobante] int NOT NULL,
    [PorcentajeRetencion] decimal(5,2) NOT NULL,
    [CertificacionExencion] nvarchar(50) NULL,
    [LimiteCompra] decimal(18,2) NULL,
    [ListaBodaNumero] varchar(20) NULL,
    [ListaBodaEvento] nvarchar(150) NULL,
    [FidelidadMiembroId] int NULL,
    [FidelidadCedula] varchar(20) NULL,
    [FidelidadNombre] nvarchar(150) NULL,
    [FidelidadNivel] nvarchar(150) NULL,
    [PuntosAcumulados] int NOT NULL,
    [PuntosCanjeados] int NOT NULL,
    [PuestaEnEsperaEn] datetimeoffset(3) NULL,
    [DescuentoFacturaTipo] int NULL,
    [DescuentoFacturaValor] decimal(18,4) NULL,
    [DescuentoFacturaLineas] varchar(2000) NULL,
    [MotivoDescuentoFactura] nvarchar(500) NULL,
    [DescuentoFacturaAutorizadoPorId] int NULL,
    [DescuentoFacturaAutorizadoPorNombre] nvarchar(150) NULL,
    [CobradaEn] datetimeoffset(3) NULL,
    [CobradaPorId] int NULL,
    [CobradaPorNombre] nvarchar(150) NULL,
    [TotalCobrado] decimal(18,2) NULL,
    [Devuelta] decimal(18,2) NOT NULL,
    [RedondeoEfectivo] decimal(18,2) NOT NULL,
    [CotizacionNumero] varchar(20) NULL,
    CONSTRAINT [PK_Ventas] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Ventas_Turnos_TurnoId] FOREIGN KEY ([TurnoId]) REFERENCES [Turnos] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [VentasGuardadas] (
    [Id] int NOT NULL,
    [Referencia] nvarchar(40) NOT NULL,
    [NumeroTransaccion] varchar(40) NOT NULL,
    [Secuencia] bigint NOT NULL,
    [SucursalId] int NOT NULL,
    [CajaId] int NOT NULL,
    [TurnoId] int NOT NULL,
    [SucursalCodigo] varchar(2) NOT NULL,
    [SucursalNombre] nvarchar(150) NOT NULL,
    [CajaCodigo] varchar(2) NOT NULL,
    [TurnoNumero] bigint NOT NULL,
    [UsuarioId] int NOT NULL,
    [UsuarioNombre] nvarchar(150) NOT NULL,
    [Moneda] varchar(3) NOT NULL,
    [SimboloMoneda] nvarchar(5) NOT NULL,
    [Estado] int NOT NULL,
    [IniciadaEn] datetimeoffset(3) NOT NULL,
    [ActualizadaEn] datetimeoffset(3) NOT NULL,
    [AnuladaEn] datetimeoffset(3) NULL,
    [MotivoAnulacion] nvarchar(500) NULL,
    [AnuladaPorId] int NULL,
    [AnuladaPorNombre] nvarchar(150) NULL,
    [ClienteId] int NULL,
    [ClienteTipoDocumento] int NULL,
    [ClienteDocumento] varchar(20) NULL,
    [ClienteNombre] nvarchar(150) NULL,
    [TipoComprobante] int NOT NULL,
    [PorcentajeRetencion] decimal(5,2) NOT NULL,
    [CertificacionExencion] nvarchar(50) NULL,
    [LimiteCompra] decimal(18,2) NULL,
    [ListaBodaNumero] varchar(20) NULL,
    [ListaBodaEvento] nvarchar(150) NULL,
    [FidelidadMiembroId] int NULL,
    [FidelidadCedula] varchar(20) NULL,
    [FidelidadNombre] nvarchar(150) NULL,
    [FidelidadNivel] nvarchar(150) NULL,
    [PuntosAcumulados] int NOT NULL,
    [PuntosCanjeados] int NOT NULL,
    [PuestaEnEsperaEn] datetimeoffset(3) NULL,
    [DescuentoFacturaTipo] int NULL,
    [DescuentoFacturaValor] decimal(18,4) NULL,
    [DescuentoFacturaLineas] varchar(2000) NULL,
    [MotivoDescuentoFactura] nvarchar(500) NULL,
    [DescuentoFacturaAutorizadoPorId] int NULL,
    [DescuentoFacturaAutorizadoPorNombre] nvarchar(150) NULL,
    [CobradaEn] datetimeoffset(3) NULL,
    [CobradaPorId] int NULL,
    [CobradaPorNombre] nvarchar(150) NULL,
    [TotalCobrado] decimal(18,2) NULL,
    [Devuelta] decimal(18,2) NOT NULL,
    [RedondeoEfectivo] decimal(18,2) NOT NULL,
    [CotizacionNumero] varchar(20) NULL,
    CONSTRAINT [PK_VentasGuardadas] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_VentasGuardadas_Turnos_TurnoId] FOREIGN KEY ([TurnoId]) REFERENCES [Turnos] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [VentasTemp] (
    [Id] int NOT NULL,
    [NumeroTransaccion] varchar(40) NOT NULL,
    [Secuencia] bigint NOT NULL,
    [SucursalId] int NOT NULL,
    [CajaId] int NOT NULL,
    [TurnoId] int NOT NULL,
    [SucursalCodigo] varchar(2) NOT NULL,
    [SucursalNombre] nvarchar(150) NOT NULL,
    [CajaCodigo] varchar(2) NOT NULL,
    [TurnoNumero] bigint NOT NULL,
    [UsuarioId] int NOT NULL,
    [UsuarioNombre] nvarchar(150) NOT NULL,
    [Moneda] varchar(3) NOT NULL,
    [SimboloMoneda] nvarchar(5) NOT NULL,
    [Estado] int NOT NULL,
    [IniciadaEn] datetimeoffset(3) NOT NULL,
    [ActualizadaEn] datetimeoffset(3) NOT NULL,
    [AnuladaEn] datetimeoffset(3) NULL,
    [MotivoAnulacion] nvarchar(500) NULL,
    [AnuladaPorId] int NULL,
    [AnuladaPorNombre] nvarchar(150) NULL,
    [ClienteId] int NULL,
    [ClienteTipoDocumento] int NULL,
    [ClienteDocumento] varchar(20) NULL,
    [ClienteNombre] nvarchar(150) NULL,
    [TipoComprobante] int NOT NULL,
    [PorcentajeRetencion] decimal(5,2) NOT NULL,
    [CertificacionExencion] nvarchar(50) NULL,
    [LimiteCompra] decimal(18,2) NULL,
    [ListaBodaNumero] varchar(20) NULL,
    [ListaBodaEvento] nvarchar(150) NULL,
    [FidelidadMiembroId] int NULL,
    [FidelidadCedula] varchar(20) NULL,
    [FidelidadNombre] nvarchar(150) NULL,
    [FidelidadNivel] nvarchar(150) NULL,
    [PuntosAcumulados] int NOT NULL,
    [PuntosCanjeados] int NOT NULL,
    [PuestaEnEsperaEn] datetimeoffset(3) NULL,
    [DescuentoFacturaTipo] int NULL,
    [DescuentoFacturaValor] decimal(18,4) NULL,
    [DescuentoFacturaLineas] varchar(2000) NULL,
    [MotivoDescuentoFactura] nvarchar(500) NULL,
    [DescuentoFacturaAutorizadoPorId] int NULL,
    [DescuentoFacturaAutorizadoPorNombre] nvarchar(150) NULL,
    [CobradaEn] datetimeoffset(3) NULL,
    [CobradaPorId] int NULL,
    [CobradaPorNombre] nvarchar(150) NULL,
    [TotalCobrado] decimal(18,2) NULL,
    [Devuelta] decimal(18,2) NOT NULL,
    [RedondeoEfectivo] decimal(18,2) NOT NULL,
    [CotizacionNumero] varchar(20) NULL,
    CONSTRAINT [PK_VentasTemp] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_VentasTemp_Turnos_TurnoId] FOREIGN KEY ([TurnoId]) REFERENCES [Turnos] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [CierresTurnoFormasPago] (
    [Id] int NOT NULL,
    [CierreTurnoId] int NOT NULL,
    [FormaPagoId] int NOT NULL,
    [Codigo] nvarchar(20) NOT NULL,
    [Nombre] nvarchar(50) NOT NULL,
    [Tipo] int NOT NULL,
    [Moneda] varchar(3) NOT NULL,
    [Orden] int NOT NULL,
    [Transacciones] int NOT NULL,
    [Esperado] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_CierresTurnoFormasPago] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CierresTurnoFormasPago_CierresTurno_CierreTurnoId] FOREIGN KEY ([CierreTurnoId]) REFERENCES [CierresTurno] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [LotesTarjetasAprobaciones] (
    [Id] int NOT NULL,
    [LoteTarjetasId] int NOT NULL,
    [Aprobacion] varchar(30) NOT NULL,
    [Origen] int NOT NULL,
    CONSTRAINT [PK_LotesTarjetasAprobaciones] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LotesTarjetasAprobaciones_LotesTarjetas_LoteTarjetasId] FOREIGN KEY ([LoteTarjetasId]) REFERENCES [LotesTarjetas] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [DestinosEntregaVenta] (
    [Id] int NOT NULL,
    [VentaId] int NOT NULL,
    [Numero] int NOT NULL,
    [Metodo] int NOT NULL,
    [SucursalRetiroId] int NULL,
    [SucursalRetiroNombre] nvarchar(150) NULL,
    [Direccion] nvarchar(250) NULL,
    [Sector] nvarchar(100) NULL,
    [Ciudad] nvarchar(100) NULL,
    [Referencia] nvarchar(250) NULL,
    [Telefono] nvarchar(20) NULL,
    [Transportista] nvarchar(100) NULL,
    [CostoEnvio] decimal(18,2) NULL,
    [FechaComprometida] date NULL,
    [Comentario] nvarchar(250) NULL,
    [AutorizadoPorId] int NULL,
    [AutorizadoPorNombre] nvarchar(150) NULL,
    CONSTRAINT [PK_DestinosEntregaVenta] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DestinosEntregaVenta_Ventas_VentaId] FOREIGN KEY ([VentaId]) REFERENCES [Ventas] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Devoluciones] (
    [Id] int NOT NULL,
    [Numero] varchar(40) NOT NULL,
    [SucursalId] int NOT NULL,
    [CajaId] int NOT NULL,
    [TurnoId] int NULL,
    [SucursalCodigo] varchar(2) NOT NULL,
    [SucursalNombre] nvarchar(150) NOT NULL,
    [CajaCodigo] varchar(2) NOT NULL,
    [TurnoNumero] bigint NULL,
    [UsuarioId] int NOT NULL,
    [UsuarioNombre] nvarchar(150) NOT NULL,
    [VentaOrigenId] int NULL,
    [VentaOrigenNumero] varchar(40) NOT NULL,
    [VentaOrigenCobradaEn] datetimeoffset(3) NOT NULL,
    [TipoComprobanteOrigen] int NOT NULL,
    [EncfOrigen] varchar(13) NULL,
    [ClienteTipoDocumento] int NULL,
    [ClienteDocumento] varchar(20) NOT NULL,
    [ClienteNombre] nvarchar(150) NOT NULL,
    [MotivoCodigo] int NOT NULL,
    [MotivoNombre] nvarchar(100) NOT NULL,
    [Observacion] nvarchar(250) NULL,
    [AutorizadoPorId] int NULL,
    [AutorizadoPorNombre] nvarchar(150) NULL,
    [RetieneImpuesto] bit NOT NULL,
    [EsInterna] bit NOT NULL,
    [EsTotal] bit NOT NULL,
    [Subtotal] decimal(18,2) NOT NULL,
    [Impuesto] decimal(18,2) NOT NULL,
    [ImpuestoRetenido] decimal(18,2) NOT NULL,
    [Total] decimal(18,2) NOT NULL,
    [Saldo] decimal(18,2) NOT NULL,
    [Moneda] varchar(3) NOT NULL,
    [SimboloMoneda] nvarchar(5) NOT NULL,
    [PuntosReversados] int NOT NULL,
    [FechaEmision] date NOT NULL,
    [CreadaEn] datetimeoffset(3) NOT NULL,
    [Encf] varchar(13) NULL,
    [Reembolso] int NOT NULL,
    [ReembolsoReferencia] nvarchar(256) NULL,
    [ReembolsoDetalle] nvarchar(256) NULL,
    [ReembolsadaEn] datetimeoffset(3) NULL,
    CONSTRAINT [PK_Devoluciones] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Devoluciones_Ventas_VentaOrigenId] FOREIGN KEY ([VentaOrigenId]) REFERENCES [Ventas] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [LineasVenta] (
    [Id] int NOT NULL,
    [Serial] nvarchar(50) NULL,
    [SerialPendiente] bit NOT NULL,
    [PromocionId] int NULL,
    [PromocionCodigo] nvarchar(30) NULL,
    [PromocionNombre] nvarchar(150) NULL,
    [PromocionDescripcion] nvarchar(60) NULL,
    [DescuentoPromocion] decimal(18,2) NOT NULL,
    [PromocionDesactivada] bit NOT NULL,
    [DescuentoManual] decimal(18,2) NOT NULL,
    [DescuentoManualTipo] int NULL,
    [DescuentoManualValor] decimal(18,4) NULL,
    [MotivoDescuento] nvarchar(500) NULL,
    [DescuentoAutorizadoPorId] int NULL,
    [DescuentoAutorizadoPorNombre] nvarchar(150) NULL,
    [DescuentoFactura] decimal(18,2) NOT NULL,
    [VentaId] int NOT NULL,
    [NumeroLinea] int NOT NULL,
    [ArticuloId] int NOT NULL,
    [CodigoInterno] nvarchar(30) NOT NULL,
    [CodigoLeido] nvarchar(30) NOT NULL,
    [Descripcion] nvarchar(200) NOT NULL,
    [TipoArticulo] int NOT NULL,
    [DepartamentoId] int NOT NULL,
    [CategoriaId] int NULL,
    [MarcaId] int NULL,
    [PermiteDescuentoManual] bit NOT NULL,
    [UnidadMedidaCodigo] nvarchar(10) NOT NULL,
    [PermiteDecimales] bit NOT NULL,
    [DecimalesCantidad] int NOT NULL,
    [ImpuestoId] int NOT NULL,
    [PorcentajeImpuesto] decimal(5,2) NOT NULL,
    [IndicadorFacturacion] int NOT NULL,
    [PorcentajeImpuestoGravado] decimal(18,4) NULL,
    [IndicadorFacturacionGravado] int NULL,
    [EsServicio] bit NOT NULL,
    [PrecioDetalle] decimal(18,4) NOT NULL,
    [PrecioMayor] decimal(18,4) NULL,
    [CantidadMinimaMayor] decimal(18,4) NULL,
    [PrecioMinimo] decimal(18,4) NULL,
    [Cantidad] decimal(18,4) NOT NULL,
    [PrecioUnitario] decimal(18,4) NOT NULL,
    [Lista] int NOT NULL,
    [MotivoPrecio] int NOT NULL,
    [ImporteEtiqueta] decimal(18,4) NULL,
    [LeidaDeBalanza] bit NOT NULL,
    [Anulada] bit NOT NULL,
    [AnuladaEn] datetimeoffset(3) NULL,
    CONSTRAINT [PK_LineasVenta] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LineasVenta_Ventas_VentaId] FOREIGN KEY ([VentaId]) REFERENCES [Ventas] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [PagosVenta] (
    [Id] int NOT NULL,
    [VentaId] int NOT NULL,
    [Numero] int NOT NULL,
    [FormaPagoId] int NOT NULL,
    [FormaPagoCodigo] nvarchar(20) NOT NULL,
    [FormaPagoNombre] nvarchar(50) NOT NULL,
    [Tipo] int NOT NULL,
    [Moneda] varchar(3) NOT NULL,
    [MontoRecibido] decimal(18,2) NOT NULL,
    [TasaCambio] decimal(18,4) NULL,
    [MontoAplicado] decimal(18,2) NOT NULL,
    [Referencia] nvarchar(60) NULL,
    [BancoId] int NULL,
    [BancoNombre] nvarchar(100) NULL,
    [TipoTarjetaId] int NULL,
    [TipoTarjetaNombre] nvarchar(100) NULL,
    [UltimosDigitos] varchar(4) NULL,
    [AprobacionManual] bit NOT NULL,
    [ParaConciliar] bit NOT NULL,
    [OperacionTerminalId] int NULL,
    [PermiteDevuelta] bit NOT NULL,
    CONSTRAINT [PK_PagosVenta] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PagosVenta_Ventas_VentaId] FOREIGN KEY ([VentaId]) REFERENCES [Ventas] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [DestinosEntregaVentaGuardadas] (
    [Id] int NOT NULL,
    [VentaId] int NOT NULL,
    [Numero] int NOT NULL,
    [Metodo] int NOT NULL,
    [SucursalRetiroId] int NULL,
    [SucursalRetiroNombre] nvarchar(150) NULL,
    [Direccion] nvarchar(250) NULL,
    [Sector] nvarchar(100) NULL,
    [Ciudad] nvarchar(100) NULL,
    [Referencia] nvarchar(250) NULL,
    [Telefono] nvarchar(20) NULL,
    [Transportista] nvarchar(100) NULL,
    [CostoEnvio] decimal(18,2) NULL,
    [FechaComprometida] date NULL,
    [Comentario] nvarchar(250) NULL,
    [AutorizadoPorId] int NULL,
    [AutorizadoPorNombre] nvarchar(150) NULL,
    CONSTRAINT [PK_DestinosEntregaVentaGuardadas] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DestinosEntregaVentaGuardadas_VentasGuardadas_VentaId] FOREIGN KEY ([VentaId]) REFERENCES [VentasGuardadas] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [LineasVentaGuardadas] (
    [Id] int NOT NULL,
    [Serial] nvarchar(50) NULL,
    [SerialPendiente] bit NOT NULL,
    [PromocionId] int NULL,
    [PromocionCodigo] nvarchar(30) NULL,
    [PromocionNombre] nvarchar(150) NULL,
    [PromocionDescripcion] nvarchar(60) NULL,
    [DescuentoPromocion] decimal(18,2) NOT NULL,
    [PromocionDesactivada] bit NOT NULL,
    [DescuentoManual] decimal(18,2) NOT NULL,
    [DescuentoManualTipo] int NULL,
    [DescuentoManualValor] decimal(18,4) NULL,
    [MotivoDescuento] nvarchar(500) NULL,
    [DescuentoAutorizadoPorId] int NULL,
    [DescuentoAutorizadoPorNombre] nvarchar(150) NULL,
    [DescuentoFactura] decimal(18,2) NOT NULL,
    [VentaId] int NOT NULL,
    [NumeroLinea] int NOT NULL,
    [ArticuloId] int NOT NULL,
    [CodigoInterno] nvarchar(30) NOT NULL,
    [CodigoLeido] nvarchar(30) NOT NULL,
    [Descripcion] nvarchar(200) NOT NULL,
    [TipoArticulo] int NOT NULL,
    [DepartamentoId] int NOT NULL,
    [CategoriaId] int NULL,
    [MarcaId] int NULL,
    [PermiteDescuentoManual] bit NOT NULL,
    [UnidadMedidaCodigo] nvarchar(10) NOT NULL,
    [PermiteDecimales] bit NOT NULL,
    [DecimalesCantidad] int NOT NULL,
    [ImpuestoId] int NOT NULL,
    [PorcentajeImpuesto] decimal(5,2) NOT NULL,
    [IndicadorFacturacion] int NOT NULL,
    [PorcentajeImpuestoGravado] decimal(18,4) NULL,
    [IndicadorFacturacionGravado] int NULL,
    [EsServicio] bit NOT NULL,
    [PrecioDetalle] decimal(18,4) NOT NULL,
    [PrecioMayor] decimal(18,4) NULL,
    [CantidadMinimaMayor] decimal(18,4) NULL,
    [PrecioMinimo] decimal(18,4) NULL,
    [Cantidad] decimal(18,4) NOT NULL,
    [PrecioUnitario] decimal(18,4) NOT NULL,
    [Lista] int NOT NULL,
    [MotivoPrecio] int NOT NULL,
    [ImporteEtiqueta] decimal(18,4) NULL,
    [LeidaDeBalanza] bit NOT NULL,
    [Anulada] bit NOT NULL,
    [AnuladaEn] datetimeoffset(3) NULL,
    CONSTRAINT [PK_LineasVentaGuardadas] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LineasVentaGuardadas_VentasGuardadas_VentaId] FOREIGN KEY ([VentaId]) REFERENCES [VentasGuardadas] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [DestinosEntregaVentaTemp] (
    [Id] int NOT NULL,
    [VentaId] int NOT NULL,
    [Numero] int NOT NULL,
    [Metodo] int NOT NULL,
    [SucursalRetiroId] int NULL,
    [SucursalRetiroNombre] nvarchar(150) NULL,
    [Direccion] nvarchar(250) NULL,
    [Sector] nvarchar(100) NULL,
    [Ciudad] nvarchar(100) NULL,
    [Referencia] nvarchar(250) NULL,
    [Telefono] nvarchar(20) NULL,
    [Transportista] nvarchar(100) NULL,
    [CostoEnvio] decimal(18,2) NULL,
    [FechaComprometida] date NULL,
    [Comentario] nvarchar(250) NULL,
    [AutorizadoPorId] int NULL,
    [AutorizadoPorNombre] nvarchar(150) NULL,
    CONSTRAINT [PK_DestinosEntregaVentaTemp] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DestinosEntregaVentaTemp_VentasTemp_VentaId] FOREIGN KEY ([VentaId]) REFERENCES [VentasTemp] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [LineasVentaTemp] (
    [Id] int NOT NULL,
    [Serial] nvarchar(50) NULL,
    [SerialPendiente] bit NOT NULL,
    [PromocionId] int NULL,
    [PromocionCodigo] nvarchar(30) NULL,
    [PromocionNombre] nvarchar(150) NULL,
    [PromocionDescripcion] nvarchar(60) NULL,
    [DescuentoPromocion] decimal(18,2) NOT NULL,
    [PromocionDesactivada] bit NOT NULL,
    [DescuentoManual] decimal(18,2) NOT NULL,
    [DescuentoManualTipo] int NULL,
    [DescuentoManualValor] decimal(18,4) NULL,
    [MotivoDescuento] nvarchar(500) NULL,
    [DescuentoAutorizadoPorId] int NULL,
    [DescuentoAutorizadoPorNombre] nvarchar(150) NULL,
    [DescuentoFactura] decimal(18,2) NOT NULL,
    [VentaId] int NOT NULL,
    [NumeroLinea] int NOT NULL,
    [ArticuloId] int NOT NULL,
    [CodigoInterno] nvarchar(30) NOT NULL,
    [CodigoLeido] nvarchar(30) NOT NULL,
    [Descripcion] nvarchar(200) NOT NULL,
    [TipoArticulo] int NOT NULL,
    [DepartamentoId] int NOT NULL,
    [CategoriaId] int NULL,
    [MarcaId] int NULL,
    [PermiteDescuentoManual] bit NOT NULL,
    [UnidadMedidaCodigo] nvarchar(10) NOT NULL,
    [PermiteDecimales] bit NOT NULL,
    [DecimalesCantidad] int NOT NULL,
    [ImpuestoId] int NOT NULL,
    [PorcentajeImpuesto] decimal(5,2) NOT NULL,
    [IndicadorFacturacion] int NOT NULL,
    [PorcentajeImpuestoGravado] decimal(18,4) NULL,
    [IndicadorFacturacionGravado] int NULL,
    [EsServicio] bit NOT NULL,
    [PrecioDetalle] decimal(18,4) NOT NULL,
    [PrecioMayor] decimal(18,4) NULL,
    [CantidadMinimaMayor] decimal(18,4) NULL,
    [PrecioMinimo] decimal(18,4) NULL,
    [Cantidad] decimal(18,4) NOT NULL,
    [PrecioUnitario] decimal(18,4) NOT NULL,
    [Lista] int NOT NULL,
    [MotivoPrecio] int NOT NULL,
    [ImporteEtiqueta] decimal(18,4) NULL,
    [LeidaDeBalanza] bit NOT NULL,
    [Anulada] bit NOT NULL,
    [AnuladaEn] datetimeoffset(3) NULL,
    CONSTRAINT [PK_LineasVentaTemp] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LineasVentaTemp_VentasTemp_VentaId] FOREIGN KEY ([VentaId]) REFERENCES [VentasTemp] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [LineasDestinoEntrega] (
    [Id] int NOT NULL,
    [DestinoEntregaId] int NOT NULL,
    [NumeroLinea] int NOT NULL,
    [Cantidad] decimal(18,3) NOT NULL,
    CONSTRAINT [PK_LineasDestinoEntrega] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LineasDestinoEntrega_DestinosEntregaVenta_DestinoEntregaId] FOREIGN KEY ([DestinoEntregaId]) REFERENCES [DestinosEntregaVenta] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ConsumosNotaCredito] (
    [Id] int NOT NULL,
    [DevolucionId] int NOT NULL,
    [VentaId] int NOT NULL,
    [VentaNumero] varchar(40) NOT NULL,
    [CajaId] int NOT NULL,
    [Monto] decimal(18,2) NOT NULL,
    [SaldoRestante] decimal(18,2) NOT NULL,
    [Fecha] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_ConsumosNotaCredito] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ConsumosNotaCredito_Devoluciones_DevolucionId] FOREIGN KEY ([DevolucionId]) REFERENCES [Devoluciones] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [LineasDevolucion] (
    [Id] int NOT NULL,
    [DevolucionId] int NOT NULL,
    [NumeroLineaOrigen] int NOT NULL,
    [ArticuloId] int NOT NULL,
    [CodigoInterno] nvarchar(50) NOT NULL,
    [CodigoLeido] nvarchar(50) NOT NULL,
    [Descripcion] nvarchar(200) NOT NULL,
    [UnidadMedidaCodigo] nvarchar(20) NOT NULL,
    [DecimalesCantidad] int NOT NULL,
    [Cantidad] decimal(18,4) NOT NULL,
    [PrecioUnitario] decimal(18,2) NOT NULL,
    [PorcentajeImpuesto] decimal(9,4) NOT NULL,
    [IndicadorFacturacion] int NOT NULL,
    [EsServicio] bit NOT NULL,
    [ImporteFactura] decimal(18,2) NOT NULL,
    [Base] decimal(18,2) NOT NULL,
    [Impuesto] decimal(18,2) NOT NULL,
    [ImpuestoRetenido] decimal(18,2) NOT NULL,
    [Importe] decimal(18,2) NOT NULL,
    [Serial] nvarchar(100) NULL,
    CONSTRAINT [PK_LineasDevolucion] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LineasDevolucion_Devoluciones_DevolucionId] FOREIGN KEY ([DevolucionId]) REFERENCES [Devoluciones] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [LineasDestinoEntregaGuardadas] (
    [Id] int NOT NULL,
    [DestinoEntregaId] int NOT NULL,
    [NumeroLinea] int NOT NULL,
    [Cantidad] decimal(18,3) NOT NULL,
    CONSTRAINT [PK_LineasDestinoEntregaGuardadas] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LineasDestinoEntregaGuardadas_DestinosEntregaVentaGuardadas_DestinoEntregaId] FOREIGN KEY ([DestinoEntregaId]) REFERENCES [DestinosEntregaVentaGuardadas] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [LineasDestinoEntregaTemp] (
    [Id] int NOT NULL,
    [DestinoEntregaId] int NOT NULL,
    [NumeroLinea] int NOT NULL,
    [Cantidad] decimal(18,3) NOT NULL,
    CONSTRAINT [PK_LineasDestinoEntregaTemp] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LineasDestinoEntregaTemp_DestinosEntregaVentaTemp_DestinoEntregaId] FOREIGN KEY ([DestinoEntregaId]) REFERENCES [DestinosEntregaVentaTemp] ([Id]) ON DELETE CASCADE
);
GO


CREATE INDEX [IX_Articulos_CategoriaId] ON [Articulos] ([CategoriaId]);
GO


CREATE UNIQUE INDEX [IX_Articulos_Codigo] ON [Articulos] ([Codigo]);
GO


CREATE INDEX [IX_Articulos_DepartamentoId] ON [Articulos] ([DepartamentoId]);
GO


CREATE INDEX [IX_Articulos_Descripcion] ON [Articulos] ([Descripcion]);
GO


CREATE INDEX [IX_Articulos_ImpuestoId] ON [Articulos] ([ImpuestoId]);
GO


CREATE INDEX [IX_Articulos_MarcaId] ON [Articulos] ([MarcaId]);
GO


CREATE INDEX [IX_Articulos_UnidadMedidaId] ON [Articulos] ([UnidadMedidaId]);
GO


CREATE INDEX [IX_Auditoria_Accion] ON [Auditoria] ([Accion]);
GO


CREATE INDEX [IX_Auditoria_OcurridoEn] ON [Auditoria] ([OcurridoEn]);
GO


CREATE INDEX [IX_Auditoria_TipoEntidad_EntidadId] ON [Auditoria] ([TipoEntidad], [EntidadId]);
GO


CREATE INDEX [IX_AutorizacionesOtorgadas_SolicitanteId_ConcedidaEn] ON [AutorizacionesOtorgadas] ([SolicitanteId], [ConcedidaEn]);
GO


CREATE UNIQUE INDEX [IX_Bancos_Codigo] ON [Bancos] ([Codigo]);
GO


CREATE INDEX [IX_BandejaSalida_Estado_ProximoIntentoEn] ON [BandejaSalida] ([Estado], [ProximoIntentoEn]);
GO


CREATE INDEX [IX_BandejaSalida_Referencia] ON [BandejaSalida] ([Referencia]);
GO


CREATE UNIQUE INDEX [IX_Cajas_SucursalId_Codigo] ON [Cajas] ([SucursalId], [Codigo]);
GO


CREATE UNIQUE INDEX [IX_Categorias_Codigo] ON [Categorias] ([Codigo]);
GO


CREATE INDEX [IX_Categorias_DepartamentoId] ON [Categorias] ([DepartamentoId]);
GO


CREATE INDEX [IX_CierresTurno_CajaId_CerradoEn] ON [CierresTurno] ([CajaId], [CerradoEn]);
GO


CREATE UNIQUE INDEX [IX_CierresTurno_TurnoId_Numero] ON [CierresTurno] ([TurnoId], [Numero]);
GO


CREATE INDEX [IX_CierresTurnoFormasPago_CierreTurnoId] ON [CierresTurnoFormasPago] ([CierreTurnoId]);
GO


CREATE UNIQUE INDEX [IX_Clientes_Codigo] ON [Clientes] ([Codigo]);
GO


CREATE INDEX [IX_Clientes_Documento] ON [Clientes] ([Documento]);
GO


CREATE INDEX [IX_Clientes_Nombre] ON [Clientes] ([Nombre]);
GO


CREATE UNIQUE INDEX [IX_Clientes_TipoDocumento_Documento] ON [Clientes] ([TipoDocumento], [Documento]);
GO


CREATE UNIQUE INDEX [IX_CodigosArticulo_Codigo] ON [CodigosArticulo] ([Codigo]);
GO


CREATE INDEX [IX_ConsumosNotaCredito_DevolucionId] ON [ConsumosNotaCredito] ([DevolucionId]);
GO


CREATE INDEX [IX_ConsumosNotaCredito_VentaId] ON [ConsumosNotaCredito] ([VentaId]);
GO


CREATE UNIQUE INDEX [IX_Denominaciones_Moneda_Valor_Tipo] ON [Denominaciones] ([Moneda], [Valor], [Tipo]);
GO


CREATE UNIQUE INDEX [IX_Departamentos_Codigo] ON [Departamentos] ([Codigo]);
GO


CREATE UNIQUE INDEX [IX_DescuentosTarjeta_Codigo] ON [DescuentosTarjeta] ([Codigo]);
GO


CREATE UNIQUE INDEX [IX_DestinosEntregaVenta_VentaId_Numero] ON [DestinosEntregaVenta] ([VentaId], [Numero]);
GO


CREATE UNIQUE INDEX [IX_DestinosEntregaVentaGuardadas_VentaId_Numero] ON [DestinosEntregaVentaGuardadas] ([VentaId], [Numero]);
GO


CREATE UNIQUE INDEX [IX_DestinosEntregaVentaTemp_VentaId_Numero] ON [DestinosEntregaVentaTemp] ([VentaId], [Numero]);
GO


CREATE UNIQUE INDEX [IX_Devoluciones_CajaId_Numero] ON [Devoluciones] ([CajaId], [Numero]);
GO


CREATE UNIQUE INDEX [IX_Devoluciones_Encf] ON [Devoluciones] ([Encf]) WHERE [Encf] IS NOT NULL;
GO


CREATE INDEX [IX_Devoluciones_VentaOrigenId] ON [Devoluciones] ([VentaOrigenId]);
GO


CREATE UNIQUE INDEX [IX_DireccionesCliente_ClienteId_Alias] ON [DireccionesCliente] ([ClienteId], [Alias]);
GO


CREATE INDEX [IX_DocumentosElectronicos_CajaId_Estado] ON [DocumentosElectronicos] ([CajaId], [Estado]);
GO


CREATE UNIQUE INDEX [IX_DocumentosElectronicos_Encf] ON [DocumentosElectronicos] ([Encf]);
GO


CREATE UNIQUE INDEX [IX_DocumentosElectronicos_VentaId_TipoOrigen] ON [DocumentosElectronicos] ([VentaId], [TipoOrigen]);
GO


CREATE UNIQUE INDEX [IX_Empresas_Rnc] ON [Empresas] ([Rnc]);
GO


CREATE UNIQUE INDEX [IX_FacturasConsultadas_Numero] ON [FacturasConsultadas] ([Numero]);
GO


CREATE UNIQUE INDEX [IX_FormasPago_Codigo] ON [FormasPago] ([Codigo]);
GO


CREATE INDEX [IX_HistorialEstadosEcf_DocumentoElectronicoId] ON [HistorialEstadosEcf] ([DocumentoElectronicoId]);
GO


CREATE UNIQUE INDEX [IX_Impuestos_Codigo] ON [Impuestos] ([Codigo]);
GO


CREATE INDEX [IX_LineasDestinoEntrega_DestinoEntregaId] ON [LineasDestinoEntrega] ([DestinoEntregaId]);
GO


CREATE INDEX [IX_LineasDestinoEntregaGuardadas_DestinoEntregaId] ON [LineasDestinoEntregaGuardadas] ([DestinoEntregaId]);
GO


CREATE INDEX [IX_LineasDestinoEntregaTemp_DestinoEntregaId] ON [LineasDestinoEntregaTemp] ([DestinoEntregaId]);
GO


CREATE INDEX [IX_LineasDevolucion_DevolucionId] ON [LineasDevolucion] ([DevolucionId]);
GO


CREATE INDEX [IX_LineasFacturaConsultada_FacturaConsultadaId] ON [LineasFacturaConsultada] ([FacturaConsultadaId]);
GO


CREATE INDEX [IX_LineasPendienteEntrega_PendienteEntregaId] ON [LineasPendienteEntrega] ([PendienteEntregaId]);
GO


CREATE INDEX [IX_LineasVenta_PromocionId] ON [LineasVenta] ([PromocionId]);
GO


CREATE UNIQUE INDEX [IX_LineasVenta_VentaId_NumeroLinea] ON [LineasVenta] ([VentaId], [NumeroLinea]);
GO


CREATE INDEX [IX_LineasVentaGuardadas_PromocionId] ON [LineasVentaGuardadas] ([PromocionId]);
GO


CREATE UNIQUE INDEX [IX_LineasVentaGuardadas_VentaId_NumeroLinea] ON [LineasVentaGuardadas] ([VentaId], [NumeroLinea]);
GO


CREATE INDEX [IX_LineasVentaTemp_PromocionId] ON [LineasVentaTemp] ([PromocionId]);
GO


CREATE UNIQUE INDEX [IX_LineasVentaTemp_VentaId_NumeroLinea] ON [LineasVentaTemp] ([VentaId], [NumeroLinea]);
GO


CREATE UNIQUE INDEX [IX_LotesTarjetas_TurnoId] ON [LotesTarjetas] ([TurnoId]);
GO


CREATE INDEX [IX_LotesTarjetasAprobaciones_LoteTarjetasId] ON [LotesTarjetasAprobaciones] ([LoteTarjetasId]);
GO


CREATE UNIQUE INDEX [IX_Marcas_Codigo] ON [Marcas] ([Codigo]);
GO


CREATE UNIQUE INDEX [IX_MiembrosFidelidad_Cedula] ON [MiembrosFidelidad] ([Cedula]);
GO


CREATE UNIQUE INDEX [IX_Monedas_Codigo] ON [Monedas] ([Codigo]);
GO


CREATE UNIQUE INDEX [IX_MotivosDescuento_Codigo] ON [MotivosDescuento] ([Codigo]);
GO


CREATE UNIQUE INDEX [IX_MotivosDevolucion_Codigo] ON [MotivosDevolucion] ([Codigo]);
GO


CREATE UNIQUE INDEX [IX_MovimientosCaja_TurnoId_Tipo_Numero] ON [MovimientosCaja] ([TurnoId], [Tipo], [Numero]);
GO


CREATE INDEX [IX_MovimientosPuntos_MiembroId_Fecha] ON [MovimientosPuntos] ([MiembroId], [Fecha]);
GO


CREATE INDEX [IX_MovimientosPuntos_VentaId] ON [MovimientosPuntos] ([VentaId]);
GO


CREATE UNIQUE INDEX [IX_NivelesFidelidad_Codigo] ON [NivelesFidelidad] ([Codigo]);
GO


CREATE INDEX [IX_OperacionesTerminal_CajaId_TurnoId_Fecha] ON [OperacionesTerminal] ([CajaId], [TurnoId], [Fecha]);
GO


CREATE INDEX [IX_OperacionesTerminal_VentaId] ON [OperacionesTerminal] ([VentaId]);
GO


CREATE INDEX [IX_PagosVenta_OperacionTerminalId] ON [PagosVenta] ([OperacionTerminalId]);
GO


CREATE UNIQUE INDEX [IX_PagosVenta_VentaId_Numero] ON [PagosVenta] ([VentaId], [Numero]);
GO


CREATE INDEX [IX_Parametros_CajaId] ON [Parametros] ([CajaId]);
GO


CREATE UNIQUE INDEX [IX_Parametros_Clave_SucursalId_CajaId] ON [Parametros] ([Clave], [SucursalId], [CajaId]);
GO


CREATE INDEX [IX_Parametros_SucursalId] ON [Parametros] ([SucursalId]);
GO


CREATE INDEX [IX_PendientesEntrega_Estado_CreadoEn] ON [PendientesEntrega] ([Estado], [CreadoEn]);
GO


CREATE UNIQUE INDEX [IX_PendientesEntrega_Numero] ON [PendientesEntrega] ([Numero]);
GO


CREATE INDEX [IX_PendientesEntrega_VentaId] ON [PendientesEntrega] ([VentaId]);
GO


CREATE INDEX [IX_Promociones_Activa_VigenteHasta] ON [Promociones] ([Activa], [VigenteHasta]);
GO


CREATE UNIQUE INDEX [IX_Promociones_Codigo] ON [Promociones] ([Codigo]);
GO


CREATE UNIQUE INDEX [IX_ReglasAcumulacion_Codigo] ON [ReglasAcumulacion] ([Codigo]);
GO


CREATE UNIQUE INDEX [IX_Roles_Codigo] ON [Roles] ([Codigo]);
GO


CREATE INDEX [IX_RolesPermisos_PermisoCodigo] ON [RolesPermisos] ([PermisoCodigo]);
GO


CREATE INDEX [IX_SecuenciasEcf_CajaId_TipoComprobante_Activa_Desde] ON [SecuenciasEcf] ([CajaId], [TipoComprobante], [Activa], [Desde]);
GO


CREATE UNIQUE INDEX [IX_SecuenciasEcf_TipoComprobante_Desde] ON [SecuenciasEcf] ([TipoComprobante], [Desde]);
GO


CREATE UNIQUE INDEX [IX_Sucursales_EmpresaId_Codigo] ON [Sucursales] ([EmpresaId], [Codigo]);
GO


CREATE UNIQUE INDEX [IX_TasasCambio_Moneda_VigenteDesde] ON [TasasCambio] ([Moneda], [VigenteDesde]);
GO


CREATE UNIQUE INDEX [IX_TiposTarjeta_Codigo] ON [TiposTarjeta] ([Codigo]);
GO


CREATE UNIQUE INDEX [IX_TopesDescuento_Codigo] ON [TopesDescuento] ([Codigo]);
GO


CREATE INDEX [IX_TopesDescuento_Nivel_DepartamentoId_ArticuloId] ON [TopesDescuento] ([Nivel], [DepartamentoId], [ArticuloId]);
GO


CREATE UNIQUE INDEX [IX_Turnos_CajaId_Abierto] ON [Turnos] ([CajaId]) WHERE [Estado] = 0;
GO


CREATE UNIQUE INDEX [IX_Turnos_CajaId_Numero] ON [Turnos] ([CajaId], [Numero]);
GO


CREATE UNIQUE INDEX [IX_UnidadesMedida_Codigo] ON [UnidadesMedida] ([Codigo]);
GO


CREATE UNIQUE INDEX [IX_Usuarios_Codigo] ON [Usuarios] ([Codigo]);
GO


CREATE INDEX [IX_Usuarios_RolId] ON [Usuarios] ([RolId]);
GO


CREATE INDEX [IX_UsuariosCajas_CajaId] ON [UsuariosCajas] ([CajaId]);
GO


CREATE UNIQUE INDEX [IX_Ventas_CajaId_Secuencia] ON [Ventas] ([CajaId], [Secuencia]);
GO


CREATE UNIQUE INDEX [IX_Ventas_NumeroTransaccion] ON [Ventas] ([NumeroTransaccion]);
GO


CREATE INDEX [IX_Ventas_TurnoId_Estado] ON [Ventas] ([TurnoId], [Estado]);
GO


CREATE INDEX [IX_Ventas_TurnoId_UsuarioId_Estado] ON [Ventas] ([TurnoId], [UsuarioId], [Estado]);
GO


CREATE INDEX [IX_VentasGuardadas_TurnoId_Estado] ON [VentasGuardadas] ([TurnoId], [Estado]);
GO


CREATE UNIQUE INDEX [IX_VentasGuardadas_TurnoId_Referencia] ON [VentasGuardadas] ([TurnoId], [Referencia]);
GO


CREATE INDEX [IX_VentasGuardadas_TurnoId_UsuarioId_Estado] ON [VentasGuardadas] ([TurnoId], [UsuarioId], [Estado]);
GO


CREATE INDEX [IX_VentasTemp_TurnoId_Estado] ON [VentasTemp] ([TurnoId], [Estado]);
GO


CREATE INDEX [IX_VentasTemp_TurnoId_UsuarioId_Estado] ON [VentasTemp] ([TurnoId], [UsuarioId], [Estado]);
GO

PRINT 'Base de la caja creada. Al abrirla le pedirá su sucursal, su caja, su IP, el servidor y la credencial.';
GO
