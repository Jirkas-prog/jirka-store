using System.IO.Compression;
using System.Security.Cryptography;
using Jirka.Store;

var root = Path.Combine(Path.GetTempPath(), "JirkaStore-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
async Task Reject(Func<Task> action, string message)
{
    bool rejected = false;
    try { await action(); } catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or OperationCanceledException) { rejected = true; }
    Check(rejected, message);
}
StoreApp Package(string version, string file = "app.exe", string content = "executable fixture")
{
    var name = Guid.NewGuid().ToString("N") + ".zip";
    using (var zip = ZipFile.Open(Path.Combine(root, name), ZipArchiveMode.Create))
    using (var writer = new StreamWriter(zip.CreateEntry(file).Open())) writer.Write(content);
    return new StoreApp { Id = "sample", Name = "Test", Category = "pc", Kind = "portable-zip", Version = version, Url = name, EntryPoint = "app.exe", Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, name)))) };
}
try
{
    var source = Path.Combine(root, "catalog.json");
    var installer = new PortableInstaller(Path.Combine(root, "apps"));
    var first = Package("1.0.0");
    CatalogIO.WriteAtomic(source, new Catalog { Apps = [first] });
    var parsed = await CatalogIO.LoadAsync(source);
    Check(parsed.Apps.Single().Id == "sample", "catalog roundtrip");
    await installer.InstallAsync(first, source, null);
    var firstExe = installer.Executable("sample")!;
    Check(File.ReadAllText(firstExe) == "executable fixture", "fresh install points to verified package");
    var second = Package("1.1.0", content: "new version");
    await installer.InstallAsync(second, source, null);
    Check(installer.GetInstalled("sample")!.Version == "1.1.0" && File.ReadAllText(installer.Executable("sample")!) == "new version", "update activates new version");
    Check(File.Exists(firstExe), "update retains previous version and its files");
    await Reject(() => installer.InstallAsync(first, source, null), "downgrade refused");
    await Reject(() => installer.InstallAsync(second, source, null), "same version refused");
    var corrupt = Package("1.2.0") with { Sha256 = new string('0', 64) };
    await Reject(() => installer.InstallAsync(corrupt, source, null), "checksum mismatch refused");
    Check(installer.GetInstalled("sample")!.Version == "1.1.0", "failed update preserves active version");
    var traversal = Package("1.2.0", "../../escape.exe");
    await Reject(() => installer.InstallAsync(traversal, source, null), "ZIP traversal refused");
    Check(!File.Exists(Path.Combine(root, "escape.exe")), "ZIP cannot escape installation root");
    await Reject(() => installer.InstallAsync(Package("1.2.0", "wrong.exe"), source, null), "missing declared executable refused");
    var cts = new CancellationTokenSource(); cts.Cancel();
    await Reject(() => installer.InstallAsync(Package("1.2.0"), source, null, cts.Token), "cancellation leaves previous install intact");
    Check(installer.GetInstalled("sample")!.Version == "1.1.0", "active version intact after cancellation");
    foreach (var bad in new[] { "../escape", "C:/Windows/test", "x:stream", "AUX.txt", "folder\\file", "a/../b", "a/b.", "/root" })
        await Reject(() => { CatalogIO.SafeRelative(bad); return Task.CompletedTask; }, "unsafe path rejected: " + bad);
    await Reject(() => { CatalogIO.Validate(new Catalog { Apps = [first, first] }); return Task.CompletedTask; }, "duplicate IDs refused");
    await Reject(() => { CatalogIO.Validate(new Catalog { Apps = [first with { Url = "http://example.org/app.zip" }] }); return Task.CompletedTask; }, "HTTP package refused");
    await Reject(() => { CatalogIO.Validate(new Catalog { Apps = [first with { Kind = "script" }] }); return Task.CompletedTask; }, "arbitrary script install kind refused");
    Check(CatalogIO.IsNewer("1.10.0", "1.9.0") && !CatalogIO.IsNewer("1.0.0", "1.0.0.0"), "numeric version comparison");
    var occupied = Path.Combine(root, "occupied.zip"); File.WriteAllText(occupied, "keep me");
    try { await CatalogIO.DownloadAsync(first, source, occupied, null, default); } catch (IOException) { }
    Check(File.ReadAllText(occupied) == "keep me", "download never removes an existing destination");
    foreach (var path in args.Where(a => !a.StartsWith("--")))
    {
        var catalog = await CatalogIO.LoadAsync(path);
        Check(catalog.Apps.Count > 0, "real catalog valid: " + path);
        if (args.Contains("--local"))
        {
            var app = catalog.Apps.First(a => a.Id == "wifi-scope");
            await installer.InstallAsync(app, path, null);
            Check(File.Exists(installer.Executable(app.Id)), "real WiFi Scope package installs from local catalog");
        }
        if (args.Contains("--online"))
        {
            var app = catalog.Apps.First(a => a.Id == "zephyr-termux");
            var destination = Path.Combine(root, "real.apk");
            await CatalogIO.DownloadAsync(app, path, destination, null, default);
            Check(new FileInfo(destination).Length == 37330, "real GitHub APK download and SHA-256");
        }
    }
    Console.WriteLine($"{checks} checks passed.");
}
finally
{
    var full = Path.GetFullPath(root);
    if (!full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("JirkaStore-check-")) throw new Exception("Unsafe cleanup target");
    Directory.Delete(full, true);
}
