using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

if (args.Length < 3)
{
    Console.Error.WriteLine("usage: dotnet run tools/TestReport.cs -- <output.md> <coverage-summary.md|-> <xunit-result.xml>...");
    return 1;
}

var output = args[0];
var coverageSummary = args[1];
var results = args.Skip(2).Select(XDocument.Load).ToList();

var requirements = new (string Id, string Description)[]
{
    ("R1", ".NET service using ASP.NET Core"),
    ("R2", "Architecture that makes integrating new APIs easy"),
    ("R3", "Data from at least three external APIs, simultaneously"),
    ("R4", "Endpoint returning the aggregated data"),
    ("R5", "Filtering and sorting (date, relevance, category)"),
    ("R6", "Error handling, transient failures, fallback"),
    ("R7", "Unit tests"),
    ("R8", "Documentation"),
    ("R9", "Caching"),
    ("R10", "Parallelism"),
    ("R11", "Request statistics with performance buckets"),
    ("O1", "JWT bearer authentication"),
    ("O2", "Performance anomaly logging (background service)")
};

var assemblies = results.SelectMany(document => document.Descendants("assembly")).ToList();
var scenarios = assemblies
    .SelectMany(assembly => assembly.Descendants("test"))
    .GroupBy(test => (Type: (string)test.Attribute("type")!, Method: (string)test.Attribute("method")!))
    .Select(cases => new Scenario(
        cases.Key.Type,
        cases.Key.Method,
        cases.Count(),
        cases.Count(test => (string?)test.Attribute("result") == "Pass"),
        cases.Count(test => (string?)test.Attribute("result") == "Fail"),
        cases.Sum(test => double.Parse((string?)test.Attribute("time") ?? "0", CultureInfo.InvariantCulture)),
        Traits(cases.First(), "Requirement").Distinct().ToList(),
        Traits(cases.First(), "Scenario").Contains("Failure")))
    .OrderBy(scenario => scenario.Type, StringComparer.Ordinal)
    .ThenBy(scenario => scenario.Method, StringComparer.Ordinal)
    .ToList();

var total = assemblies.Sum(assembly => Int(assembly, "total"));
var passed = assemblies.Sum(assembly => Int(assembly, "passed"));
var failed = assemblies.Sum(assembly => Int(assembly, "failed"));
var skipped = assemblies.Sum(assembly => Int(assembly, "skipped"));
var seconds = assemblies.Sum(assembly => double.Parse((string?)assembly.Attribute("time") ?? "0", CultureInfo.InvariantCulture));

var report = new StringBuilder();
report.AppendLine("# Test report");
report.AppendLine();
report.AppendLine(CultureInfo.InvariantCulture, $"Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC from the xUnit results of the Docker `test-report` stage.");
report.AppendLine();
report.AppendLine(CultureInfo.InvariantCulture, $"**{total} tests · {passed} passed · {failed} failed · {skipped} skipped · {seconds:0.0} s** {(failed == 0 ? "✅" : "❌")}");
report.AppendLine();
report.AppendLine("| Test project | Tests | Passed | Failed | Time |");
report.AppendLine("|---|---|---|---|---|");
foreach (var assembly in assemblies)
{
    var name = Path.GetFileNameWithoutExtension((string?)assembly.Attribute("name") ?? "?");
    report.AppendLine(CultureInfo.InvariantCulture,
        $"| {name} | {Int(assembly, "total")} | {Int(assembly, "passed")} | {Int(assembly, "failed")} | {double.Parse((string?)assembly.Attribute("time") ?? "0", CultureInfo.InvariantCulture):0.0} s |");
}

report.AppendLine();
report.AppendLine("Each test is tagged with the requirement(s) it proves and whether it emulates a success or a failure scenario. A test with several data rows (a theory) counts as one scenario.");
report.AppendLine();
report.AppendLine("## Coverage by requirement");
report.AppendLine();
report.AppendLine("| Requirement | | Success scenarios | Failure scenarios | Result |");
report.AppendLine("|---|---|---|---|---|");
foreach (var (id, description) in requirements)
{
    var covering = scenarios.Where(scenario => scenario.Requirements.Contains(id)).ToList();
    var result = id switch
    {
        "R1" => string.Create(CultureInfo.InvariantCulture, $"every integration test hosts the ASP.NET Core app ({IntegrationTotal(assemblies)} tests)"),
        "R7" => "this report",
        _ when covering.Count == 0 => "–",
        _ => covering.All(scenario => scenario.Failed == 0) ? "✅" : "❌"
    };
    report.AppendLine(CultureInfo.InvariantCulture,
        $"| **{id}** | {description} | {covering.Count(scenario => !scenario.IsFailure)} | {covering.Count(scenario => scenario.IsFailure)} | {result} |");
}

