namespace CgPos.Dominio.Comun;

/// <summary>
/// La base de datos no existe o no tiene el esquema. El sistema no crea ni cambia bases: se crean con los scripts de
/// <c>scripts/base-datos</c>, así que el arranque solo avisa qué falta y con cuál script se resuelve.
/// </summary>
public sealed class BaseDatosNoPreparadaExcepcion(string mensaje, Exception? interna = null) : Exception(mensaje, interna);
