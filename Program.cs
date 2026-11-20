using System.Diagnostics;
using System.Globalization;
using System.Text;
using DeliveryRoutes.Backend;

Console.OutputEncoding = Encoding.UTF8;

var compare = args.Contains("--comparar");
var positional = args.Where(a => a != "--comparar").ToArray();

string cityPath, packagesPath, start;
if (positional.Length == 3)
{
    (cityPath, packagesPath, start) = (positional[0], positional[1], positional[2]);
}
else if (positional.Length == 0)
{
    cityPath = Ask("Archivo del mapa", "city.txt");
    packagesPath = Ask("Archivo de paquetes", "packages.txt");
    start = Ask("Posición inicial", "D");
    Console.WriteLine();
}
else
{
    Console.Error.WriteLine("Uso: dotnet run -- <city.txt> <packages.txt> <posición inicial> [--comparar]");
    return 1;
}

try
{
    var city = CityFile.Load(cityPath);
    var packages = PackageFile.Load(packagesPath, city.Graph);
    var planner = new RoutePlanner(city.Graph, packages, start);

    var route = planner.Plan();
    if (!planner.IsValid(route))
    {
        Console.Error.WriteLine("Error interno: la ruta calculada no es válida.");
        return 1;
    }

    Console.Write(RoutePrinter.Format(city, packages, start, planner, route));

    if (compare)
    {
        PrintComparison(planner);
    }

    return 0;
}
catch (InputValidationException ex)
{
    Console.Error.WriteLine(ex.Title);
    foreach (var error in ex.Errors)
    {
        Console.Error.WriteLine($"  - {error}");
    }

    return 1;
}

static string Ask(string label, string defaultValue)
{
    Console.Write($"{label} [{defaultValue}]: ");
    var answer = Console.ReadLine()?.Trim();
    return string.IsNullOrEmpty(answer) ? defaultValue : answer;
}

static void PrintComparison(RoutePlanner planner)
{
    var strategies = new (string Name, Func<List<Stop>> Run)[]
    {
        ("Vecino más cercano (sin mejoras)", planner.PlanNearestNeighbor),
        ("Inserción + mejoras + reinicios", planner.Plan),
    };

    Console.WriteLine();
    Console.WriteLine($"{"Estrategia",-36} {"Recorrido",12} {"Tiempo",10}");
    foreach (var (name, run) in strategies)
    {
        var watch = Stopwatch.StartNew();
        var route = run();
        watch.Stop();
        var km = (planner.TotalDistance(route) / 1000).ToString("F2", CultureInfo.InvariantCulture);
        Console.WriteLine($"{name,-36} {km + " km",12} {watch.ElapsedMilliseconds + " ms",10}");
    }
}
