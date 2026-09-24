/*
    CG-POS · Base de datos del Central
    Estructura completa del servidor corporativo y el usuario administrador.

    Cómo usarlo:
        sqlcmd -S .\SQLEXPRESS -E -i estructura_base_datos_central.sql
    o ábralo en SQL Server Management Studio y ejecútelo.

    Crea la base «CgPosCentral» si no existe, la secuencia de Id, todas las tablas con sus llaves,
    índices y restricciones. Volver a ejecutarlo sobre una base que ya tiene las tablas da error:
    es para crear la base desde cero.

    Generado desde el modelo del sistema con scripts/base-datos/generar-estructura-sql.py.
    No editar a mano: se edita el modelo y se vuelve a generar.
*/

IF DB_ID(N'CgPosCentral') IS NULL
BEGIN
    PRINT 'Creando la base CgPosCentral...';
    EXEC (N'CREATE DATABASE [CgPosCentral]');
END
GO

ALTER DATABASE [CgPosCentral] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO

USE [CgPosCentral];
GO

/* Los índices filtrados y las restricciones exigen estas opciones; sqlcmd las trae apagadas. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

CREATE SEQUENCE [SecuenciaAccesosUsuarioCaja] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaAjustesCierreTurno] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaAnulacionesEcf] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaArticulos] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaArticulosListaBoda] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaAuditoria] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaBancos] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaCajas] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaCategorias] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaCierresFormaPago] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaCierresSucursal] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaCierresSucursalFormaPago] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaCierresTurno] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaCierresTurnoDenominaciones] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaCierresTurnoLote] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaCierresTurnoLoteAprobaciones] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaCierresTurnoMovimientos] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaClientes] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaComprasListaBoda] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaComprobantesRecibidos] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaConflictosSincronizacion] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaConsumosNotaCredito] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaCotizaciones] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaCredencialesDispositivo] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaDenominaciones] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaDepartamentos] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaDepositosCierreSucursal] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaDescuentosTarjeta] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaDireccionesCliente] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaDocumentosRecibidos] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaEmpresas] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaEntregasPendiente] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaFormasPago] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaImpuestos] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaImpuestosVenta] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaLineasCotizacion] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaLineasEntregaPendiente] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaLineasPendienteEntrega] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaLineasReservaFactura] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaLineasVenta] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaListasBoda] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
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


CREATE SEQUENCE [SecuenciaMotivosSuspension] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaMovimientosPuntos] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaNivelesFidelidad] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaNotasCredito] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
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


CREATE SEQUENCE [SecuenciaReservasFactura] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaReservasNotaCredito] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaRolesCaja] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaRolesCentral] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaSecuenciasEcf] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaSucursales] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaSuspensionesCaja] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaTasasCambio] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaTiposTarjeta] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaTopesDescuento] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaUnidadesMedida] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaUsuariosCaja] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaUsuariosCentral] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE SEQUENCE [SecuenciaVentasCentral] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
GO


CREATE TABLE [ArchivosArranqueAplicados] (
    [Clave] varchar(100) NOT NULL,
    [Huella] varchar(64) NOT NULL,
    [AplicadoEn] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_ArchivosArranqueAplicados] PRIMARY KEY ([Clave])
);
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


CREATE TABLE [Bancos] (
    [Id] int NOT NULL,
    [Codigo] nvarchar(20) NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [RutaLogo] nvarchar(260) NULL,
    [Activo] bit NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Bancos] PRIMARY KEY ([Id])
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
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Clientes] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Denominaciones] (
    [Id] int NOT NULL,
    [Moneda] char(3) NOT NULL,
    [Valor] decimal(18,2) NOT NULL,
    [Tipo] int NOT NULL,
    [Activa] bit NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
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
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Departamentos] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Empresas] (
    [Id] int NOT NULL,
    [Rnc] varchar(11) NOT NULL,
    [RazonSocial] nvarchar(150) NOT NULL,
    [NombreComercial] nvarchar(150) NOT NULL,
    [Direccion] nvarchar(250) NOT NULL,
    [Telefono] nvarchar(20) NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Empresas] PRIMARY KEY ([Id])
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
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
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
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Impuestos] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Marcas] (
    [Id] int NOT NULL,
    [Codigo] int NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [Activa] bit NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Marcas] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Monedas] (
    [Id] int NOT NULL,
    [Codigo] char(3) NOT NULL,
    [Nombre] nvarchar(50) NOT NULL,
    [Simbolo] nvarchar(5) NOT NULL,
    [Activa] bit NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Monedas] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [MotivosDescuento] (
    [Id] int NOT NULL,
    [Codigo] int NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [Activo] bit NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_MotivosDescuento] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [MotivosDevolucion] (
    [Id] int NOT NULL,
    [Codigo] int NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [Activo] bit NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_MotivosDevolucion] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [MotivosSuspension] (
    [Id] int NOT NULL,
    [Codigo] int NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [Programado] bit NOT NULL,
    [ExigeNota] bit NOT NULL,
    [Activo] bit NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_MotivosSuspension] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [NivelesFidelidad] (
    [Id] int NOT NULL,
    [Codigo] int NOT NULL,
    [Nombre] nvarchar(60) NOT NULL,
    [Orden] int NOT NULL,
    [FactorAcumulacion] decimal(9,4) NOT NULL,
    [Activo] bit NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_NivelesFidelidad] PRIMARY KEY ([Id])
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
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
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
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_ReglasAcumulacion] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [RolesCaja] (
    [Id] int NOT NULL,
    [Codigo] nvarchar(30) NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [Nivel] int NOT NULL,
    [Activo] bit NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_RolesCaja] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [RolesCentral] (
    [Id] int NOT NULL,
    [Codigo] nvarchar(30) NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [Activo] bit NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    CONSTRAINT [PK_RolesCentral] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [SaldosPuntos] (
    [MiembroId] int NOT NULL,
    [Cedula] char(11) NOT NULL,
    [Puntos] int NOT NULL,
    [PuntosPorVencer] int NOT NULL,
    [ProximoVencimiento] date NULL,
    [Vencidos] int NOT NULL,
    [CalculadoEn] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_SaldosPuntos] PRIMARY KEY ([MiembroId])
);
GO


CREATE TABLE [SecuenciasCentral] (
    [Codigo] varchar(30) NOT NULL,
    [Prefijo] varchar(10) NOT NULL,
    [Documento] nvarchar(60) NOT NULL,
    [Ultimo] bigint NOT NULL,
    [Digitos] int NOT NULL,
    [Activa] bit NOT NULL,
    CONSTRAINT [PK_SecuenciasCentral] PRIMARY KEY ([Codigo])
);
GO


CREATE TABLE [TasasCambio] (
    [Id] int NOT NULL,
    [Moneda] varchar(3) NOT NULL,
    [Tasa] decimal(18,4) NOT NULL,
    [VigenteDesde] datetimeoffset(3) NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_TasasCambio] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [TiposTarjeta] (
    [Id] int NOT NULL,
    [Codigo] int NOT NULL,
    [Nombre] nvarchar(50) NOT NULL,
    [Activo] bit NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_TiposTarjeta] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [UnidadesMedida] (
    [Id] int NOT NULL,
    [Codigo] int NOT NULL,
    [Abreviatura] nvarchar(10) NOT NULL,
    [Nombre] nvarchar(50) NOT NULL,
    [PermiteDecimales] bit NOT NULL,
    [Decimales] int NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_UnidadesMedida] PRIMARY KEY ([Id])
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
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_DescuentosTarjeta] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DescuentosTarjeta_Bancos_BancoId] FOREIGN KEY ([BancoId]) REFERENCES [Bancos] ([Id]) ON DELETE NO ACTION
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
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Categorias] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Categorias_Departamentos_DepartamentoId] FOREIGN KEY ([DepartamentoId]) REFERENCES [Departamentos] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Sucursales] (
    [Id] int NOT NULL,
    [EmpresaId] int NOT NULL,
    [Codigo] char(2) NOT NULL,
    [Nombre] nvarchar(150) NOT NULL,
    [Direccion] nvarchar(250) NOT NULL,
    [Telefono] nvarchar(20) NOT NULL,
    [Activa] bit NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Sucursales] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Sucursales_Empresas_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION
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
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_MiembrosFidelidad] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MiembrosFidelidad_NivelesFidelidad_NivelId] FOREIGN KEY ([NivelId]) REFERENCES [NivelesFidelidad] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [RolesCajaPermisos] (
    [RolId] int NOT NULL,
    [PermisoCodigo] varchar(100) NOT NULL,
    CONSTRAINT [PK_RolesCajaPermisos] PRIMARY KEY ([RolId], [PermisoCodigo]),
    CONSTRAINT [FK_RolesCajaPermisos_RolesCaja_RolId] FOREIGN KEY ([RolId]) REFERENCES [RolesCaja] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [UsuariosCaja] (
    [Id] int NOT NULL,
    [Codigo] nvarchar(20) NOT NULL,
    [Nombre] nvarchar(150) NOT NULL,
    [RolId] int NOT NULL,
    [Activo] bit NOT NULL,
    [ClaveHash] varchar(256) NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_UsuariosCaja] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UsuariosCaja_RolesCaja_RolId] FOREIGN KEY ([RolId]) REFERENCES [RolesCaja] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [RolesCentralPermisos] (
    [RolId] int NOT NULL,
    [PermisoCodigo] varchar(100) NOT NULL,
    CONSTRAINT [PK_RolesCentralPermisos] PRIMARY KEY ([RolId], [PermisoCodigo]),
    CONSTRAINT [FK_RolesCentralPermisos_RolesCentral_RolId] FOREIGN KEY ([RolId]) REFERENCES [RolesCentral] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [UsuariosCentral] (
    [Id] int NOT NULL,
    [Codigo] nvarchar(50) NOT NULL,
    [Nombre] nvarchar(150) NOT NULL,
    [Correo] nvarchar(150) NULL,
    [RolId] int NOT NULL,
    [Activo] bit NOT NULL,
    [ContrasenaHash] varchar(256) NOT NULL,
    [DebeCambiarContrasena] bit NOT NULL,
    [ContrasenaCambiadaEn] datetimeoffset(3) NULL,
    [IntentosFallidos] int NOT NULL,
    [BloqueadoHasta] datetimeoffset(3) NULL,
    [UltimoIngresoEn] datetimeoffset(3) NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    CONSTRAINT [PK_UsuariosCentral] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UsuariosCentral_RolesCentral_RolId] FOREIGN KEY ([RolId]) REFERENCES [RolesCentral] ([Id]) ON DELETE NO ACTION
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
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [PrecioDetalle] decimal(18,2) NOT NULL,
    [PrecioMayor] decimal(18,2) NULL,
    [PreciosVigentesDesde] datetimeoffset(3) NULL,
    [Version] rowversion NOT NULL,
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
    [Codigo] char(2) NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [DireccionIp] varchar(45) NOT NULL,
    [Habilitada] bit NOT NULL,
    [VersionAgente] nvarchar(256) NULL,
    [VersionReportadaEn] datetimeoffset(3) NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Cajas] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Cajas_Sucursales_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [Sucursales] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [CierresSucursal] (
    [Id] int NOT NULL,
    [SucursalId] int NOT NULL,
    [NumeroCentral] varchar(200) NULL,
    [FechaOperacion] date NOT NULL,
    [CantidadCierres] int NOT NULL,
    [CantidadVentas] int NOT NULL,
    [TotalVentas] decimal(18,4) NOT NULL,
    [TotalEsperado] decimal(18,4) NOT NULL,
    [TotalDeclarado] decimal(18,4) NOT NULL,
    [Diferencia] decimal(18,4) NOT NULL,
    [Observacion] nvarchar(500) NULL,
    [CerradoPor] nvarchar(200) NOT NULL,
    [CerradoEn] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_CierresSucursal] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CierresSucursal_Sucursales_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [Sucursales] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Cotizaciones] (
    [Id] int NOT NULL,
    [Numero] varchar(20) NOT NULL,
    [ClienteNombre] nvarchar(150) NOT NULL,
    [ClienteDocumento] nvarchar(20) NULL,
    [ClienteTelefono] nvarchar(100) NULL,
    [ClienteCorreo] nvarchar(100) NULL,
    [SucursalId] int NULL,
    [Observacion] nvarchar(500) NULL,
    [VenceEn] date NOT NULL,
    [Estado] varchar(20) NOT NULL,
    [VentaNumero] varchar(40) NULL,
    [FacturadaEn] datetimeoffset(3) NULL,
    [MotivoAnulacion] nvarchar(500) NULL,
    [CreadaPor] nvarchar(150) NOT NULL,
    [CreadaEn] datetimeoffset(3) NOT NULL,
    [ActualizadaEn] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_Cotizaciones] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Cotizaciones_Sucursales_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [Sucursales] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [ListasBoda] (
    [Id] int NOT NULL,
    [Numero] varchar(20) NOT NULL,
    [Evento] nvarchar(150) NOT NULL,
    [FechaEvento] date NOT NULL,
    [Lugar] nvarchar(200) NULL,
    [ClienteDocumento] nvarchar(20) NOT NULL,
    [ClienteNombre] nvarchar(150) NOT NULL,
    [ClienteTelefono] nvarchar(100) NULL,
    [ClienteCorreo] nvarchar(100) NULL,
    [SucursalId] int NULL,
    [Observacion] nvarchar(500) NULL,
    [Estado] varchar(20) NOT NULL,
    [CreadaEn] datetimeoffset(3) NOT NULL,
    [ActualizadaEn] datetimeoffset(3) NOT NULL,
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    CONSTRAINT [PK_ListasBoda] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ListasBoda_Sucursales_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [Sucursales] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [SesionesCentral] (
    [Id] uniqueidentifier NOT NULL,
    [UsuarioId] int NOT NULL,
    [Familia] uniqueidentifier NOT NULL,
    [TokenHash] char(64) NOT NULL,
    [CreadaEn] datetimeoffset(3) NOT NULL,
    [ExpiraEn] datetimeoffset(3) NOT NULL,
    [FinSesion] datetimeoffset(3) NOT NULL,
    [UsadaEn] datetimeoffset(3) NULL,
    [ReemplazadaPorId] uniqueidentifier NULL,
    [RevocadaEn] datetimeoffset(3) NULL,
    [MotivoRevocacion] nvarchar(150) NULL,
    [DireccionIp] varchar(45) NULL,
    [AgenteUsuario] nvarchar(256) NULL,
    CONSTRAINT [PK_SesionesCentral] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SesionesCentral_UsuariosCentral_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [UsuariosCentral] ([Id]) ON DELETE NO ACTION
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
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_TopesDescuento] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TopesDescuento_Articulos_ArticuloId] FOREIGN KEY ([ArticuloId]) REFERENCES [Articulos] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TopesDescuento_Categorias_CategoriaId] FOREIGN KEY ([CategoriaId]) REFERENCES [Categorias] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TopesDescuento_Departamentos_DepartamentoId] FOREIGN KEY ([DepartamentoId]) REFERENCES [Departamentos] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TopesDescuento_Marcas_MarcaId] FOREIGN KEY ([MarcaId]) REFERENCES [Marcas] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [AccesosUsuarioCaja] (
    [Id] int NOT NULL,
    [UsuarioId] int NOT NULL,
    [CajaId] int NOT NULL,
    [IngresoEn] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_AccesosUsuarioCaja] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccesosUsuarioCaja_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]),
    CONSTRAINT [FK_AccesosUsuarioCaja_UsuariosCaja_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [UsuariosCaja] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AnulacionesEcf] (
    [Id] int NOT NULL,
    [SecuenciaId] int NOT NULL,
    [CajaId] int NOT NULL,
    [TipoComprobante] int NOT NULL,
    [Desde] bigint NOT NULL,
    [Hasta] bigint NOT NULL,
    [Motivo] nvarchar(500) NOT NULL,
    [UsuarioNombre] nvarchar(150) NOT NULL,
    [SolicitadaEn] datetimeoffset(3) NOT NULL,
    [Estado] int NOT NULL,
    [RespuestaDgii] nvarchar(2000) NULL,
    [XmlFirmado] nvarchar(max) NULL,
    CONSTRAINT [PK_AnulacionesEcf] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AnulacionesEcf_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [CierresTurno] (
    [Id] int NOT NULL,
    [TurnoNumero] bigint NOT NULL,
    [Numero] int NOT NULL,
    [SucursalId] int NOT NULL,
    [CajaId] int NOT NULL,
    [FechaOperacion] date NOT NULL,
    [UsuarioNombre] nvarchar(200) NOT NULL,
    [Moneda] char(3) NOT NULL,
    [FondoInicial] decimal(18,4) NOT NULL,
    [CantidadVentas] int NOT NULL,
    [TotalVentas] decimal(18,4) NOT NULL,
    [TotalRetiros] decimal(18,4) NOT NULL,
    [TotalEsperado] decimal(18,4) NOT NULL,
    [TotalDeclarado] decimal(18,4) NOT NULL,
    [Diferencia] decimal(18,4) NOT NULL,
    [AbiertoEn] datetimeoffset(3) NOT NULL,
    [CerradoEn] datetimeoffset(3) NOT NULL,
    [RegistradoEn] datetimeoffset(3) NOT NULL,
    [CuadradoPor] nvarchar(200) NULL,
    [CuadradoEn] datetimeoffset(3) NULL,
    CONSTRAINT [PK_CierresTurno] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CierresTurno_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CierresTurno_Sucursales_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [Sucursales] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [ConflictosSincronizacion] (
    [Id] int NOT NULL,
    [CajaId] int NOT NULL,
    [SucursalId] int NULL,
    [MensajeId] uniqueidentifier NOT NULL,
    [TipoMensaje] nvarchar(100) NOT NULL,
    [Tipo] nvarchar(40) NOT NULL,
    [Detalle] nvarchar(2000) NOT NULL,
    [DetectadoEn] datetimeoffset(3) NOT NULL,
    [Ocurrencias] int NOT NULL,
    [UltimaOcurrenciaEn] datetimeoffset(3) NOT NULL,
    [ResueltoEn] datetimeoffset(3) NULL,
    [ResueltoPor] nvarchar(150) NULL,
    [Resolucion] nvarchar(500) NULL,
    CONSTRAINT [PK_ConflictosSincronizacion] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ConflictosSincronizacion_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [ConsumosNotaCredito] (
    [Id] int NOT NULL,
    [NotaCreditoNumero] nvarchar(40) NOT NULL,
    [VentaNumero] nvarchar(40) NOT NULL,
    [CajaId] int NOT NULL,
    [Monto] decimal(18,4) NOT NULL,
    [Fecha] datetimeoffset(3) NOT NULL,
    [RegistradoEn] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_ConsumosNotaCredito] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ConsumosNotaCredito_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [CredencialesDispositivo] (
    [Id] int NOT NULL,
    [CajaId] int NOT NULL,
    [SecretoHash] char(64) NOT NULL,
    [EmitidaEn] datetimeoffset(3) NOT NULL,
    [EmitidaPor] nvarchar(150) NOT NULL,
    [RevocadaEn] datetimeoffset(3) NULL,
    [MotivoRevocacion] nvarchar(250) NULL,
    [UltimoUsoEn] datetimeoffset(3) NULL,
    [UltimaIp] varchar(45) NULL,
    CONSTRAINT [PK_CredencialesDispositivo] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CredencialesDispositivo_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [DocumentosRecibidos] (
    [Id] int NOT NULL,
    [MensajeId] uniqueidentifier NOT NULL,
    [CajaId] int NOT NULL,
    [SucursalId] int NOT NULL,
    [TipoMensaje] nvarchar(100) NOT NULL,
    [Referencia] varchar(40) NOT NULL,
    [Contenido] nvarchar(max) NOT NULL,
    [HashContenido] char(64) NOT NULL,
    [CreadoEnCaja] datetimeoffset(3) NOT NULL,
    [RecibidoEn] datetimeoffset(3) NOT NULL,
    [Reenvios] int NOT NULL,
    [UltimoReenvioEn] datetimeoffset(3) NULL,
    CONSTRAINT [PK_DocumentosRecibidos] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DocumentosRecibidos_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_DocumentosRecibidos_Sucursales_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [Sucursales] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [EstadosSincronizacionCaja] (
    [CajaId] int NOT NULL,
    [UltimaRecepcionEn] datetimeoffset(3) NULL,
    [MensajesRecibidos] bigint NOT NULL,
    [Duplicados] bigint NOT NULL,
    [Rechazados] bigint NOT NULL,
    [UltimoRechazoEn] datetimeoffset(3) NULL,
    [UltimoError] nvarchar(2000) NULL,
    [UltimaDescargaEn] datetimeoffset(3) NULL,
    [VersionMaestrosConfirmada] bigint NOT NULL,
    [VersionMaestrosEntregada] bigint NOT NULL,
    CONSTRAINT [PK_EstadosSincronizacionCaja] PRIMARY KEY ([CajaId]),
    CONSTRAINT [FK_EstadosSincronizacionCaja_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [MovimientosPuntos] (
    [Id] int NOT NULL,
    [Cedula] char(11) NOT NULL,
    [Tipo] int NOT NULL,
    [Origen] int NOT NULL,
    [Puntos] int NOT NULL,
    [Documento] nvarchar(40) NOT NULL,
    [CajaId] int NULL,
    [SucursalId] int NULL,
    [Fecha] datetimeoffset(3) NOT NULL,
    [VenceEn] date NULL,
    [RegistradoEn] datetimeoffset(3) NOT NULL,
    [Usuario] nvarchar(150) NULL,
    [Motivo] nvarchar(500) NULL,
    CONSTRAINT [PK_MovimientosPuntos] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MovimientosPuntos_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_MovimientosPuntos_Sucursales_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [Sucursales] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [NotasCredito] (
    [Id] int NOT NULL,
    [Numero] nvarchar(40) NOT NULL,
    [Encf] char(13) NULL,
    [CajaId] int NOT NULL,
    [SucursalId] int NOT NULL,
    [ClienteDocumento] nvarchar(200) NOT NULL,
    [ClienteNombre] nvarchar(200) NOT NULL,
    [Moneda] char(3) NOT NULL,
    [Total] decimal(18,4) NOT NULL,
    [Consumido] decimal(18,4) NOT NULL,
    [FechaEmision] date NOT NULL,
    [EmitidaEn] datetimeoffset(3) NOT NULL,
    [RegistradaEn] datetimeoffset(3) NOT NULL,
    [EsInterna] bit NOT NULL,
    CONSTRAINT [PK_NotasCredito] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_NotasCredito_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_NotasCredito_Sucursales_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [Sucursales] ([Id]) ON DELETE NO ACTION
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
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Parametros] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Parametros_UnSoloAmbito] CHECK ([SucursalId] IS NULL OR [CajaId] IS NULL),
    CONSTRAINT [FK_Parametros_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Parametros_Sucursales_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [Sucursales] ([Id]) ON DELETE NO ACTION
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
    [AvisoEnviadoEn] datetimeoffset(3) NULL,
    [NumeroCentral] varchar(40) NULL,
    CONSTRAINT [PK_PendientesEntrega] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PendientesEntrega_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PendientesEntrega_Sucursales_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [Sucursales] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [ReservasFactura] (
    [Id] int NOT NULL,
    [FacturaNumero] varchar(40) NOT NULL,
    [CajaId] int NOT NULL,
    [CreadaEn] datetimeoffset(3) NOT NULL,
    [VenceEn] datetimeoffset(3) NOT NULL,
    [CerradaEn] datetimeoffset(3) NULL,
    [Cierre] nvarchar(40) NULL,
    CONSTRAINT [PK_ReservasFactura] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReservasFactura_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION
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
    [ModificadoEn] datetimeoffset(3) NOT NULL,
    [ModificadoPor] nvarchar(150) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_SecuenciasEcf] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SecuenciasEcf_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [SuspensionesCaja] (
    [Id] int NOT NULL,
    [SucursalId] int NOT NULL,
    [CajaId] int NOT NULL,
    [TurnoNumero] bigint NOT NULL,
    [Numero] int NOT NULL,
    [FechaOperacion] date NOT NULL,
    [UsuarioNombre] nvarchar(200) NOT NULL,
    [MotivoCodigo] int NULL,
    [MotivoNombre] nvarchar(200) NOT NULL,
    [Programado] bit NOT NULL,
    [Nota] nvarchar(250) NULL,
    [SuspendidaEn] datetimeoffset(3) NOT NULL,
    [ReanudadaEn] datetimeoffset(3) NOT NULL,
    [CerradaPorCierreDeTurno] bit NOT NULL,
    [Segundos] int NOT NULL,
    CONSTRAINT [PK_SuspensionesCaja] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SuspensionesCaja_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SuspensionesCaja_Sucursales_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [Sucursales] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [UsuariosCajaCajas] (
    [UsuarioId] int NOT NULL,
    [CajaId] int NOT NULL,
    CONSTRAINT [PK_UsuariosCajaCajas] PRIMARY KEY ([UsuarioId], [CajaId]),
    CONSTRAINT [FK_UsuariosCajaCajas_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UsuariosCajaCajas_UsuariosCaja_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [UsuariosCaja] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [VentasCentral] (
    [Id] int NOT NULL,
    [Tipo] int NOT NULL,
    [Numero] nvarchar(40) NOT NULL,
    [NumeroCentral] varchar(40) NULL,
    [SucursalId] int NOT NULL,
    [CajaId] int NOT NULL,
    [TurnoNumero] bigint NULL,
    [UsuarioNombre] nvarchar(200) NOT NULL,
    [Fecha] datetimeoffset(3) NOT NULL,
    [FechaOperacion] date NOT NULL,
    [TipoComprobanteFiscal] int NOT NULL,
    [Encf] char(13) NULL,
    [EncfModificado] char(13) NULL,
    [ClienteTipoDocumento] int NULL,
    [ClienteDocumento] nvarchar(200) NULL,
    [ClienteNombre] nvarchar(200) NULL,
    [Moneda] char(3) NOT NULL,
    [Subtotal] decimal(18,4) NOT NULL,
    [Descuento] decimal(18,4) NOT NULL,
    [Impuesto] decimal(18,4) NOT NULL,
    [ImpuestoRetenido] decimal(18,4) NOT NULL,
    [Total] decimal(18,4) NOT NULL,
    [CantidadLineas] int NOT NULL,
    [RegistradoEn] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_VentasCentral] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_VentasCentral_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_VentasCentral_Sucursales_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [Sucursales] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [CierresSucursalFormaPago] (
    [Id] int NOT NULL,
    [CierreSucursalId] int NOT NULL,
    [Tipo] int NOT NULL,
    [Nombre] nvarchar(200) NOT NULL,
    [Moneda] char(3) NOT NULL,
    [Transacciones] int NOT NULL,
    [Esperado] decimal(18,4) NOT NULL,
    [Declarado] decimal(18,4) NOT NULL,
    [Diferencia] decimal(18,4) NOT NULL,
    CONSTRAINT [PK_CierresSucursalFormaPago] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CierresSucursalFormaPago_CierresSucursal_CierreSucursalId] FOREIGN KEY ([CierreSucursalId]) REFERENCES [CierresSucursal] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [DepositosCierreSucursal] (
    [Id] int NOT NULL,
    [CierreSucursalId] int NOT NULL,
    [Moneda] char(3) NOT NULL,
    [BancoCodigo] nvarchar(20) NOT NULL,
    [BancoNombre] nvarchar(100) NOT NULL,
    [NumeroBoleta] nvarchar(50) NOT NULL,
    [Monto] decimal(18,4) NOT NULL,
    [FechaDeposito] date NOT NULL,
    CONSTRAINT [PK_DepositosCierreSucursal] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DepositosCierreSucursal_CierresSucursal_CierreSucursalId] FOREIGN KEY ([CierreSucursalId]) REFERENCES [CierresSucursal] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [LineasCotizacion] (
    [Id] int NOT NULL,
    [CotizacionId] int NOT NULL,
    [NumeroLinea] int NOT NULL,
    [ArticuloCodigo] varchar(30) NOT NULL,
    [Descripcion] nvarchar(200) NOT NULL,
    [UnidadMedida] varchar(10) NULL,
    [Cantidad] decimal(18,3) NOT NULL,
    [PrecioUnitario] decimal(18,2) NOT NULL,
    [Descuento] decimal(18,2) NOT NULL,
    [PorcentajeImpuesto] decimal(5,2) NOT NULL,
    [IndicadorFacturacion] int NOT NULL,
    CONSTRAINT [PK_LineasCotizacion] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LineasCotizacion_Cotizaciones_CotizacionId] FOREIGN KEY ([CotizacionId]) REFERENCES [Cotizaciones] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ArticulosListaBoda] (
    [Id] int NOT NULL,
    [ListaBodaId] int NOT NULL,
    [ArticuloCodigo] varchar(30) NOT NULL,
    [Descripcion] nvarchar(150) NOT NULL,
    [Cantidad] decimal(18,3) NOT NULL,
    [Comprado] decimal(18,3) NOT NULL,
    CONSTRAINT [PK_ArticulosListaBoda] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ArticulosListaBoda_ListasBoda_ListaBodaId] FOREIGN KEY ([ListaBodaId]) REFERENCES [ListasBoda] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ComprasListaBoda] (
    [Id] int NOT NULL,
    [ListaBodaId] int NOT NULL,
    [VentaNumero] varchar(40) NOT NULL,
    [CajaId] int NOT NULL,
    [Monto] decimal(18,2) NOT NULL,
    [Fecha] datetimeoffset(3) NOT NULL,
    [RegistradaEn] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_ComprasListaBoda] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ComprasListaBoda_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ComprasListaBoda_ListasBoda_ListaBodaId] FOREIGN KEY ([ListaBodaId]) REFERENCES [ListasBoda] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AjustesCierreTurno] (
    [Id] int NOT NULL,
    [CierreId] int NOT NULL,
    [FormaPagoId] int NOT NULL,
    [FormaPagoNombre] nvarchar(200) NOT NULL,
    [Moneda] char(3) NOT NULL,
    [DeclaradoAnterior] decimal(18,4) NOT NULL,
    [DeclaradoNuevo] decimal(18,4) NOT NULL,
    [Motivo] nvarchar(500) NOT NULL,
    [AjustadoPorNombre] nvarchar(200) NOT NULL,
    [AjustadoEn] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_AjustesCierreTurno] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AjustesCierreTurno_CierresTurno_CierreId] FOREIGN KEY ([CierreId]) REFERENCES [CierresTurno] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [CierresFormaPago] (
    [Id] int NOT NULL,
    [CierreId] int NOT NULL,
    [Tipo] int NOT NULL,
    [Nombre] nvarchar(200) NOT NULL,
    [Moneda] char(3) NOT NULL,
    [Transacciones] int NOT NULL,
    [Esperado] decimal(18,4) NOT NULL,
    [Declarado] decimal(18,4) NOT NULL,
    [Diferencia] decimal(18,4) NOT NULL,
    CONSTRAINT [PK_CierresFormaPago] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CierresFormaPago_CierresTurno_CierreId] FOREIGN KEY ([CierreId]) REFERENCES [CierresTurno] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [CierresTurnoDenominaciones] (
    [Id] int NOT NULL,
    [CierreId] int NOT NULL,
    [DenominacionId] int NOT NULL,
    [Moneda] char(3) NOT NULL,
    [Valor] decimal(18,2) NOT NULL,
    [Tipo] int NOT NULL,
    [Cantidad] int NOT NULL,
    [Importe] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_CierresTurnoDenominaciones] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CierresTurnoDenominaciones_CierresTurno_CierreId] FOREIGN KEY ([CierreId]) REFERENCES [CierresTurno] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [CierresTurnoLote] (
    [Id] int NOT NULL,
    [CierreId] int NOT NULL,
    [NumeroLote] varchar(30) NULL,
    [TransaccionesCaja] int NOT NULL,
    [MontoCaja] decimal(18,2) NOT NULL,
    [TransaccionesTerminal] int NOT NULL,
    [MontoTerminal] decimal(18,2) NOT NULL,
    [Diferencia] decimal(18,2) NOT NULL,
    [DetalleDelTerminal] bit NOT NULL,
    [UsuarioNombre] nvarchar(200) NOT NULL,
    [CerradoEn] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_CierresTurnoLote] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CierresTurnoLote_CierresTurno_CierreId] FOREIGN KEY ([CierreId]) REFERENCES [CierresTurno] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [CierresTurnoLoteAprobaciones] (
    [Id] int NOT NULL,
    [CierreId] int NOT NULL,
    [Aprobacion] varchar(30) NOT NULL,
    [Origen] int NOT NULL,
    CONSTRAINT [PK_CierresTurnoLoteAprobaciones] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CierresTurnoLoteAprobaciones_CierresTurno_CierreId] FOREIGN KEY ([CierreId]) REFERENCES [CierresTurno] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [CierresTurnoMovimientos] (
    [Id] int NOT NULL,
    [CierreId] int NOT NULL,
    [Tipo] int NOT NULL,
    [Numero] int NOT NULL,
    [Monto] decimal(18,2) NOT NULL,
    [Moneda] char(3) NOT NULL,
    [Motivo] nvarchar(500) NULL,
    [UsuarioNombre] nvarchar(200) NOT NULL,
    [UsuarioAnteriorNombre] nvarchar(200) NULL,
    [AutorizadoPorNombre] nvarchar(200) NULL,
    [Fecha] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_CierresTurnoMovimientos] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CierresTurnoMovimientos_CierresTurno_CierreId] FOREIGN KEY ([CierreId]) REFERENCES [CierresTurno] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ComprobantesRecibidos] (
    [Id] int NOT NULL,
    [DocumentoId] int NOT NULL,
    [Referencia] varchar(40) NOT NULL,
    [CajaId] int NOT NULL,
    [SucursalId] int NOT NULL,
    [Encf] char(13) NOT NULL,
    [TipoComprobante] int NOT NULL,
    [XmlFirmado] nvarchar(max) NOT NULL,
    [HashXml] char(64) NOT NULL,
    [FechaFirma] datetimeoffset(3) NOT NULL,
    [RecibidoEn] datetimeoffset(3) NOT NULL,
    [EsResumenConsumo] bit NOT NULL,
    [EstadoDgii] nvarchar(30) NOT NULL,
    [EstadoDgiiEn] datetimeoffset(3) NULL,
    [MensajeDgii] nvarchar(2000) NULL,
    [TrackId] varchar(100) NULL,
    [IntentosEnvio] int NOT NULL,
    [EnviadoEn] datetimeoffset(3) NULL,
    [ProximoIntentoEn] datetimeoffset(3) NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_ComprobantesRecibidos] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ComprobantesRecibidos_DocumentosRecibidos_DocumentoId] FOREIGN KEY ([DocumentoId]) REFERENCES [DocumentosRecibidos] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [ReservasNotaCredito] (
    [Id] int NOT NULL,
    [NotaCreditoId] int NOT NULL,
    [CajaId] int NOT NULL,
    [VentaNumero] nvarchar(40) NOT NULL,
    [Monto] decimal(18,4) NOT NULL,
    [CreadaEn] datetimeoffset(3) NOT NULL,
    [VenceEn] datetimeoffset(3) NOT NULL,
    [CerradaEn] datetimeoffset(3) NULL,
    [Cierre] nvarchar(40) NULL,
    CONSTRAINT [PK_ReservasNotaCredito] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReservasNotaCredito_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ReservasNotaCredito_NotasCredito_NotaCreditoId] FOREIGN KEY ([NotaCreditoId]) REFERENCES [NotasCredito] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [EntregasPendiente] (
    [Id] int NOT NULL,
    [PendienteEntregaId] int NOT NULL,
    [Numero] int NOT NULL,
    [RecibeNombre] nvarchar(150) NOT NULL,
    [RecibeCedula] nvarchar(20) NOT NULL,
    [UsuarioNombre] nvarchar(150) NOT NULL,
    [Fecha] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_EntregasPendiente] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EntregasPendiente_PendientesEntrega_PendienteEntregaId] FOREIGN KEY ([PendienteEntregaId]) REFERENCES [PendientesEntrega] ([Id]) ON DELETE CASCADE
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


CREATE TABLE [LineasReservaFactura] (
    [Id] int NOT NULL,
    [ReservaId] int NOT NULL,
    [NumeroLinea] int NOT NULL,
    [Cantidad] decimal(18,4) NOT NULL,
    CONSTRAINT [PK_LineasReservaFactura] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LineasReservaFactura_ReservasFactura_ReservaId] FOREIGN KEY ([ReservaId]) REFERENCES [ReservasFactura] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ImpuestosVenta] (
    [Id] int NOT NULL,
    [ComprobanteId] int NOT NULL,
    [Porcentaje] decimal(18,4) NOT NULL,
    [Base] decimal(18,4) NOT NULL,
    [Impuesto] decimal(18,4) NOT NULL,
    CONSTRAINT [PK_ImpuestosVenta] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ImpuestosVenta_VentasCentral_ComprobanteId] FOREIGN KEY ([ComprobanteId]) REFERENCES [VentasCentral] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [LineasVenta] (
    [Id] int NOT NULL,
    [ComprobanteId] int NOT NULL,
    [NumeroLinea] int NOT NULL,
    [Codigo] varchar(30) NOT NULL,
    [Descripcion] nvarchar(200) NOT NULL,
    [UnidadMedida] varchar(10) NULL,
    [Cantidad] decimal(18,3) NOT NULL,
    [PrecioUnitario] decimal(18,4) NOT NULL,
    [Descuento] decimal(18,2) NOT NULL,
    [Impuesto] decimal(18,2) NOT NULL,
    [Importe] decimal(18,2) NOT NULL,
    [Serial] nvarchar(200) NULL,
    [PromocionCodigo] varchar(30) NULL,
    CONSTRAINT [PK_LineasVenta] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LineasVenta_VentasCentral_ComprobanteId] FOREIGN KEY ([ComprobanteId]) REFERENCES [VentasCentral] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [PagosVenta] (
    [Id] int NOT NULL,
    [ComprobanteId] int NOT NULL,
    [Tipo] int NOT NULL,
    [FormaPagoNombre] nvarchar(200) NOT NULL,
    [Moneda] char(3) NOT NULL,
    [Monto] decimal(18,4) NOT NULL,
    CONSTRAINT [PK_PagosVenta] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PagosVenta_VentasCentral_ComprobanteId] FOREIGN KEY ([ComprobanteId]) REFERENCES [VentasCentral] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [LineasEntregaPendiente] (
    [Id] int NOT NULL,
    [EntregaPendienteId] int NOT NULL,
    [NumeroLineaVenta] int NOT NULL,
    [Descripcion] nvarchar(200) NOT NULL,
    [Cantidad] decimal(18,3) NOT NULL,
    [Serial] nvarchar(50) NULL,
    CONSTRAINT [PK_LineasEntregaPendiente] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LineasEntregaPendiente_EntregasPendiente_EntregaPendienteId] FOREIGN KEY ([EntregaPendienteId]) REFERENCES [EntregasPendiente] ([Id]) ON DELETE CASCADE
);
GO


CREATE INDEX [IX_AccesosUsuarioCaja_CajaId] ON [AccesosUsuarioCaja] ([CajaId]);
GO


CREATE UNIQUE INDEX [IX_AccesosUsuarioCaja_UsuarioId] ON [AccesosUsuarioCaja] ([UsuarioId]);
GO


CREATE INDEX [IX_AjustesCierreTurno_CierreId] ON [AjustesCierreTurno] ([CierreId]);
GO


CREATE INDEX [IX_AnulacionesEcf_CajaId_TipoComprobante] ON [AnulacionesEcf] ([CajaId], [TipoComprobante]);
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


CREATE INDEX [IX_Articulos_Referencia] ON [Articulos] ([Referencia]);
GO


CREATE INDEX [IX_Articulos_UnidadMedidaId] ON [Articulos] ([UnidadMedidaId]);
GO


CREATE INDEX [IX_Articulos_Version] ON [Articulos] ([Version]);
GO


CREATE UNIQUE INDEX [IX_ArticulosListaBoda_ListaBodaId_ArticuloCodigo] ON [ArticulosListaBoda] ([ListaBodaId], [ArticuloCodigo]);
GO


CREATE INDEX [IX_Auditoria_Accion] ON [Auditoria] ([Accion]);
GO


CREATE INDEX [IX_Auditoria_OcurridoEn] ON [Auditoria] ([OcurridoEn]);
GO


CREATE INDEX [IX_Auditoria_TipoEntidad_EntidadId] ON [Auditoria] ([TipoEntidad], [EntidadId]);
GO


CREATE UNIQUE INDEX [IX_Bancos_Codigo] ON [Bancos] ([Codigo]);
GO


CREATE INDEX [IX_Bancos_Version] ON [Bancos] ([Version]);
GO


CREATE UNIQUE INDEX [IX_Cajas_DireccionIp] ON [Cajas] ([DireccionIp]);
GO


CREATE UNIQUE INDEX [IX_Cajas_SucursalId_Codigo] ON [Cajas] ([SucursalId], [Codigo]);
GO


CREATE UNIQUE INDEX [IX_Categorias_Codigo] ON [Categorias] ([Codigo]);
GO


CREATE INDEX [IX_Categorias_DepartamentoId] ON [Categorias] ([DepartamentoId]);
GO


CREATE INDEX [IX_Categorias_Version] ON [Categorias] ([Version]);
GO


CREATE INDEX [IX_CierresFormaPago_CierreId] ON [CierresFormaPago] ([CierreId]);
GO


CREATE UNIQUE INDEX [IX_CierresSucursal_NumeroCentral] ON [CierresSucursal] ([NumeroCentral]) WHERE [NumeroCentral] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_CierresSucursal_SucursalId_FechaOperacion] ON [CierresSucursal] ([SucursalId], [FechaOperacion]);
GO


CREATE INDEX [IX_CierresSucursalFormaPago_CierreSucursalId] ON [CierresSucursalFormaPago] ([CierreSucursalId]);
GO


CREATE UNIQUE INDEX [IX_CierresTurno_CajaId_TurnoNumero] ON [CierresTurno] ([CajaId], [TurnoNumero]);
GO


CREATE INDEX [IX_CierresTurno_FechaOperacion_SucursalId_CajaId] ON [CierresTurno] ([FechaOperacion], [SucursalId], [CajaId]);
GO


CREATE INDEX [IX_CierresTurno_SucursalId_CuadradoEn] ON [CierresTurno] ([SucursalId], [CuadradoEn]);
GO


CREATE INDEX [IX_CierresTurnoDenominaciones_CierreId] ON [CierresTurnoDenominaciones] ([CierreId]);
GO


CREATE UNIQUE INDEX [IX_CierresTurnoLote_CierreId] ON [CierresTurnoLote] ([CierreId]);
GO


CREATE INDEX [IX_CierresTurnoLoteAprobaciones_CierreId] ON [CierresTurnoLoteAprobaciones] ([CierreId]);
GO


CREATE INDEX [IX_CierresTurnoMovimientos_CierreId] ON [CierresTurnoMovimientos] ([CierreId]);
GO


CREATE UNIQUE INDEX [IX_Clientes_Codigo] ON [Clientes] ([Codigo]);
GO


CREATE INDEX [IX_Clientes_Nombre] ON [Clientes] ([Nombre]);
GO


CREATE UNIQUE INDEX [IX_Clientes_TipoDocumento_Documento] ON [Clientes] ([TipoDocumento], [Documento]);
GO


CREATE INDEX [IX_Clientes_Version] ON [Clientes] ([Version]);
GO


CREATE UNIQUE INDEX [IX_CodigosArticulo_Codigo] ON [CodigosArticulo] ([Codigo]);
GO


CREATE INDEX [IX_ComprasListaBoda_CajaId] ON [ComprasListaBoda] ([CajaId]);
GO


CREATE UNIQUE INDEX [IX_ComprasListaBoda_ListaBodaId_VentaNumero] ON [ComprasListaBoda] ([ListaBodaId], [VentaNumero]);
GO


CREATE INDEX [IX_ComprobantesRecibidos_DocumentoId] ON [ComprobantesRecibidos] ([DocumentoId]);
GO


CREATE UNIQUE INDEX [IX_ComprobantesRecibidos_Encf] ON [ComprobantesRecibidos] ([Encf]);
GO


CREATE INDEX [IX_ComprobantesRecibidos_EstadoDgii_ProximoIntentoEn] ON [ComprobantesRecibidos] ([EstadoDgii], [ProximoIntentoEn]);
GO


CREATE INDEX [IX_ComprobantesRecibidos_EstadoDgii_RecibidoEn] ON [ComprobantesRecibidos] ([EstadoDgii], [RecibidoEn]);
GO


CREATE INDEX [IX_ComprobantesRecibidos_Version] ON [ComprobantesRecibidos] ([Version]);
GO


CREATE INDEX [IX_ConflictosSincronizacion_CajaId_ResueltoEn] ON [ConflictosSincronizacion] ([CajaId], [ResueltoEn]);
GO


CREATE INDEX [IX_ConflictosSincronizacion_MensajeId_CajaId_Tipo] ON [ConflictosSincronizacion] ([MensajeId], [CajaId], [Tipo]);
GO


CREATE INDEX [IX_ConsumosNotaCredito_CajaId] ON [ConsumosNotaCredito] ([CajaId]);
GO


CREATE UNIQUE INDEX [IX_ConsumosNotaCredito_NotaCreditoNumero_VentaNumero] ON [ConsumosNotaCredito] ([NotaCreditoNumero], [VentaNumero]);
GO


CREATE INDEX [IX_Cotizaciones_ClienteDocumento] ON [Cotizaciones] ([ClienteDocumento]);
GO


CREATE UNIQUE INDEX [IX_Cotizaciones_Numero] ON [Cotizaciones] ([Numero]);
GO


CREATE INDEX [IX_Cotizaciones_SucursalId] ON [Cotizaciones] ([SucursalId]);
GO


CREATE INDEX [IX_Cotizaciones_VenceEn] ON [Cotizaciones] ([VenceEn]);
GO


CREATE UNIQUE INDEX [IX_CredencialesDispositivo_CajaActiva] ON [CredencialesDispositivo] ([CajaId]) WHERE [RevocadaEn] IS NULL;
GO


CREATE UNIQUE INDEX [IX_Denominaciones_Moneda_Valor_Tipo] ON [Denominaciones] ([Moneda], [Valor], [Tipo]);
GO


CREATE INDEX [IX_Denominaciones_Version] ON [Denominaciones] ([Version]);
GO


CREATE UNIQUE INDEX [IX_Departamentos_Codigo] ON [Departamentos] ([Codigo]);
GO


CREATE INDEX [IX_Departamentos_Version] ON [Departamentos] ([Version]);
GO


CREATE INDEX [IX_DepositosCierreSucursal_CierreSucursalId] ON [DepositosCierreSucursal] ([CierreSucursalId]);
GO


CREATE INDEX [IX_DescuentosTarjeta_BancoId] ON [DescuentosTarjeta] ([BancoId]);
GO


CREATE UNIQUE INDEX [IX_DescuentosTarjeta_Codigo] ON [DescuentosTarjeta] ([Codigo]);
GO


CREATE INDEX [IX_DescuentosTarjeta_Version] ON [DescuentosTarjeta] ([Version]);
GO


CREATE UNIQUE INDEX [IX_DireccionesCliente_ClienteId_Alias] ON [DireccionesCliente] ([ClienteId], [Alias]);
GO


CREATE INDEX [IX_DocumentosRecibidos_CajaId_RecibidoEn] ON [DocumentosRecibidos] ([CajaId], [RecibidoEn]);
GO


CREATE UNIQUE INDEX [IX_DocumentosRecibidos_MensajeId] ON [DocumentosRecibidos] ([MensajeId]);
GO


CREATE INDEX [IX_DocumentosRecibidos_Referencia] ON [DocumentosRecibidos] ([Referencia]);
GO


CREATE INDEX [IX_DocumentosRecibidos_SucursalId] ON [DocumentosRecibidos] ([SucursalId]);
GO


CREATE INDEX [IX_DocumentosRecibidos_TipoMensaje_RecibidoEn] ON [DocumentosRecibidos] ([TipoMensaje], [RecibidoEn]);
GO


CREATE UNIQUE INDEX [IX_Empresas_Rnc] ON [Empresas] ([Rnc]);
GO


CREATE UNIQUE INDEX [IX_EntregasPendiente_PendienteEntregaId_Numero] ON [EntregasPendiente] ([PendienteEntregaId], [Numero]);
GO


CREATE UNIQUE INDEX [IX_FormasPago_Codigo] ON [FormasPago] ([Codigo]);
GO


CREATE INDEX [IX_FormasPago_Version] ON [FormasPago] ([Version]);
GO


CREATE UNIQUE INDEX [IX_Impuestos_Codigo] ON [Impuestos] ([Codigo]);
GO


CREATE INDEX [IX_Impuestos_Version] ON [Impuestos] ([Version]);
GO


CREATE INDEX [IX_ImpuestosVenta_ComprobanteId] ON [ImpuestosVenta] ([ComprobanteId]);
GO


CREATE UNIQUE INDEX [IX_LineasCotizacion_CotizacionId_ArticuloCodigo] ON [LineasCotizacion] ([CotizacionId], [ArticuloCodigo]);
GO


CREATE INDEX [IX_LineasEntregaPendiente_EntregaPendienteId] ON [LineasEntregaPendiente] ([EntregaPendienteId]);
GO


CREATE INDEX [IX_LineasPendienteEntrega_PendienteEntregaId] ON [LineasPendienteEntrega] ([PendienteEntregaId]);
GO


CREATE INDEX [IX_LineasReservaFactura_ReservaId] ON [LineasReservaFactura] ([ReservaId]);
GO


CREATE INDEX [IX_LineasVenta_Codigo] ON [LineasVenta] ([Codigo]);
GO


CREATE INDEX [IX_LineasVenta_ComprobanteId] ON [LineasVenta] ([ComprobanteId]);
GO


CREATE INDEX [IX_ListasBoda_ClienteDocumento] ON [ListasBoda] ([ClienteDocumento]);
GO


CREATE INDEX [IX_ListasBoda_FechaEvento] ON [ListasBoda] ([FechaEvento]);
GO


CREATE UNIQUE INDEX [IX_ListasBoda_Numero] ON [ListasBoda] ([Numero]);
GO


CREATE INDEX [IX_ListasBoda_SucursalId] ON [ListasBoda] ([SucursalId]);
GO


CREATE UNIQUE INDEX [IX_Marcas_Codigo] ON [Marcas] ([Codigo]);
GO


CREATE INDEX [IX_Marcas_Version] ON [Marcas] ([Version]);
GO


CREATE UNIQUE INDEX [IX_MiembrosFidelidad_Cedula] ON [MiembrosFidelidad] ([Cedula]);
GO


CREATE INDEX [IX_MiembrosFidelidad_NivelId] ON [MiembrosFidelidad] ([NivelId]);
GO


CREATE INDEX [IX_MiembrosFidelidad_Nombre] ON [MiembrosFidelidad] ([Nombre]);
GO


CREATE INDEX [IX_MiembrosFidelidad_Version] ON [MiembrosFidelidad] ([Version]);
GO


CREATE UNIQUE INDEX [IX_Monedas_Codigo] ON [Monedas] ([Codigo]);
GO


CREATE INDEX [IX_Monedas_Version] ON [Monedas] ([Version]);
GO


CREATE UNIQUE INDEX [IX_MotivosDescuento_Codigo] ON [MotivosDescuento] ([Codigo]);
GO


CREATE INDEX [IX_MotivosDescuento_Version] ON [MotivosDescuento] ([Version]);
GO


CREATE UNIQUE INDEX [IX_MotivosDevolucion_Codigo] ON [MotivosDevolucion] ([Codigo]);
GO


CREATE INDEX [IX_MotivosDevolucion_Version] ON [MotivosDevolucion] ([Version]);
GO


CREATE UNIQUE INDEX [IX_MotivosSuspension_Codigo] ON [MotivosSuspension] ([Codigo]);
GO


CREATE INDEX [IX_MotivosSuspension_Version] ON [MotivosSuspension] ([Version]);
GO


CREATE INDEX [IX_MovimientosPuntos_CajaId] ON [MovimientosPuntos] ([CajaId]);
GO


CREATE INDEX [IX_MovimientosPuntos_Cedula_Fecha] ON [MovimientosPuntos] ([Cedula], [Fecha]);
GO


CREATE UNIQUE INDEX [IX_MovimientosPuntos_Documento_Tipo] ON [MovimientosPuntos] ([Documento], [Tipo]) WHERE [Origen] = 0;
GO


CREATE INDEX [IX_MovimientosPuntos_SucursalId] ON [MovimientosPuntos] ([SucursalId]);
GO


CREATE UNIQUE INDEX [IX_NivelesFidelidad_Codigo] ON [NivelesFidelidad] ([Codigo]);
GO


CREATE INDEX [IX_NivelesFidelidad_Version] ON [NivelesFidelidad] ([Version]);
GO


CREATE INDEX [IX_NotasCredito_CajaId] ON [NotasCredito] ([CajaId]);
GO


CREATE INDEX [IX_NotasCredito_ClienteDocumento] ON [NotasCredito] ([ClienteDocumento]);
GO


CREATE UNIQUE INDEX [IX_NotasCredito_Encf] ON [NotasCredito] ([Encf]) WHERE [Encf] IS NOT NULL;
GO


CREATE INDEX [IX_NotasCredito_FechaEmision] ON [NotasCredito] ([FechaEmision]);
GO


CREATE UNIQUE INDEX [IX_NotasCredito_Numero] ON [NotasCredito] ([Numero]);
GO


CREATE INDEX [IX_NotasCredito_SucursalId] ON [NotasCredito] ([SucursalId]);
GO


CREATE INDEX [IX_PagosVenta_ComprobanteId] ON [PagosVenta] ([ComprobanteId]);
GO


CREATE INDEX [IX_Parametros_CajaId] ON [Parametros] ([CajaId]);
GO


CREATE UNIQUE INDEX [IX_Parametros_Clave_SucursalId_CajaId] ON [Parametros] ([Clave], [SucursalId], [CajaId]);
GO


CREATE INDEX [IX_Parametros_SucursalId] ON [Parametros] ([SucursalId]);
GO


CREATE INDEX [IX_PendientesEntrega_CajaId] ON [PendientesEntrega] ([CajaId]);
GO


CREATE INDEX [IX_PendientesEntrega_Estado_FechaComprometida] ON [PendientesEntrega] ([Estado], [FechaComprometida]);
GO


CREATE UNIQUE INDEX [IX_PendientesEntrega_Numero] ON [PendientesEntrega] ([Numero]);
GO


CREATE UNIQUE INDEX [IX_PendientesEntrega_NumeroCentral] ON [PendientesEntrega] ([NumeroCentral]) WHERE [NumeroCentral] IS NOT NULL;
GO


CREATE INDEX [IX_PendientesEntrega_SucursalId] ON [PendientesEntrega] ([SucursalId]);
GO


CREATE UNIQUE INDEX [IX_Promociones_Codigo] ON [Promociones] ([Codigo]);
GO


CREATE INDEX [IX_Promociones_Version] ON [Promociones] ([Version]);
GO


CREATE UNIQUE INDEX [IX_ReglasAcumulacion_Codigo] ON [ReglasAcumulacion] ([Codigo]);
GO


CREATE INDEX [IX_ReglasAcumulacion_Version] ON [ReglasAcumulacion] ([Version]);
GO


CREATE INDEX [IX_ReservasFactura_CajaId] ON [ReservasFactura] ([CajaId]);
GO


CREATE INDEX [IX_ReservasFactura_FacturaNumero_CerradaEn_VenceEn] ON [ReservasFactura] ([FacturaNumero], [CerradaEn], [VenceEn]);
GO


CREATE INDEX [IX_ReservasNotaCredito_CajaId] ON [ReservasNotaCredito] ([CajaId]);
GO


CREATE INDEX [IX_ReservasNotaCredito_NotaCreditoId_CerradaEn_VenceEn] ON [ReservasNotaCredito] ([NotaCreditoId], [CerradaEn], [VenceEn]);
GO


CREATE UNIQUE INDEX [IX_RolesCaja_Codigo] ON [RolesCaja] ([Codigo]);
GO


CREATE INDEX [IX_RolesCaja_Version] ON [RolesCaja] ([Version]);
GO


CREATE UNIQUE INDEX [IX_RolesCentral_Codigo] ON [RolesCentral] ([Codigo]);
GO


CREATE INDEX [IX_SaldosPuntos_ProximoVencimiento] ON [SaldosPuntos] ([ProximoVencimiento]);
GO


CREATE UNIQUE INDEX [IX_SecuenciasCentral_Prefijo] ON [SecuenciasCentral] ([Prefijo]);
GO


CREATE INDEX [IX_SecuenciasEcf_CajaId] ON [SecuenciasEcf] ([CajaId]);
GO


CREATE UNIQUE INDEX [IX_SecuenciasEcf_TipoComprobante_Desde] ON [SecuenciasEcf] ([TipoComprobante], [Desde]);
GO


CREATE INDEX [IX_SecuenciasEcf_Version] ON [SecuenciasEcf] ([Version]);
GO


CREATE INDEX [IX_SesionesCentral_Familia] ON [SesionesCentral] ([Familia]);
GO


CREATE UNIQUE INDEX [IX_SesionesCentral_TokenHash] ON [SesionesCentral] ([TokenHash]);
GO


CREATE INDEX [IX_SesionesCentral_UsuarioId_RevocadaEn] ON [SesionesCentral] ([UsuarioId], [RevocadaEn]);
GO


CREATE UNIQUE INDEX [IX_Sucursales_EmpresaId_Codigo] ON [Sucursales] ([EmpresaId], [Codigo]);
GO


CREATE UNIQUE INDEX [IX_SuspensionesCaja_CajaId_Numero] ON [SuspensionesCaja] ([CajaId], [Numero]);
GO


CREATE INDEX [IX_SuspensionesCaja_FechaOperacion_SucursalId_CajaId] ON [SuspensionesCaja] ([FechaOperacion], [SucursalId], [CajaId]);
GO


CREATE INDEX [IX_SuspensionesCaja_SucursalId] ON [SuspensionesCaja] ([SucursalId]);
GO


CREATE UNIQUE INDEX [IX_TasasCambio_Moneda_VigenteDesde] ON [TasasCambio] ([Moneda], [VigenteDesde]);
GO


CREATE INDEX [IX_TasasCambio_Version] ON [TasasCambio] ([Version]);
GO


CREATE UNIQUE INDEX [IX_TiposTarjeta_Codigo] ON [TiposTarjeta] ([Codigo]);
GO


CREATE INDEX [IX_TiposTarjeta_Version] ON [TiposTarjeta] ([Version]);
GO


CREATE INDEX [IX_TopesDescuento_ArticuloId] ON [TopesDescuento] ([ArticuloId]);
GO


CREATE INDEX [IX_TopesDescuento_CategoriaId] ON [TopesDescuento] ([CategoriaId]);
GO


CREATE UNIQUE INDEX [IX_TopesDescuento_Codigo] ON [TopesDescuento] ([Codigo]);
GO


CREATE INDEX [IX_TopesDescuento_DepartamentoId] ON [TopesDescuento] ([DepartamentoId]);
GO


CREATE INDEX [IX_TopesDescuento_MarcaId] ON [TopesDescuento] ([MarcaId]);
GO


CREATE INDEX [IX_TopesDescuento_Version] ON [TopesDescuento] ([Version]);
GO


CREATE UNIQUE INDEX [IX_UnidadesMedida_Codigo] ON [UnidadesMedida] ([Codigo]);
GO


CREATE INDEX [IX_UnidadesMedida_Version] ON [UnidadesMedida] ([Version]);
GO


CREATE UNIQUE INDEX [IX_UsuariosCaja_Codigo] ON [UsuariosCaja] ([Codigo]);
GO


CREATE INDEX [IX_UsuariosCaja_RolId] ON [UsuariosCaja] ([RolId]);
GO


CREATE INDEX [IX_UsuariosCaja_Version] ON [UsuariosCaja] ([Version]);
GO


CREATE INDEX [IX_UsuariosCajaCajas_CajaId] ON [UsuariosCajaCajas] ([CajaId]);
GO


CREATE UNIQUE INDEX [IX_UsuariosCentral_Codigo] ON [UsuariosCentral] ([Codigo]);
GO


CREATE INDEX [IX_UsuariosCentral_RolId] ON [UsuariosCentral] ([RolId]);
GO


CREATE INDEX [IX_VentasCentral_CajaId] ON [VentasCentral] ([CajaId]);
GO


CREATE INDEX [IX_VentasCentral_Encf] ON [VentasCentral] ([Encf]);
GO


CREATE INDEX [IX_VentasCentral_FechaOperacion_SucursalId_CajaId] ON [VentasCentral] ([FechaOperacion], [SucursalId], [CajaId]);
GO


CREATE UNIQUE INDEX [IX_VentasCentral_NumeroCentral] ON [VentasCentral] ([NumeroCentral]) WHERE [NumeroCentral] IS NOT NULL;
GO


CREATE INDEX [IX_VentasCentral_SucursalId] ON [VentasCentral] ([SucursalId]);
GO


CREATE UNIQUE INDEX [IX_VentasCentral_Tipo_Numero] ON [VentasCentral] ([Tipo], [Numero]);
GO

/* ------------------------------------------------------------------------
   Datos iniciales: solo el administrador del sistema.
   Todo lo demás (empresa, sucursales, cajas, maestros, artículos, precios,
   promociones, usuarios de caja y los demás parámetros) se crea desde el
   Central entrando con este usuario.

        Usuario:    ADMIN
        Contraseña: Admin.CGPOS#2026

   El RNC sembrado (000000002) es un marcador: corríjalo al entrar, en
   Organización -> Empresa.

   El sistema exige cambiarla en el primer ingreso.
   ------------------------------------------------------------------------ */

