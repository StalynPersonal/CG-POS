namespace CgPos.Contratos.Central;

/// <summary>Maestro publicado tal como baja a las cajas, con cuándo y quién lo cambió por última vez.</summary>
public sealed record DatosMaestroCentral<T>(T Dato, DateTimeOffset ModificadoEn, string ModificadoPor);
