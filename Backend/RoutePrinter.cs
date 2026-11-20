using System.Globalization;
using System.Text;

namespace DeliveryRoutes.Backend;

public static class RoutePrinter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Format(CityMap city, IReadOnlyList<Package> packages, string start,
                                RoutePlanner planner, List<Stop> route)
    {
        var output = new StringBuilder();
        output.AppendLine($"Posición inicial: {start}");
        output.AppendLine($"Paquetes: {packages.Count}   Ubicaciones: {city.Graph.VertexCount}   " +
                          $"Calles: {city.StreetCount} ({city.OneWayCount} de un solo sentido)");
        output.AppendLine();

        var totalMeters = 0.0;
        var current = start;
        var number = 0;

        foreach (var group in GroupByLocation(route))
        {
            number++;
            var location = group[0].Location;
            var actions = DescribeActions(group);

            if (location == current)
            {
                output.AppendLine($"{number,2}. En {location}: {actions}.");
                continue;
            }

            var meters = planner.Distance(current, location);
            totalMeters += meters;
            output.AppendLine($"{number,2}. Ir de {current} a {location} ({FormatMeters(meters)} m): {actions}.");
            output.AppendLine($"      Camino: {string.Join(" -> ", planner.PathBetween(current, location))}");
            current = location;
        }

        output.AppendLine();
        output.AppendLine("Todos los paquetes fueron recogidos y entregados.");
        output.AppendLine($"Recorrido total: {(totalMeters / 1000).ToString("F2", Invariant)} km");
        return output.ToString();
    }

    private static List<List<Stop>> GroupByLocation(List<Stop> route)
    {
        var groups = new List<List<Stop>>();
        foreach (var stop in route)
        {
            if (groups.Count > 0 && groups[^1][0].Location == stop.Location)
            {
                groups[^1].Add(stop);
            }
            else
            {
                groups.Add(new List<Stop> { stop });
            }
        }

        return groups;
    }

    private static string DescribeActions(List<Stop> group)
    {
        var pickedHere = group.Where(s => s.Kind == StopKind.Pickup).Select(s => s.PackageId).ToHashSet();
        var deliveries = group.Where(s => s.Kind == StopKind.Delivery).Select(s => s.PackageId).ToList();

        var parts = new List<string>();
        AddAction(parts, "entregar", deliveries.Where(id => !pickedHere.Contains(id)).ToList());
        AddAction(parts, "recoger", pickedHere.ToList());
        AddAction(parts, "entregar", deliveries.Where(pickedHere.Contains).ToList());
        return string.Join(" y ", parts);
    }

    private static void AddAction(List<string> parts, string verb, List<int> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var noun = ids.Count == 1 ? "paquete" : "paquetes";
        parts.Add($"{verb} {noun} {JoinSpanish(ids.Select(id => id.ToString(Invariant)).ToList())}");
    }

    private static string JoinSpanish(List<string> items) =>
        items.Count == 1 ? items[0] : string.Join(", ", items.Take(items.Count - 1)) + " y " + items[^1];

    private static string FormatMeters(double meters) => meters.ToString("0.##", Invariant);
}
