using System.Text;

namespace CgPos.Contratos.Importacion;

/// <summary>Lectura mínima de CSV: separador configurable y campos entre comillas dobles ("" = comilla).</summary>
public static class LectorCsv
{
    public static char DetectarSeparador(string encabezado) =>
        encabezado.Count(c => c == ';') > encabezado.Count(c => c == ',') ? ';' : ',';

    public static string[] DividirLinea(string linea, char separador)
    {
        var campos = new List<string>();
        var actual = new StringBuilder();
        var entreComillas = false;

        for (var posicion = 0; posicion < linea.Length; posicion++)
        {
            var caracter = linea[posicion];
            if (entreComillas)
            {
                if (caracter != '"')
                    actual.Append(caracter);
                else if (posicion + 1 < linea.Length && linea[posicion + 1] == '"')
                {
                    actual.Append('"');
                    posicion++;
                }
                else
                    entreComillas = false;
            }
            else if (caracter == '"')
                entreComillas = true;
            else if (caracter == separador)
            {
                campos.Add(actual.ToString());
                actual.Clear();
            }
            else
                actual.Append(caracter);
        }

        campos.Add(actual.ToString());
        return campos.ToArray();
    }
}
