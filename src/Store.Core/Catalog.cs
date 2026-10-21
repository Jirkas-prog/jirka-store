using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Jirka.Store;

public sealed record StoreApp
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public string Version { get; init; } = "0.0.0";
    public string Kind { get; init; } = "draft";
    public string Url { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public string EntryPoint { get; init; } = "";
    public string Repository { get; init; } = "";
    public string PackageName { get; init; } = "";
    public long VersionCode { get; init; }
    public string InstallUrl { get; init; } = "";
}

public sealed record Catalog
{
    public int SchemaVersion { get; init; } = 1;
    public List<StoreApp> Apps { get; init; } = [];
}

public static class CatalogIO
{
    public const string DefaultSource = "https://raw.githubusercontent.com/Jirkas-prog/jirka-store/main/catalog/catalog.json";
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public static readonly string[] Categories = ["pc", "esp32", "homeassistant", "android", "web", "other"];
    public static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(15) };
    public const long MaxPackageBytes = 1024L * 1024 * 1024;

    public static string CategoryName(string category) => category switch
    {
        "pc" => "PC · Windows", "esp32" => "ESP32", "homeassistant" => "Home Assistant OS",
        "android" => "Android", "web" => "Web", _ => "Ostatní"
    };

    public static bool IsNewer(string offered, string installed) => ParseVersion(offered) > ParseVersion(installed);
    public static Version ParseVersion(string value)
    {
        if (!Regex.IsMatch(value ?? "", @"^\d+\.\d+\.\d+(\.\d+)?$") || !Version.TryParse(value, out var v))
            throw new InvalidDataException("Verze musí mít formát 1.2.3 nebo 1.2.3.4.");
        return new Version(v.Major, v.Minor, v.Build, Math.Max(v.Revision, 0));
    }

    public static Uri Https(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0)
            throw new InvalidDataException("Je vyžadována HTTPS adresa bez přihlašovacích údajů.");
        return uri;
    }

    public static void Validate(Catalog catalog)
    {
        if (catalog.SchemaVersion != 1 || catalog.Apps == null || catalog.Apps.Count > 5000)
            throw new InvalidDataException("Nepodporovaný nebo příliš velký katalog.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in catalog.Apps)
        {
            if (app == null || app.Id == null || !Regex.IsMatch(app.Id, "^[a-z][a-z0-9-]{0,63}$") || !ids.Add(app.Id))
                throw new InvalidDataException("Neplatné nebo duplicitní ID aplikace.");
            if (string.IsNullOrWhiteSpace(app.Name) || app.Name.Length > 120 || !Categories.Contains(app.Category) || app.Description == null)
                throw new InvalidDataException($"Neplatný název nebo kategorie: {app.Id}");
            ParseVersion(app.Version);
            if (!string.IsNullOrEmpty(app.Repository)) Https(app.Repository);
            switch (app.Kind)
            {
                case "portable-zip":
                    if (app.Category != "pc" || !app.EntryPoint.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("PC balíček musí mít EXE vstupní bod.");
                    SafeRelative(app.EntryPoint);
                    goto case "apk";
                case "apk":
                    if (string.IsNullOrWhiteSpace(app.Url) || !Regex.IsMatch(app.Sha256 ?? "", "^[a-fA-F0-9]{64}$"))
                        throw new InvalidDataException($"Chybí balíček nebo SHA-256: {app.Id}");
                    if (Uri.TryCreate(app.Url, UriKind.Absolute, out _)) Https(app.Url); else SafeRelative(app.Url);
                    if (app.Kind == "apk" && (app.Category != "android" || app.VersionCode < 1 || !Regex.IsMatch(app.PackageName ?? "", @"^[A-Za-z][\w]*(\.[A-Za-z][\w]*)+$")))
                        throw new InvalidDataException("APK musí mít packageName a kladný versionCode.");
                    break;
                case "esp-web":
                    if (app.Category != "esp32") throw new InvalidDataException("ESP instalátor patří do ESP32.");
                    Https(app.InstallUrl); break;
                case "ha-addon":
                    if (app.Category != "homeassistant") throw new InvalidDataException("HA aplikace patří do Home Assistant OS.");
                    Https(app.Repository); break;
                case "link": Https(app.InstallUrl); break;
                case "draft": break;
                default: throw new InvalidDataException($"Neznámý typ balíčku: {app.Kind}");
            }
        }
    }

    public static string SafeRelative(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains('\\'))
            throw new InvalidDataException("Neplatná relativní cesta.");
        foreach (var part in relative.Split('/'))
        {
            if (part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ') ||
                part.Any(c => c < 32 || "<>:\"|?*".Contains(c)) || Regex.IsMatch(part, @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(\.|$)", RegexOptions.IgnoreCase))
                throw new InvalidDataException("Nebezpečná cesta v balíčku.");
        }
        return relative;
    }

    public static string Inside(string root, string relative)
    {
        SafeRelative(relative);
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Cesta opouští instalační složku.");
        return full;
    }

    public static async Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken ct)
    {
        for (var hop = 0; hop < 6; hop++)
        {
            Https(uri.AbsoluteUri);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("JirkaStore/1.0");
            var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (location == null) throw new HttpRequestException("Přesměrování bez adresy.");
                uri = new Uri(uri, location); continue;
            }
            if (!response.IsSuccessStatusCode) { var code = response.StatusCode; response.Dispose(); throw new HttpRequestException($"Server vrátil HTTP {(int)code}."); }
            return response;
        }
        throw new HttpRequestException("Příliš mnoho přesměrování.");
    }

    public static async Task<Catalog> LoadAsync(string source, CancellationToken ct = default)
    {
        byte[] data;
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            using var response = await GetAsync(Https(source), ct);
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            await CopyLimited(stream, buffer, 2 * 1024 * 1024, ct);
            data = buffer.ToArray();
        }
        else
        {
            if (new FileInfo(source).Length > 2 * 1024 * 1024) throw new InvalidDataException("Katalog je příliš velký.");
            data = await File.ReadAllBytesAsync(source, ct);
        }
        var catalog = JsonSerializer.Deserialize<Catalog>(data, Json) ?? throw new InvalidDataException("Prázdný katalog.");
        Validate(catalog); return catalog;
    }

    public static async Task DownloadAsync(StoreApp app, string source, string destination, Action<string>? progress, CancellationToken ct)
    {
        string? local = null; Uri? remote = null;
        bool created = false;
        if (Uri.TryCreate(app.Url, UriKind.Absolute, out var absolute)) remote = Https(absolute.AbsoluteUri);
        else if (Uri.TryCreate(source, UriKind.Absolute, out var origin) && !origin.IsFile) remote = Https(new Uri(origin, app.Url).AbsoluteUri);
        else local = Inside(Path.GetDirectoryName(Path.GetFullPath(source))!, app.Url);
        try
        {
            using var response = remote == null ? null : await GetAsync(remote, ct);
            long total = response?.Content.Headers.ContentLength ?? (local == null ? 0 : new FileInfo(local).Length);
            if (total > MaxPackageBytes) throw new InvalidDataException("Balíček přesahuje limit 1 GiB.");
            await using (var input = response == null ? File.OpenRead(local!) : await response.Content.ReadAsStreamAsync(ct))
            await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                await CopyLimited(input, output, MaxPackageBytes, ct, bytes => progress?.Invoke($"Stahuji {bytes / 1048576.0:F1} MB" + (total > 0 ? $" / {total / 1048576.0:F1} MB" : "")));
            }
            progress?.Invoke("Ověřuji SHA-256…");
            await using var verify = File.OpenRead(destination);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(verify, ct));
            if (!actual.Equals(app.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA-256 nesouhlasí. Balíček nebyl nainstalován.");
        }
        catch { if (created && File.Exists(destination)) File.Delete(destination); throw; }
    }

    static async Task CopyLimited(Stream input, Stream output, long limit, CancellationToken ct, Action<long>? progress = null)
    {
        var buffer = new byte[81920]; long total = 0; int count;
        while ((count = await input.ReadAsync(buffer, ct)) > 0)
        {
            total += count;
            if (total > limit) throw new InvalidDataException("Soubor překročil povolenou velikost.");
            await output.WriteAsync(buffer.AsMemory(0, count), ct); progress?.Invoke(total);
        }
    }

    public static void WriteAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(value, Json)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
