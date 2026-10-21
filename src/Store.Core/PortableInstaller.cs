using System.IO.Compression;
using System.Text.Json;

namespace Jirka.Store;

public sealed record InstalledApp(string Version, string Directory, string EntryPoint);

public sealed class PortableInstaller(string root)
{
    public string Root { get; } = Path.GetFullPath(root);
    string AppRoot(string id) => CatalogIO.Inside(Root, CatalogIO.SafeRelative(id));

    public InstalledApp? GetInstalled(string id)
    {
        var path = Path.Combine(AppRoot(id), "installed.json");
        if (!File.Exists(path)) return null;
        var state = JsonSerializer.Deserialize<InstalledApp>(File.ReadAllText(path), CatalogIO.Json) ?? throw new InvalidDataException("Poškozený stav instalace.");
        CatalogIO.ParseVersion(state.Version);
        var executable = CatalogIO.Inside(CatalogIO.Inside(AppRoot(id), state.Directory), state.EntryPoint);
        if (!File.Exists(executable)) throw new InvalidDataException($"Chybí nainstalovaný soubor: {id}. Obnovte instalaci ze zálohy.");
        return state;
    }

    public string? Executable(string id)
    {
        var state = GetInstalled(id);
        return state == null ? null : CatalogIO.Inside(CatalogIO.Inside(AppRoot(id), state.Directory), state.EntryPoint);
    }

    public async Task InstallAsync(StoreApp app, string source, Action<string>? progress, CancellationToken ct = default)
    {
        CatalogIO.Validate(new Catalog { Apps = [app] });
        if (app.Kind != "portable-zip") throw new InvalidOperationException("Tento balíček není Windows ZIP.");
        var appRoot = AppRoot(app.Id);
        Directory.CreateDirectory(appRoot);
        // Per-app process lock also protects callers outside the desktop UI.
        using var guard = new FileStream(Path.Combine(appRoot, ".install-lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var installed = GetInstalled(app.Id);
        if (installed != null && !CatalogIO.IsNewer(app.Version, installed.Version)) throw new InvalidOperationException("Stejná nebo novější verze je již nainstalovaná.");
        var folder = "versions/" + app.Version + "-" + Guid.NewGuid().ToString("N");
        var staging = CatalogIO.Inside(appRoot, folder);
        var archive = Path.Combine(appRoot, Guid.NewGuid().ToString("N") + ".zip");
        bool committed = false;
        try
        {
            await CatalogIO.DownloadAsync(app, source, archive, progress, ct);
            progress?.Invoke("Rozbaluji a kontroluji balíček…");
            await Task.Run(() => Extract(archive, staging, ct), ct);
            if (!File.Exists(CatalogIO.Inside(staging, app.EntryPoint))) throw new InvalidDataException("Balíček neobsahuje deklarované EXE.");
            ct.ThrowIfCancellationRequested();
            // The old version and its user files remain intact if download/extraction fails.
            CatalogIO.WriteAtomic(Path.Combine(appRoot, "installed.json"), new InstalledApp(app.Version, folder, app.EntryPoint));
            committed = true;
            progress?.Invoke("Instalace dokončena.");
        }
        finally
        {
            if (File.Exists(archive)) File.Delete(archive);
            if (!committed && Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }

    static void Extract(string archive, string root, CancellationToken ct)
    {
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count > 20000) throw new InvalidDataException("Příliš mnoho souborů v balíčku.");
        long expanded = 0;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();
            var name = item.FullName.TrimEnd('/');
            var target = CatalogIO.Inside(root, name);
            if (!names.Add(name) || ((item.ExternalAttributes >> 16) & 0xF000) == 0xA000 || (item.ExternalAttributes & 0x400) != 0)
                throw new InvalidDataException("Duplicitní cesta nebo odkaz v ZIP.");
            expanded = checked(expanded + item.Length);
            if (expanded > 2L * 1024 * 1024 * 1024) throw new InvalidDataException("Rozbalený balíček přesahuje 2 GiB.");
            if (item.FullName.EndsWith('/')) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            item.ExtractToFile(target, false);
        }
    }
}
