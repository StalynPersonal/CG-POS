using System.Collections.Frozen;

namespace CgPos.ECF.Documentos;

/// <summary>
/// Unidades de medida del e-CF: la DGII las recibe como un código numérico de su tabla, no como la sigla que usa la caja.
/// Lo que no esté en la tabla no se envía: el esquema rechaza cualquier valor fuera de ella.
/// </summary>
public static class UnidadesMedidaEcf
{
    private static readonly FrozenDictionary<string, string> PorSigla = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["BARR"] = "1", ["BOL"] = "2", ["BOT"] = "3", ["BULTO"] = "4", ["BOTELLA"] = "5", ["CAJ"] = "6", ["CAJA"] = "6",
        ["CAJETILLA"] = "7", ["CM"] = "8", ["CIL"] = "9", ["CONJ"] = "10", ["CONT"] = "11", ["DIA"] = "12", ["DÍA"] = "12",
        ["DOC"] = "13", ["FARD"] = "14", ["GL"] = "15", ["GAL"] = "15", ["GRAD"] = "16", ["GR"] = "17", ["GRAMO"] = "17",
        ["GRAN"] = "18", ["HOR"] = "19", ["HORA"] = "19", ["HUAC"] = "20", ["KG"] = "21", ["KWH"] = "22", ["LB"] = "23",
        ["LIBRA"] = "23", ["LITRO"] = "24", ["LT"] = "24", ["L"] = "24", ["LOT"] = "25", ["M"] = "26", ["MT"] = "26",
        ["M2"] = "27", ["M3"] = "28", ["MMBTU"] = "29", ["MIN"] = "30", ["PAQ"] = "31", ["PAR"] = "32", ["PIE"] = "33",
        ["PZA"] = "34", ["ROL"] = "35", ["SOBR"] = "36", ["SEG"] = "37", ["TANQUE"] = "38", ["TONE"] = "39", ["TON"] = "39",
        ["TUB"] = "40", ["YD"] = "41", ["YD2"] = "42", ["UND"] = "43", ["UNI"] = "43", ["UNIDAD"] = "43", ["EA"] = "44",
        ["MILLAR"] = "45", ["SAC"] = "46", ["LAT"] = "47", ["DIS"] = "48", ["BID"] = "49", ["RAC"] = "50", ["Q"] = "51",
        ["GRT"] = "52", ["P2"] = "53", ["PAX"] = "54", ["PULG"] = "55", ["STAY"] = "56", ["BDJ"] = "57", ["HA"] = "58",
        ["ML"] = "59", ["MG"] = "60", ["OZ"] = "61", ["OZT"] = "62",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <returns>El código de la DGII para esa sigla, el mismo valor si ya es un código válido, o nulo si no corresponde a ninguna.</returns>
    public static string? Codigo(string? unidad)
    {
        if (string.IsNullOrWhiteSpace(unidad))
            return null;

        var texto = unidad.Trim();
        if (PorSigla.TryGetValue(texto, out var codigo))
            return codigo;

        return PorSigla.Values.Contains(texto) ? texto : null;
    }
}
