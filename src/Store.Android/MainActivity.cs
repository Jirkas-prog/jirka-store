using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using Jirka.Store;
using Uri = Android.Net.Uri;
using Path = System.IO.Path;
using OperationCanceledException = System.OperationCanceledException;

namespace Jirka.Store.AndroidApp;

[Activity(Label = "Jirka Store", MainLauncher = true, Exported = true, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : Activity
{
    static readonly Color Ink = Color.Rgb(28, 43, 57), Green = Color.Rgb(22, 110, 98);
    LinearLayout cards = null!;
    EditText source = null!, search = null!;
    TextView status = null!;
    Spinner filter = null!;
    Button reload = null!, cancel = null!;
    Catalog catalog = new();
    string loadedSource = "";
    CancellationTokenSource? operation;
    bool busy, destroyed;
    string? pendingProgress;
    Handler timer = null!;
    Action tick = null!;

    protected override void OnCreate(Bundle? state)
    {
        base.OnCreate(state);
        Window!.SetStatusBarColor(Green);
        var page = new LinearLayout(this) { Orientation = Orientation.Vertical };
        page.SetPadding(Dp(20), Dp(22), Dp(20), Dp(8)); page.SetBackgroundColor(Color.Rgb(245, 248, 250));
        page.AddView(Text("Jirka Store", 30, true)); page.AddView(Text("Vaše Android aplikace na jednom místě", 14));
        source = new EditText(this) { TextSize = 12, Hint = "HTTPS adresa katalogu", ContentDescription = "Adresa katalogu" };
        source.SetSingleLine(true);
        source.Text = GetPreferences(FileCreationMode.Private)!.GetString("catalog", CatalogIO.DefaultSource);
        page.AddView(source);
        reload = Button("Obnovit katalog", async () => await Refresh()); page.AddView(reload);
        search = new EditText(this) { Hint = "Hledat aplikaci…", ContentDescription = "Hledat aplikaci" }; search.SetSingleLine(true); page.AddView(search);
        filter = new Spinner(this) { ContentDescription = "Filtr aplikací" };
        filter.Adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerDropDownItem, ["Všechny aplikace", "Nainstalované", "Aktualizace"]);
        page.AddView(filter);
        status = Text("Načítám…", 12); page.AddView(status);
        cancel = Button("Zrušit stahování", () => { operation?.Cancel(); return Task.CompletedTask; }); cancel.Visibility = ViewStates.Gone; page.AddView(cancel);
        cards = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var scroll = new ScrollView(this); scroll.AddView(cards); page.AddView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));
        SetContentView(page);
        search.TextChanged += (_, _) => Render(); filter.ItemSelected += (_, _) => Render();
        timer = new Handler(Looper.MainLooper!);
        tick = () => { if (destroyed) return; if (pendingProgress != null) status.Text = pendingProgress; timer.PostDelayed(tick!, 100); };
        timer.Post(tick);
        _ = Refresh(true);
    }

    protected override void OnResume() { base.OnResume(); if (cards != null) Render(); }
    protected override void OnDestroy() { destroyed = true; operation?.Cancel(); timer?.RemoveCallbacksAndMessages(null); base.OnDestroy(); }
    int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density);
    TextView Text(string text, int size, bool bold = false)
    {
        var view = new TextView(this) { Text = text, TextSize = size };
        view.SetTextColor(Ink); view.SetPadding(0, Dp(4), 0, Dp(6));
        if (bold) view.SetTypeface(null, TypefaceStyle.Bold);
        return view;
    }
    Button Button(string text, Func<Task> action)
    {
        var button = new Button(this) { Text = text, ContentDescription = text };
        button.SetTextColor(Green); button.SetAllCaps(false);
        button.Click += async (_, _) => { try { await action(); } catch (Exception ex) { if (!destroyed) { pendingProgress = ex.Message; new AlertDialog.Builder(this)!.SetTitle("Jirka Store")!.SetMessage(ex.Message)!.SetPositiveButton("OK", (_, _) => { })!.Show(); } } };
        return button;
    }

    async Task Refresh(bool startup = false)
    {
        if (busy) return; SetBusy(true); operation = new CancellationTokenSource();
        try
        {
            pendingProgress = "Hledám aktualizace…";
            var requested = source.Text!.Trim();
            if (requested.Length == 0)
            {
                await LoadSavedCatalog();
                pendingProgress = "Uložený katalog · pro nové verze nastavte HTTPS adresu katalogu.";
                return;
            }
            CatalogIO.Https(requested);
            var next = await CatalogIO.LoadAsync(requested, operation.Token);
            catalog = next; loadedSource = requested;
            GetPreferences(FileCreationMode.Private)!.Edit()!.PutString("catalog", requested)!.Apply();
            CatalogIO.WriteAtomic(Path.Combine(FilesDir!.AbsolutePath, "catalog-cache.json"), next);
            GetPreferences(FileCreationMode.Private)!.Edit()!.PutString("cacheSource", requested)!.Apply();
            pendingProgress = "Katalog obnoven · " + DateTime.Now.ToString("HH:mm");
        }
        catch (OperationCanceledException) { pendingProgress = "Načítání zrušeno."; }
        catch (Exception ex)
        {
            if (startup)
            {
                try
                {
                    await LoadSavedCatalog();
                }
                catch { catalog = new(); }
            }
            pendingProgress = "Offline / katalog nedostupný. Zobrazuji uložený katalog. " + ex.Message;
        }
        finally { operation.Dispose(); operation = null; if (!destroyed) { SetBusy(false); Render(); } }
    }

    async Task LoadSavedCatalog()
    {
        var cached = Path.Combine(FilesDir!.AbsolutePath, "catalog-cache.json");
        if (File.Exists(cached))
        {
            catalog = await CatalogIO.LoadAsync(cached);
            loadedSource = GetPreferences(FileCreationMode.Private)!.GetString("cacheSource", CatalogIO.DefaultSource)!;
        }
        else
        {
            using var asset = Assets!.Open("catalog.json"); using var reader = new StreamReader(asset);
            catalog = System.Text.Json.JsonSerializer.Deserialize<Catalog>(await reader.ReadToEndAsync(), CatalogIO.Json)!;
            CatalogIO.Validate(catalog); loadedSource = CatalogIO.DefaultSource;
        }
    }

    long InstalledVersion(StoreApp app)
    {
        try
        {
            var info = PackageManager!.GetPackageInfo(app.PackageName, PackageInfoFlags.MetaData)!;
            return info.LongVersionCode;
        }
        catch (PackageManager.NameNotFoundException) { return 0; }
    }

    void Render()
    {
        if (destroyed || cards == null) return;
        cards.RemoveAllViews(); int count = 0;
        foreach (var app in catalog.Apps.Where(a => a.Category == "android" && (a.Name + " " + a.Description).Contains(search.Text ?? "", StringComparison.CurrentCultureIgnoreCase)))
        {
            var installed = app.Kind == "apk" ? InstalledVersion(app) : 0;
            if (filter.SelectedItemPosition == 1 && installed == 0 || filter.SelectedItemPosition == 2 && (installed == 0 || installed >= app.VersionCode)) continue;
            count++;
            var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
            card.SetBackgroundColor(Color.White); card.SetPadding(Dp(16), Dp(14), Dp(16), Dp(14));
            cards.AddView(card, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dp(12) });
            card.AddView(Text(app.Name, 21, true));
            card.AddView(Text("Verze " + app.Version + (installed > 0 ? " · " + (installed < app.VersionCode ? "Dostupná aktualizace" : "Nainstalováno") : ""), 12));
            card.AddView(Text(app.Description, 14));
            if (app.Kind == "apk" && installed < app.VersionCode)
            {
                var action = Button(installed == 0 ? "Instalovat" : "Aktualizovat", () => Install(app)); action.Enabled = !busy; card.AddView(action);
            }
            if (installed > 0 && app.PackageName != PackageName)
                card.AddView(Button("Spustit", () => { var intent = PackageManager!.GetLaunchIntentForPackage(app.PackageName); if (intent == null) throw new InvalidOperationException("Aplikace nemá spouštěcí aktivitu."); StartActivity(intent); return Task.CompletedTask; }));
            if (!string.IsNullOrEmpty(app.Repository)) card.AddView(Button("Projekt na Gitu", () => { OpenLink(app.Repository); return Task.CompletedTask; }));
            if (app.Kind == "draft") card.AddView(Text("Instalační balíček se připravuje.", 12));
        }
        if (count == 0) cards.AddView(Text("V tomto pohledu nejsou žádné aplikace.", 16));
    }

    void OpenLink(string address) => StartActivity(new Intent(Intent.ActionView, Uri.Parse(CatalogIO.Https(address).AbsoluteUri)));
    void SetBusy(bool value) { busy = value; reload.Enabled = !value; source.Enabled = !value; cancel.Visibility = value ? ViewStates.Visible : ViewStates.Gone; Render(); }

    async Task Install(StoreApp app)
    {
        if (busy) return;
        if (!PackageManager!.CanRequestPackageInstalls())
        {
            pendingProgress = "Povolte instalace z Jirka Store, vraťte se a znovu klepněte na Instalovat.";
            StartActivity(new Intent(global::Android.Provider.Settings.ActionManageUnknownAppSources, Uri.Parse("package:" + PackageName)));
            return;
        }
        SetBusy(true); operation = new CancellationTokenSource();
        var apk = Path.Combine(CacheDir!.AbsolutePath, Guid.NewGuid().ToString("N") + ".apk");
        bool handedToAndroid = false;
        try
        {
            await CatalogIO.DownloadAsync(app, loadedSource, apk, text => pendingProgress = text, operation.Token);
            var info = PackageManager.GetPackageArchiveInfo(apk, PackageInfoFlags.MetaData) ?? throw new InvalidDataException("Soubor není platné APK.");
            var versionCode = info.LongVersionCode;
            if (info.PackageName != app.PackageName || versionCode != app.VersionCode) throw new InvalidDataException("Identita nebo verze APK nesouhlasí s katalogem.");
            if (InstalledVersion(app) >= app.VersionCode) throw new InvalidOperationException("Stejná nebo novější verze je již nainstalovaná.");
            if (destroyed) return;
            var uri = Uri.Parse("content://cz.jirkas.store.files/" + Path.GetFileName(apk))!;
            var intent = new Intent(Intent.ActionView);
            intent.SetDataAndType(uri, "application/vnd.android.package-archive");
            intent.AddFlags(ActivityFlags.GrantReadUriPermission);
            intent.ClipData = ClipData.NewRawUri("APK", uri);
            StartActivity(intent); handedToAndroid = true;
            pendingProgress = "Balíček ověřen. Dokončete instalaci v systémovém dialogu Androidu.";
        }
        catch (OperationCanceledException) { pendingProgress = "Stahování zrušeno."; }
        finally
        {
            if (!handedToAndroid && File.Exists(apk)) File.Delete(apk);
            operation.Dispose(); operation = null; if (!destroyed) SetBusy(false);
        }
    }
}
