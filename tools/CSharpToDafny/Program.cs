using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Wallymathieu.Auctions.Tools.CSharpToDafny;

var sources = new List<string>();
string? output = null;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--source" when i + 1 < args.Length:
            sources.Add(args[++i]);
            break;
        case "--output" when i + 1 < args.Length:
            output = args[++i];
            break;
        default:
            Console.Error.WriteLine($"Unknown argument '{args[i]}'.");
            return Usage();
    }
}

if (sources.Count == 0 || output == null) return Usage();

foreach (var source in sources.Where(source => !Directory.Exists(source)))
{
    Console.Error.WriteLine($"Source directory '{source}' does not exist.");
    return 1;
}

// Parse every C# file under the source directories into one compilation. References come from the
// running runtime's platform assemblies, which is sufficient semantic information for the pure domain
// methods the tool targets.
var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

// The projects use <ImplicitUsings>enable</ImplicitUsings>; the generated global usings live under obj/
// (which is excluded), so provide the standard SDK set here.
const string implicitUsings = """
    global using System;
    global using System.Collections.Generic;
    global using System.IO;
    global using System.Linq;
    global using System.Net.Http;
    global using System.Threading;
    global using System.Threading.Tasks;
    """;

var trees = new List<Microsoft.CodeAnalysis.SyntaxTree>
{
    CSharpSyntaxTree.ParseText(SourceText.From(implicitUsings), parseOptions, path: "ImplicitUsings.g.cs")
};
trees.AddRange(sources
    .SelectMany(source => Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
    .Where(file => !file.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
    .OrderBy(file => file, StringComparer.Ordinal)
    .Select(file => CSharpSyntaxTree.ParseText(SourceText.From(File.ReadAllText(file)), parseOptions, path: file)));

var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
    .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
    .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
    .ToList();

var compilation = CSharpCompilation.Create(
    "CSharpToDafny.Extraction",
    trees,
    references,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

var registry = new TypeRegistry();
var extractor = new VerifiedMethodExtractor(compilation, registry, Console.Error);
var methods = extractor.Extract();

if (methods.Count == 0)
{
    Console.Error.WriteLine("warning: no [Verify] methods found.");
}

var files = DafnyEmitter.Emit(methods, registry);

Directory.CreateDirectory(output);
foreach (var stale in Directory.EnumerateFiles(output, "*.dfy").Where(f => !files.ContainsKey(Path.GetFileName(f))))
{
    File.Delete(stale);
    Console.WriteLine($"Removed stale {stale}");
}

foreach (var (name, content) in files)
{
    var path = Path.Combine(output, name);
    File.WriteAllText(path, content);
    Console.WriteLine($"Wrote {path}");
}

Console.WriteLine($"Extracted {methods.Count} [Verify] method(s) into {files.Count} Dafny file(s).");
return 0;

static int Usage()
{
    Console.Error.WriteLine("Usage: CSharpToDafny --source <dir> [--source <dir> ...] --output <dir>");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Discovers [Verify]-annotated C# methods and generates Dafny skeletons with their");
    Console.Error.WriteLine("Contract.Requires/Contract.Ensures clauses translated to requires/ensures.");
    return 2;
}