/* ------------------------------------------------------------------------
   Empresa: es la que sale en las facturas y la que se identifica ante la DGII.
   Todos estos datos, el RNC incluido, se editan luego en el Central, en
   Organización → Empresa. El RNC admite 9 dígitos (RNC) u 11 (cédula, si
   factura una persona física) y se le valida el dígito verificador.
   ------------------------------------------------------------------------ */

INSERT INTO [Empresas] ([Id], [Rnc], [RazonSocial], [NombreComercial], [Direccion], [Telefono], [ModificadoEn], [ModificadoPor])
VALUES (1, '000000002', N'CONTRERAS GROUP SRL', N'Contreras Group', N'Indique la dirección de la empresa', N'000-000-0000', SYSDATETIMEOFFSET(), N'Instalación');
ALTER SEQUENCE [SecuenciaEmpresas] RESTART WITH 11;
GO

INSERT INTO [RolesCentral] ([Id], [Codigo], [Nombre], [Activo], [ModificadoEn], [ModificadoPor])
VALUES (1, N'ADMINISTRADOR', N'Administrador', 1, SYSDATETIMEOFFSET(), N'Instalación');
ALTER SEQUENCE [SecuenciaRolesCentral] RESTART WITH 11;
GO

INSERT INTO [RolesCentralPermisos] ([RolId], [PermisoCodigo])
VALUES
    (1, N'Central.Seguridad.Administrar'),
    (1, N'Central.Auditoria.Consultar'),
    (1, N'Central.Organizacion.Administrar'),
    (1, N'Central.Dispositivos.Administrar'),
    (1, N'Central.Cajas.Configurar'),
    (1, N'Central.UsuariosCaja.Administrar'),
    (1, N'Central.Maestros.Administrar'),
    (1, N'Central.Clientes.CorregirDocumento'),
    (1, N'Central.Precios.Administrar'),
    (1, N'Central.Promociones.Administrar'),
    (1, N'Central.Fiscal.Administrar'),
    (1, N'Central.Sincronizacion.Monitorear'),
    (1, N'Central.NotasCredito.Administrar'),
    (1, N'Central.Fidelidad.Administrar'),
    (1, N'Central.ListasBoda.Administrar'),
    (1, N'Central.Cotizaciones.Administrar'),
    (1, N'Central.Despacho.Operar'),
    (1, N'Central.Despacho.Anular'),
    (1, N'Central.Reportes.Consultar'),
    (1, N'Central.CierresSucursal.Operar'),
    (1, N'Central.Cierres.Ajustar');
