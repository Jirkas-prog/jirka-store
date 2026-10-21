using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Jirka.Store;

Console.InputEncoding = Console.OutputEncoding = new UTF8Encoding(false);
await new StoreHost(args).Run();

sealed class StoreHost
{
    readonly string root, bundled, settingsPath;
    readonly PortableInstaller installer;
    readonly object outputLock = new();
    Catalog catalog = new();
    string source, loadedSource = "", message = "Načítám katalog…", activeId = "";
    CancellationTokenSource? operation;
    Task work = Task.CompletedTask;
    bool busy;
    long lastProgress;

    public StoreHost(string[] args)
    {
        root = args.Length > 1 && args[0] == "--data-root" ? Path.GetFullPath(args[1]) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JirkaStore");
        bundled = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "catalog", "catalog.json"));
        settingsPath = Path.Combine(root, "settings.json");
        installer = new PortableInstaller(Path.Combine(root, "apps"));
        source = CatalogIO.DefaultSource;
        try { if (File.Exists(settingsPath)) source = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath), CatalogIO.Json)?.Source ?? source; }
        catch { message = "Nastavení se nepodařilo načíst."; }
    }

    void Emit(object value) { lock (outputLock) Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions(CatalogIO.Json) { WriteIndented = false })); }
    string State(StoreApp app)
    {
        if (app.Kind == "draft") return "draft";
        if (app.Kind == "portable-zip")
        {
            try { var current = installer.GetInstalled(app.Id); return current == null ? "available" : CatalogIO.IsNewer(app.Version, current.Version) ? "update" : "installed"; }
            catch { return "broken"; }
        }
        return app.Kind == "apk" ? "android" : "external";
    }
    string InstalledVersion(StoreApp app) { try { return app.Kind == "portable-zip" ? installer.GetInstalled(app.Id)?.Version ?? "" : ""; } catch { return ""; } }
    void Snapshot() => Emit(new { type = "snapshot", source, loadedSource, busy, activeId, message, apps = catalog.Apps.Select(app => new { app, state = State(app), installedVersion = InstalledVersion(app) }).ToArray() });
    void Progress(string text)
    {
        message = text;
        var now = Environment.TickCount64;
        if (now - lastProgress < 100) return;
        lastProgress = now; Emit(new { type = "progress", message, activeId });
    }

    void Start(Func<CancellationToken, Task> action, string id = "")
    {
        if (busy) return;
        busy = true; activeId = id; operation = new CancellationTokenSource(); Snapshot();
        work = Execute(action, operation);
    }
    async Task Execute(Func<CancellationToken, Task> action, CancellationTokenSource cancellation)
    {
        try { await action(cancellation.Token); }
        catch (OperationCanceledException) { message = "Operace zrušena. Předchozí instalace zůstává aktivní."; }
        catch (Exception ex) { message = ex.Message; Emit(new { type = "error", message }); }
        finally { cancellation.Dispose(); operation = null; activeId = ""; busy = false; Snapshot(); }
    }
    async Task Refresh(CancellationToken ct, bool startup = false)
    {
        try
        {
            Progress("Hledám aktualizace…");
            var next = await CatalogIO.LoadAsync(source, ct);
            catalog = next; loadedSource = source;
            CatalogIO.WriteAtomic(settingsPath, new Settings(source));
            CatalogIO.WriteAtomic(Path.Combine(root, "catalog-cache.json"), new Cached(source, next));
            message = $"Ověřeno {DateTime.Now:HH:mm} · {catalog.Apps.Count} aplikací";
        }
        catch (Exception ex) when (startup && ex is not OperationCanceledException)
        {
            try
            {
                var cached = JsonSerializer.Deserialize<Cached>(File.ReadAllText(Path.Combine(root, "catalog-cache.json")), CatalogIO.Json)!;
                CatalogIO.Validate(cached.Catalog); catalog = cached.Catalog; loadedSource = cached.Source;
            }
            catch { catalog = await CatalogIO.LoadAsync(bundled, ct); loadedSource = bundled; }
            message = "Offline · používám uložený katalog. " + ex.Message;
        }
    }
    static void Open(string address) => Process.Start(new ProcessStartInfo(CatalogIO.Https(address).AbsoluteUri) { UseShellExecute = true });
    public async Task Run()
    {
        Start(ct => Refresh(ct, true));
        while (await Console.In.ReadLineAsync() is { } line)
        {
            try
            {
                if (line.Length > 32768) throw new InvalidDataException("Příliš dlouhý příkaz.");
                var request = JsonSerializer.Deserialize<Request>(line, CatalogIO.Json) ?? throw new InvalidDataException("Prázdný příkaz.");
                if (request.Command == "cancel") { operation?.Cancel(); continue; }
                if (busy) continue;
                var app = catalog.Apps.FirstOrDefault(a => a.Id == request.Id);
                switch (request.Command)
                {
                    case "refresh": Start(ct => Refresh(ct)); break;
                    case "source":
                        source = request.Source?.Trim() ?? CatalogIO.DefaultSource;
                        Start(ct => Refresh(ct)); break;
                    case "install" when app?.Kind == "portable-zip":
                        Start(async ct => { await installer.InstallAsync(app, loadedSource, Progress, ct); message = app.Name + " · instalace dokončena"; }, app.Id); break;
                    case "launch" when app?.Kind == "portable-zip":
                        var exe = installer.Executable(app.Id) ?? throw new InvalidOperationException("Aplikace není nainstalovaná.");
                        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) }); break;
                    case "open" when app != null:
                        if (app.Kind == "ha-addon") Open("https://my.home-assistant.io/redirect/supervisor_addon_repository/?repository_url=" + Uri.EscapeDataString(app.Repository));
                        else if (app.Kind is "esp-web" or "link") Open(app.InstallUrl);
                        else throw new InvalidOperationException("Tato aplikace nemá externí instalátor.");
                        break;
                    case "repo" when app != null: Open(app.Repository); break;
                    case "folder":
                        Directory.CreateDirectory(installer.Root); Process.Start(new ProcessStartInfo(installer.Root) { UseShellExecute = true }); break;
                    default: throw new InvalidOperationException("Neznámý příkaz nebo aplikace.");
                }
            }
            catch (Exception ex) { message = ex.Message; Emit(new { type = "error", message }); Snapshot(); }
        }
        operation?.Cancel(); await work;
    }
    sealed record Settings(string Source);
    sealed record Cached(string Source, Catalog Catalog);
    sealed record Request(string Command, string? Id, string? Source);
}
