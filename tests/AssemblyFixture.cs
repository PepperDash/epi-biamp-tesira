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

        // Fail clearly if the plugin hasn't been built yet, rather than letting
        // Directory.GetFiles throw a less actionable DirectoryNotFoundException below.
        if (!File.Exists(PluginDllPath))
            throw new FileNotFoundException(
                $"Plugin DLL not found at '{PluginDllPath}'. Build the plugin first.", PluginDllPath);

        foreach (var dll in Directory.GetFiles(PluginOutputDir, "*.dll"))
            dllByName[Path.GetFileName(dll)] = dll;

        foreach (var dll in Directory.GetFiles(runtimeDir, "*.dll"))
            dllByName.TryAdd(Path.GetFileName(dll), dll);

        // NOT deps.json - the plugin csproj uses <ExcludeAssets>runtime</ExcludeAssets> on the
        // PepperDashEssentials reference (Essentials provides those DLLs on the processor at
        // runtime), so the plugin's own deps.json omits dependencies (e.g. Newtonsoft.Json)
        // entirely and this resolver would fail with FileNotFoundException. project.assets.json
        // (in src/obj/) has the full pre-exclusion dependency graph and is present after any
        // restore/build.
        var assetsJsonPath = Path.Combine(SourceDirectory, "obj", "project.assets.json");
        if (File.Exists(assetsJsonPath))
        {
            foreach (var path in ResolveProjectAssetsAssemblies(assetsJsonPath))
                dllByName.TryAdd(Path.GetFileName(path), path);
        }

        return new MetadataLoadContext(new PathAssemblyResolver(dllByName.Values));
    }

    private static IEnumerable<string> ResolveProjectAssetsAssemblies(string assetsJsonPath)
    {
        // Honor NUGET_PACKAGES (common in CI / enterprise setups); fall back to the default.
        var nugetDir = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (string.IsNullOrWhiteSpace(nugetDir))
            nugetDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget", "packages");

        using var stream = File.OpenRead(assetsJsonPath);
        using var doc = JsonDocument.Parse(stream);

        if (!doc.RootElement.TryGetProperty("targets", out var targets))
            yield break;

        // Exactly one TFM per build - take whichever key is present rather than hard-coding one.
        var target = targets.EnumerateObject().FirstOrDefault();
        if (target.Value.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var lib in target.Value.EnumerateObject())
        {
            // Library key is "PackageId/Version"
            var slash = lib.Name.LastIndexOf('/');
            if (slash < 0) continue;
            var packageId = lib.Name[..slash].ToLowerInvariant();
            var version = lib.Name[(slash + 1)..];

            if (!lib.Value.TryGetProperty("compile", out var assets) &&
                !lib.Value.TryGetProperty("runtime", out assets))
                continue;

            foreach (var asset in assets.EnumerateObject())
            {
                // "_._" is NuGet's placeholder for "no asset of this kind" - skip it.
                if (!asset.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    continue;

                var dllPath = Path.Combine(nugetDir, packageId, version,
                    asset.Name.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(dllPath))
                    yield return dllPath;
            }
        }
    }

    private static Assembly LoadPluginAssembly()
    {
        if (!File.Exists(PluginDllPath))
            throw new FileNotFoundException(
                $"Plugin DLL not found at '{PluginDllPath}'. Build the plugin first.", PluginDllPath);
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

    // Read every source file once, then search in memory - FindSourceForClass is called by many tests.
    private static readonly Lazy<string[]> AllSourceContents = new(() =>
        Directory.GetFiles(SourceDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText)
            .ToArray());

    public static string? FindSourceForClass(string className) =>
        AllSourceContents.Value.FirstOrDefault(content => DeclaresClass(content, className));

    // Match "class <name>" only when <name> ends on a non-identifier boundary, so a prefix
    // like "Foo" does not match "class FooBar". Avoids a regex (keeps the helper allocation-light).
    private static bool DeclaresClass(string content, string className)
    {
        var needle = "class " + className;
        for (var i = content.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = content.IndexOf(needle, i + 1, StringComparison.Ordinal))
        {
            var after = i + needle.Length;
            var next = after < content.Length ? content[after] : ' ';
            if (!char.IsLetterOrDigit(next) && next != '_')
                return true;
        }
        return false;
    }
}
