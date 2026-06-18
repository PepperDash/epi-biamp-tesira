using System.Reflection;
using System.Text.Json;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public static class AssemblyFixture
{
    private static readonly Lazy<MetadataLoadContext> LazyContext = new(CreateContext);
    private static readonly Lazy<Assembly> LazyAssembly = new(LoadPluginAssembly);

    private static string Configuration
    {
        get
        {
            var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            var parts = baseDir.Split(Path.DirectorySeparatorChar);
            return parts[^2]; // net8.0 is last, Configuration is second-to-last
        }
    }

    // Nested layout: src/4Series/bin/{Config}/net8/ (OutputPath = 4Series\bin\$(Configuration)\).
    private static string PluginDllPath =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "src", "4Series", "bin", Configuration, "net8",
            "Tesira-DSP-EPI.4Series.dll"));

    private static string PluginOutputDir => Path.GetDirectoryName(PluginDllPath)!;

    public static MetadataLoadContext Context => LazyContext.Value;
    public static Assembly PluginAssembly => LazyAssembly.Value;

    private static MetadataLoadContext CreateContext()
    {
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var dllByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dll in Directory.GetFiles(PluginOutputDir, "*.dll"))
            dllByName[Path.GetFileName(dll)] = dll;

        foreach (var dll in Directory.GetFiles(runtimeDir, "*.dll"))
            dllByName.TryAdd(Path.GetFileName(dll), dll);

        var depsJsonPath = Path.ChangeExtension(PluginDllPath, ".deps.json");
        if (File.Exists(depsJsonPath))
        {
            foreach (var path in ResolveDepsJsonAssemblies(depsJsonPath))
                dllByName.TryAdd(Path.GetFileName(path), path);
        }

        return new MetadataLoadContext(new PathAssemblyResolver(dllByName.Values));
    }

    private static IEnumerable<string> ResolveDepsJsonAssemblies(string depsJsonPath)
    {
        var nugetDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages");

        using var stream = File.OpenRead(depsJsonPath);
        using var doc = JsonDocument.Parse(stream);

        if (!doc.RootElement.TryGetProperty("libraries", out var libraries))
            yield break;

        foreach (var lib in libraries.EnumerateObject())
        {
            if (!lib.Value.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "package")
                continue;
            if (!lib.Value.TryGetProperty("path", out var pathProp))
                continue;

            var packagePath = Path.Combine(nugetDir, pathProp.GetString()!);
            if (!Directory.Exists(packagePath)) continue;

            var libDir = Path.Combine(packagePath, "lib", "net8.0");
            if (!Directory.Exists(libDir))
                libDir = Path.Combine(packagePath, "lib", "netstandard2.0");
            if (!Directory.Exists(libDir)) continue;

            foreach (var dll in Directory.GetFiles(libDir, "*.dll"))
                yield return dll;
        }
    }

    private static Assembly LoadPluginAssembly()
    {
        if (!File.Exists(PluginDllPath))
            throw new FileNotFoundException(
                $"Plugin DLL not found at '{PluginDllPath}'. Build the plugin first.");
        return Context.LoadFromAssemblyPath(PluginDllPath);
    }

    public static List<Type> FindFactoryTypes(string baseTypePrefix = "EssentialsPluginDeviceFactory")
    {
        return PluginAssembly.GetTypes()
            .Where(t => !t.IsAbstract
                && t.BaseType is { IsGenericType: true }
                && t.BaseType.GetGenericTypeDefinition().Name.StartsWith(baseTypePrefix))
            .ToList();
    }

    public static string SourceDirectory =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "src"));

    public static string? FindSourceForClass(string className)
    {
        foreach (var file in Directory.GetFiles(SourceDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            if (content.Contains($"class {className}"))
                return content;
        }
        return null;
    }
}
