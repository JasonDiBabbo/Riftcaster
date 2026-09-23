using TypeGen.Core;
using TypeGen.Core.Generator;
using TypeGen.Core.SpecGeneration;

namespace Riftcaster.Codegen;

internal class Program
{
    private static void Main(string[] args)
    {
        var outputDirectory = Path.GetFullPath(args[0]);

        var options = new GeneratorOptions
        {
            BaseOutputDirectory = outputDirectory,
            CreateIndexFile = true, // index.ts re-exports everything
            CsNullableTranslation = StrictNullTypeUnionFlags.Null, // string? -> string | null
            UseImportType = true, // matches the overlay's tsconfig
            SingleQuotes = true,
            CustomTypeMappings = new Dictionary<string, string>
            {
                ["System.DateTimeOffset"] = "string" // JSON sends ISO-8601 strings for DateTimeOffset, so we map it to string in TypeScript
            },
        };

        if (Directory.Exists(outputDirectory))
        {
            Console.WriteLine($"Output directory '{outputDirectory}' already exists and will be deleted.");
            Directory.Delete(outputDirectory, true);
            Console.WriteLine($"Output directory '{outputDirectory}' was deleted.");
        }

        new Generator(options).Generate([new ContractsSpec()]);
        Console.WriteLine($"TypeScript contracts written to '{outputDirectory}'");
    }
}

sealed class ContractsSpec : GenerationSpec
{
    public ContractsSpec()
    {
        var exportedTypes = typeof(Contracts.ContractsAssembly).Assembly.GetExportedTypes();

        foreach (var type in exportedTypes)
        {
            // Static classes (compiled as abstract + sealed) can't be serialized, so they're
            // never wire types. This skips ContractsAssembly and any future static helpers.
            if (type.IsAbstract && type.IsSealed)
            {
                continue;
            }

            if (type.IsEnum)
            {
                AddEnum(type);
            }
            else
            {
                AddInterface(type);
            }
        }
    }
}