foreach (var (id, description) in requirements)
{
    var covering = scenarios.Where(scenario => scenario.Requirements.Contains(id)).ToList();
    if (covering.Count == 0)
        continue;

    report.AppendLine();
    report.AppendLine(CultureInfo.InvariantCulture, $"## {id}: {description}");
    report.AppendLine();
    report.AppendLine("| Scenario | Type | Result | Test |");
    report.AppendLine("|---|---|---|---|");
    foreach (var scenario in covering.OrderBy(scenario => scenario.IsFailure).ThenBy(scenario => scenario.Type, StringComparer.Ordinal))
    {
        var cases = scenario.Cases > 1 ? $" ({scenario.Cases} cases)" : string.Empty;
        var outcome = scenario.Failed > 0 ? $"❌ {scenario.Failed} failed" : scenario.Passed == scenario.Cases ? "✅" : "⚠️ skipped";
        report.AppendLine(CultureInfo.InvariantCulture,
            $"| {Humanize(scenario.Method)}{cases} | {(scenario.IsFailure ? "failure" : "success")} | {outcome} | `{ShortType(scenario.Type)}` |");
    }
}

var untagged = scenarios.Where(scenario => scenario.Requirements.Count == 0).ToList();
if (untagged.Count > 0)
{
    report.AppendLine();
    report.AppendLine("## Tests without a requirement tag");
    report.AppendLine();
    foreach (var scenario in untagged)
        report.AppendLine(CultureInfo.InvariantCulture, $"- `{ShortType(scenario.Type)}.{scenario.Method}`");
}

report.AppendLine();
report.AppendLine("## Code coverage");
report.AppendLine();
if (coverageSummary != "-" && File.Exists(coverageSummary))
    report.AppendLine(DemoteHeadings(File.ReadAllText(coverageSummary)).Trim());
else
    report.AppendLine("_Coverage was not collected._");

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, report.ToString());
Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Wrote {output}: {scenarios.Count} scenarios, {untagged.Count} untagged."));
return failed == 0 ? 0 : 2;

static IEnumerable<string> Traits(XElement test, string name) =>
    test.Descendants("trait").Where(trait => (string?)trait.Attribute("name") == name).Select(trait => (string)trait.Attribute("value")!);

static int Int(XElement element, string attribute) =>
    int.Parse((string?)element.Attribute(attribute) ?? "0", CultureInfo.InvariantCulture);

static string ShortType(string type) => type[(type.LastIndexOf('.') + 1)..];

static int IntegrationTotal(IEnumerable<XElement> assemblies) =>
    assemblies.Where(assembly => ((string?)assembly.Attribute("name") ?? string.Empty).Contains("IntegrationTests", StringComparison.Ordinal))
        .Sum(assembly => Int(assembly, "total"));

static string Humanize(string method)
{
    var parts = method.Split('_', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.EndsWith("Async", StringComparison.Ordinal) ? part[..^"Async".Length] : part)
        .Select(part => string.Join(' ', Words().Split(part)).ToLowerInvariant());
    var sentence = string.Join(" → ", parts);
    return sentence.Length == 0 ? method : char.ToUpperInvariant(sentence[0]) + sentence[1..];
}

static string DemoteHeadings(string markdown)
{
    var lines = markdown.Split('\n')
        .Where(line => !line.Contains("only available for sponsors", StringComparison.OrdinalIgnoreCase))
        .Select(line => line.StartsWith('#') ? "##" + line : line);
    return string.Join('\n', lines);
}

internal sealed record Scenario(
    string Type,
    string Method,
    int Cases,
    int Passed,
    int Failed,
    double Seconds,
    IReadOnlyList<string> Requirements,
    bool IsFailure);

internal static partial class Program
{
    [GeneratedRegex("(?<=[a-z])(?=[A-Z0-9])|(?<=[0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex Words();
}