GO

INSERT INTO [UsuariosCentral] ([Id], [Codigo], [Nombre], [Correo], [RolId], [Activo], [ContrasenaHash],
                               [DebeCambiarContrasena], [ContrasenaCambiadaEn], [IntentosFallidos],
                               [BloqueadoHasta], [UltimoIngresoEn], [ModificadoEn], [ModificadoPor])
VALUES (1, N'ADMIN', N'Administrador del sistema', NULL, 1, 1, 'PBKDF2-SHA256$600000$JcgsbpqdKcOjE/bpuy1rMQ==$LnYGL+oBnoN33q3pqyrXrJYL4Wob2nE81uQujhYni0c=', 1, NULL, 0, NULL, NULL,
        SYSDATETIMEOFFSET(), N'Instalación');
ALTER SEQUENCE [SecuenciaUsuariosCentral] RESTART WITH 11;
GO

/* Parámetros con los que el sistema arranca usable; se cambian en el Central (Organización → Parámetros).
   Los que dependen de la empresa o del ambiente (direcciones de la DGII, tipo de ingresos, textos y políticas)
   quedan sin valor a propósito: el Central avisa cuáles faltan. */
INSERT INTO [Parametros] ([Id], [Clave], [Valor], [Descripcion], [SucursalId], [CajaId], [ModificadoEn], [ModificadoPor])
VALUES
    (1, N'Central.Seguridad.IntentosMaximos', N'5', N'Intentos de contraseña fallidos que bloquean al usuario', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (2, N'Central.Seguridad.MinutosBloqueo', N'15', N'Minutos que dura el bloqueo por intentos fallidos', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (3, N'Central.Seguridad.MinutosToken', N'15', N'Minutos de vigencia del token de acceso', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (4, N'Central.Seguridad.MinutosInactividad', N'60', N'Minutos sin actividad tras los que vence la sesión', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (5, N'Central.Seguridad.HorasSesion', N'12', N'Horas máximas de una sesión', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (6, N'Central.Seguridad.LargoMinimoContrasena', N'10', N'Largo mínimo de las contraseñas de los usuarios del Central', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (7, N'Central.Seguridad.LargoMinimoClaveCaja', N'6', N'Largo mínimo de la clave de los usuarios de caja', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (8, N'Central.Seguridad.ContrasenaCompleja', N'true', N'Exige mayúscula, minúscula, número y símbolo', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (9, N'Central.Dispositivos.MinutosToken', N'30', N'Minutos de vigencia del token de una caja', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (10, N'Central.Sincronizacion.FilasPorPagina', N'5000', N'Filas por página de la bajada de maestros a las cajas', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (11, N'Central.Dgii.SegundosCiclo', N'30', N'Segundos entre envíos de e-CF a la DGII', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (12, N'Central.Dgii.LoteEnvio', N'50', N'Comprobantes por lote de envío a la DGII', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (13, N'Central.Dgii.MinutosReintento', N'5', N'Minutos antes de reintentar un envío fallido', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (14, N'Central.Dgii.MinutosMaximoReintento', N'120', N'Tope de espera entre reintentos', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (15, N'Central.Dgii.SegundosConsultaEstado', N'60', N'Segundos entre consultas del resultado a la DGII', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (16, N'Central.Monitor.MinutosSinComunicacion', N'30', N'Minutos sin comunicación tras los que una caja es alerta', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (17, N'Central.Monitor.MinutosAlertaDgii', N'60', N'Minutos sin resultado de la DGII tras los que un e-CF es alerta', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (18, N'Central.NotasCredito.MinutosReserva', N'10', N'Minutos que se retiene el saldo de una nota mientras la caja cobra', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (19, N'Central.Devoluciones.MinutosReserva', N'10', N'Minutos que se retienen las líneas de una factura mientras otra tienda le hace la nota de crédito', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (20, N'Central.Fidelidad.MinutosCicloVencimiento', N'60', N'Minutos entre revisiones de los puntos vencidos', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (21, N'Central.Fidelidad.LoteVencimiento', N'500', N'Miembros por lote al vencer puntos', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (22, N'Central.Cotizaciones.DiasVigencia', N'15', N'Días que vale una cotización desde que se hace', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (23, N'Central.Despacho.MinutosCicloAvisos', N'15', N'Minutos entre avisos de pedidos preparados', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (24, N'Central.Despacho.LoteAvisos', N'50', N'Avisos por lote', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (25, N'Seguridad.IntentosMaximosClave', N'3', N'Intentos de clave fallidos que bloquean al usuario de la caja', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (26, N'Seguridad.MinutosBloqueo', N'5', N'Minutos que dura el bloqueo del usuario de la caja', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (27, N'Seguridad.MinutosVigenciaAutorizacion', N'5', N'Minutos para usar una autorización de supervisor', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (28, N'Seguridad.HorasSesion', N'12', N'Horas que dura la sesión en la caja', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (29, N'General.MonedaLocal', N'DOP', N'Moneda local del negocio', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (30, N'Caja.FondoPredeterminado', N'0.00', N'Fondo sugerido al abrir turno', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (31, N'Ventas.CantidadMaximaDigitada', N'10', N'Cantidad máxima que el cajero puede digitar de un artículo de unidad entera', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (32, N'Caja.PasoRedondeoEfectivo', N'0', N'Múltiplo al que se redondea el cobro en efectivo (0 = sin redondeo)', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (33, N'Caja.FondoEnCuadre', N'false', N'El fondo forma parte del efectivo esperado en el cierre', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (34, N'Caja.BloquearVentaTurnoDiaAnterior', N'true', N'Con un turno abierto de un día anterior la caja no vende ni cobra', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (35, N'Numeracion.DigitosSecuencia', N'7', N'Dígitos de la secuencia en el número de los documentos', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (36, N'Pantallas.SegundosAviso', N'6', N'Segundos que dura un aviso en la pantalla de la caja antes de quitarse solo (0 = hasta que lo cierren)', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (37, N'Fiscal.MontoIdentificacionConsumo', N'250000', N'Total desde el cual la factura de consumo exige cédula o RNC', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (38, N'Fiscal.PorcentajeAlertaSecuenciaEcf', N'10', N'Porcentaje restante de un rango de e-CF desde el cual se alerta', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (39, N'Fiscal.ComprobantesAlertaSecuenciaEcf', N'10', N'Cantidad de comprobantes restantes de un rango de e-CF desde la cual se alerta al facturar', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (40, N'Fiscal.DiasAlertaCertificado', N'30', N'Días antes del vencimiento del certificado para alertar', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (41, N'Fiscal.PorcentajeRetencionLey3223', N'0', N'Retención de la Ley 32-23 en facturas gubernamentales E45 (0 = sin retención)', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (42, N'Devoluciones.DiasRetencionImpuesto', N'30', N'Días desde la factura tras los cuales la devolución retiene el ITBIS', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (43, N'Devoluciones.DiasVigenciaNotaCredito', N'180', N'Días desde la emisión en que se puede consumir una nota de crédito', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (44, N'Fidelidad.ValorPunto', N'1', N'Valor en dinero de cada punto al canjearlo', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (45, N'Fidelidad.MesesVigenciaPuntos', N'12', N'Meses que duran los puntos acumulados', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (46, N'Fidelidad.MinimoPuntosCanje', N'50', N'Puntos mínimos para poder canjear', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (47, N'Fidelidad.MaximoPuntosCanjeSinConexion', N'2000', N'Tope de puntos a canjear sin conexión con el Central', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (48, N'Sincronizacion.DiasRetencionXmlEnviados', N'90', N'Días que se conservan los XML ya enviados', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (49, N'Sincronizacion.DiasRetencionMensajesConfirmados', N'60', N'Días que se conservan los mensajes confirmados', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (50, N'Sincronizacion.AlertaTamanoBaseDatosMb', N'8000', N'Tamaño de la base de la caja que dispara alerta', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (51, N'Sincronizacion.HorasAlertaPendientes', N'24', N'Horas con documentos sin sincronizar que disparan alerta', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (52, N'Respaldo.DiasRetencion', N'7', N'Días que se conservan los respaldos de la caja', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (53, N'Reloj.ToleranciaSegundos', N'60', N'Diferencia de hora tolerada contra el servidor NTP', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (54, N'Balanza.PrefijoPeso', N'21', N'Prefijo de las etiquetas de balanza con peso', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (55, N'Balanza.PrefijoPrecio', N'22', N'Prefijo de las etiquetas de balanza con precio', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (56, N'Balanza.DigitosCodigoArticulo', N'5', N'Dígitos del código del artículo en la etiqueta', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (57, N'Balanza.DigitosValor', N'5', N'Dígitos del valor en la etiqueta', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (58, N'Balanza.DecimalesPeso', N'3', N'Decimales del peso en la etiqueta', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación'),
    (59, N'Balanza.DecimalesPrecio', N'2', N'Decimales del precio en la etiqueta', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación');
ALTER SEQUENCE [SecuenciaParametros] RESTART WITH 61;
GO

/* Monedas */
INSERT INTO [Monedas] ([Id], [Codigo], [Nombre], [Simbolo], [Activa], [ModificadoEn], [ModificadoPor])
VALUES
    (1, 'DOP', N'Peso dominicano', N'RD$', 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (2, 'USD', N'Dólar estadounidense', N'US$', 1, SYSDATETIMEOFFSET(), N'Instalación');
ALTER SEQUENCE [SecuenciaMonedas] RESTART WITH 11;
GO

/* Impuestos (ITBIS y exento) */
INSERT INTO [Impuestos] ([Id], [Codigo], [Nombre], [Porcentaje], [IndicadorFacturacion], [Activo], [ModificadoEn], [ModificadoPor])
VALUES
    (1, N'ITBIS18', N'ITBIS 18%', 18.00, 1, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (2, N'ITBIS16', N'ITBIS 16%', 16.00, 2, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (3, N'ITBIS0', N'ITBIS 0%', 0.00, 3, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (4, N'EXENTO', N'Exento', 0.00, 4, 1, SYSDATETIMEOFFSET(), N'Instalación');
ALTER SEQUENCE [SecuenciaImpuestos] RESTART WITH 11;
GO

/* Unidades de medida */
INSERT INTO [UnidadesMedida] ([Id], [Codigo], [Abreviatura], [Nombre], [PermiteDecimales], [Decimales], [ModificadoEn], [ModificadoPor])
VALUES
    (1, 1, N'UND', N'Unidad', 0, 0, SYSDATETIMEOFFSET(), N'Instalación'),
    (2, 2, N'LB', N'Libra', 1, 3, SYSDATETIMEOFFSET(), N'Instalación'),
    (3, 3, N'PIE', N'Pie', 1, 2, SYSDATETIMEOFFSET(), N'Instalación'),
    (4, 4, N'YD', N'Yarda', 1, 2, SYSDATETIMEOFFSET(), N'Instalación'),
    (5, 5, N'GAL', N'Galón', 1, 2, SYSDATETIMEOFFSET(), N'Instalación');
ALTER SEQUENCE [SecuenciaUnidadesMedida] RESTART WITH 11;
GO

/* Denominaciones del efectivo (para el cuadre) */
INSERT INTO [Denominaciones] ([Id], [Moneda], [Valor], [Tipo], [Activa], [ModificadoEn], [ModificadoPor])
VALUES
    (1, 'DOP', 2000.00, 0, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (2, 'DOP', 1000.00, 0, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (3, 'DOP', 500.00, 0, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (4, 'DOP', 200.00, 0, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (5, 'DOP', 100.00, 0, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (6, 'DOP', 50.00, 0, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (7, 'DOP', 25.00, 1, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (8, 'DOP', 10.00, 1, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (9, 'DOP', 5.00, 1, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (10, 'DOP', 1.00, 1, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (11, 'USD', 100.00, 0, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (12, 'USD', 50.00, 0, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (13, 'USD', 20.00, 0, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (14, 'USD', 10.00, 0, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (15, 'USD', 5.00, 0, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (16, 'USD', 1.00, 0, 1, SYSDATETIMEOFFSET(), N'Instalación');
ALTER SEQUENCE [SecuenciaDenominaciones] RESTART WITH 21;
GO

/* Tipos de tarjeta */
INSERT INTO [TiposTarjeta] ([Id], [Codigo], [Nombre], [Activo], [ModificadoEn], [ModificadoPor])
VALUES
    (1, 1, N'Visa', 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (2, 2, N'Mastercard', 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (3, 3, N'American Express', 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (4, 4, N'Discover', 1, SYSDATETIMEOFFSET(), N'Instalación');
ALTER SEQUENCE [SecuenciaTiposTarjeta] RESTART WITH 11;
GO

/* Motivos de descuento */
INSERT INTO [MotivosDescuento] ([Id], [Codigo], [Nombre], [Activo], [ModificadoEn], [ModificadoPor])
VALUES
    (1, 1, N'Cliente frecuente', 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (2, 2, N'Producto con daño o defecto', 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (3, 3, N'Ajuste de precio', 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (4, 4, N'Autorizado por gerencia', 1, SYSDATETIMEOFFSET(), N'Instalación');
ALTER SEQUENCE [SecuenciaMotivosDescuento] RESTART WITH 11;
GO

/* Motivos de devolución */
INSERT INTO [MotivosDevolucion] ([Id], [Codigo], [Nombre], [Activo], [ModificadoEn], [ModificadoPor])
VALUES
    (1, 1, N'Artículo defectuoso', 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (2, 2, N'Artículo equivocado', 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (3, 3, N'Cliente no satisfecho', 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (4, 4, N'Garantía', 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (5, 5, N'Error de facturación', 1, SYSDATETIMEOFFSET(), N'Instalación');
ALTER SEQUENCE [SecuenciaMotivosDevolucion] RESTART WITH 11;
GO

/* Motivos de caja parada */
INSERT INTO [MotivosSuspension] ([Id], [Codigo], [Nombre], [Programado], [ExigeNota], [Activo], [ModificadoEn], [ModificadoPor])
VALUES
    (1, 1, N'Baño', 0, 0, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (2, 2, N'Almuerzo', 1, 0, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (3, 3, N'Receso', 1, 0, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (4, 4, N'Llamado del supervisor', 0, 0, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (5, 5, N'Otro', 0, 1, 1, SYSDATETIMEOFFSET(), N'Instalación');
ALTER SEQUENCE [SecuenciaMotivosSuspension] RESTART WITH 11;
GO

/* Numeración de los documentos que emite el Central. Sin la fila de un documento, ese documento no se
   puede crear: la numeración es una decisión del negocio, no algo que el sistema invente. */
INSERT INTO [SecuenciasCentral] ([Codigo], [Prefijo], [Documento], [Ultimo], [Digitos], [Activa])
VALUES
    ('Factura', 'FAC', N'Factura', 0, 6, 1),
    ('NotaCredito', 'NC', N'Nota de crédito', 0, 6, 1),
    ('Despacho', 'DES', N'Despacho', 0, 6, 1),
    ('CierreSucursal', 'CS', N'Cierre de sucursal', 0, 6, 1),
    ('Cotizacion', 'COT', N'Cotización', 0, 6, 1),
    ('ListaBoda', 'LB', N'Lista de boda', 0, 6, 1),
    ('Promocion', 'PRO', N'Promoción', 0, 6, 1);
GO

/* Formas de pago */
INSERT INTO [FormasPago] ([Id], [Codigo], [Nombre], [Tipo], [Moneda], [Orden], [AbreGaveta], [PermiteDevuelta], [RequiereReferencia], [RequiereBanco], [PermiteComprobanteFiscal], [Activa], [ModificadoEn], [ModificadoPor])
VALUES
    (1, N'EFE', N'Efectivo', 0, 'DOP', 1, 1, 1, 0, 0, 1, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (2, N'TAR', N'Tarjeta', 1, 'DOP', 2, 0, 0, 1, 0, 1, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (3, N'TRA', N'Transferencia', 2, 'DOP', 3, 0, 0, 1, 1, 1, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (4, N'CHE', N'Cheque', 3, 'DOP', 4, 0, 0, 1, 1, 1, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (5, N'USD', N'Dólares', 9, 'USD', 5, 1, 1, 0, 0, 1, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (6, N'NC', N'Nota de crédito', 5, 'DOP', 6, 0, 0, 1, 0, 1, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (7, N'BONO', N'Bono de regalo', 4, 'DOP', 7, 0, 0, 1, 0, 0, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (8, N'GIFT', N'Tarjeta de regalo', 7, 'DOP', 8, 0, 0, 1, 0, 0, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (9, N'PRE', N'Préstamo bancario', 6, 'DOP', 9, 0, 0, 1, 1, 1, 1, SYSDATETIMEOFFSET(), N'Instalación'),
    (10, N'PUN', N'Puntos', 8, 'DOP', 10, 0, 0, 0, 0, 1, 1, SYSDATETIMEOFFSET(), N'Instalación');
ALTER SEQUENCE [SecuenciaFormasPago] RESTART WITH 21;
GO


PRINT 'Base del Central creada. Entre al Central con el usuario ADMIN y cambie su contraseña.';
GO
