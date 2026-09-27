using System.Reflection;
using System.Text.Json.Serialization;
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
    private readonly List<Type> _polymorphicBases = [];

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
            else if (Attribute.IsDefined(type, typeof(JsonPolymorphicAttribute)))
            {
                _polymorphicBases.Add(type);
            }
            else
            {
                var builder = AddInterface(type);

                // Derived from a polymorphic base: drop "extends Base" (the base becomes a union type,
                // which an interface can't extend) and add the discriminator as a literal-typed property.
                var polymorphic = type.BaseType?.GetCustomAttribute<JsonPolymorphicAttribute>();
                if (polymorphic is not null)
                {
                    var propertyName = polymorphic.TypeDiscriminatorPropertyName ?? "$type";
                    var discriminator = GetDiscriminator(type);
                    builder.IgnoreBase().CustomBody($"\n    {propertyName}: '{discriminator}';");
                }
            }
        }
    }

    /// <summary>
    /// Writes each polymorphic base as a TypeScript union of its derived types. Runs after TypeGen has
    /// written its files, so it replaces the empty interface TypeGen generates for the base (as a
    /// dependency of types that reference it); index.ts and importers already use that file name.
    /// Supports one level of inheritance: base → derived.
    /// </summary>
    public override void OnAfterGeneration(OnAfterGenerationArgs args)
    {
        var options = args.GeneratorOptions;
        string FileName(Type type) => options.FileNameConverters.Convert(type.Name, type);

        foreach (var polymorphicBase in _polymorphicBases)
        {
            var derivedTypes = polymorphicBase.GetCustomAttributes<JsonDerivedTypeAttribute>()
                .Select(registration => registration.DerivedType)
                .ToList();

            if (derivedTypes.Count == 0)
            {
                throw new InvalidOperationException($"{polymorphicBase.Name} has [JsonPolymorphic] but no [JsonDerivedType] attributes.");
            }

            var lines = new List<string>
            {
                "/**",
                " * This is a Riftcaster.Codegen auto-generated file.",
                " * Any changes made to this file can be lost when this file is regenerated.",
                " */",
                "",
            };

            foreach (var derivedType in derivedTypes)
            {
                lines.Add($"import type {{ {derivedType.Name} }} from './{FileName(derivedType)}';");
            }

            lines.Add("");
            lines.Add($"export type {polymorphicBase.Name} =");

            foreach (var derivedType in derivedTypes)
            {
                lines.Add($"    | {derivedType.Name}");
            }

            lines[^1] += ";";

            var path = Path.Combine(options.BaseOutputDirectory, FileName(polymorphicBase) + ".ts");
            File.WriteAllText(path, string.Join("\n", lines) + "\n");
        }
    }

    /// <summary>
    /// Finds the tag for a derived type in its base's [JsonDerivedType] attributes, e.g. "keyword".
    /// </summary>
    /// <param name="derivedType">The derived type</param>
    /// <returns>The discriminator string of a derived type</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the derived type does not include a [JsonDerivedType] attribute or the included discriminator is not a string.
    /// </exception>
    private static string GetDiscriminator(Type derivedType)
    {
        var polymorphicBase = derivedType.BaseType!;

        foreach (var registration in polymorphicBase.GetCustomAttributes<JsonDerivedTypeAttribute>())
        {
            if (registration.DerivedType == derivedType)
            {
                return registration.TypeDiscriminator as string
                    ?? throw new InvalidOperationException(
                        $"{polymorphicBase.Name}: the discriminator for {derivedType.Name} must be a string.");
            }
        }

        throw new InvalidOperationException(
            $"{derivedType.Name} derives from {polymorphicBase.Name} but has no [JsonDerivedType] on it.");
    }
}
