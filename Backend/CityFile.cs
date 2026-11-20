using System.Globalization;

namespace DeliveryRoutes.Backend;

public sealed record CityMap(Graph<string> Graph, int StreetCount, int OneWayCount);

public static class CityFile
{
    private const string TwoWay = "TwoWay";
    private const string OneWay = "OneWay";

    public static CityMap Load(string path)
    {
        var graph = new Graph<string>(directed: true);
        var errors = new List<string>();
        var seenStreets = new Dictionary<(string, string), int>();
        var oneWayCount = 0;

        foreach (var (lineNumber, fields) in FlatFile.Read(path))
        {
            var prefix = $"Línea {lineNumber}:";
            if (fields.Length != 4)
            {
                errors.Add($"{prefix} se esperaban 4 campos (from,to,distancia,sentido) y hay {fields.Length}.");
                continue;
            }

            var (from, to, distanceText, direction) = (fields[0], fields[1], fields[2], fields[3]);
            var lineIsValid = true;

            if (from.Length == 0 || to.Length == 0)
            {
                errors.Add($"{prefix} el origen y el destino no pueden estar vacíos.");
                lineIsValid = false;
            }
            else if (from == to)
            {
                errors.Add($"{prefix} la calle empieza y termina en la misma ubicación ({from}).");
                lineIsValid = false;
            }

            if (!double.TryParse(distanceText, NumberStyles.Float, CultureInfo.InvariantCulture, out var meters)
                || !double.IsFinite(meters) || meters <= 0)
            {
                errors.Add($"{prefix} la distancia '{distanceText}' no es un número positivo.");
                lineIsValid = false;
            }

            var isTwoWay = direction.Equals(TwoWay, StringComparison.OrdinalIgnoreCase);
            var isOneWay = direction.Equals(OneWay, StringComparison.OrdinalIgnoreCase);
            if (!isTwoWay && !isOneWay)
            {
                errors.Add($"{prefix} el sentido '{direction}' no es {TwoWay} ni {OneWay}.");
                lineIsValid = false;
            }

            if (!lineIsValid)
            {
                continue;
            }

            var key = string.CompareOrdinal(from, to) < 0 ? (from, to) : (to, from);
            if (seenStreets.TryGetValue(key, out var firstLine))
            {
                errors.Add($"{prefix} la calle {from}-{to} ya aparece en la línea {firstLine}.");
                continue;
            }

            seenStreets[key] = lineNumber;
            graph.AddEdge(from, to, meters);
            if (isTwoWay)
            {
                graph.AddEdge(to, from, meters);
            }
            else
            {
                oneWayCount++;
            }
        }

        if (errors.Count == 0 && seenStreets.Count == 0)
        {
            errors.Add("El archivo no tiene ninguna calle.");
        }

        if (errors.Count > 0)
        {
            throw new InputValidationException($"Errores en el mapa '{path}':", errors);
        }

        return new CityMap(graph, seenStreets.Count, oneWayCount);
    }
}
