using System.Diagnostics;
using System.Text.Json;
using Jirka.Store;
using WorkFlow;

namespace Jirka.Store.Windows;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, "Local\\JirkaStore.Windows", out var owner);
        if (!owner) { MessageBox.Show("Jirka Store už běží."); return; }
        Application.ThreadException += (_, e) => MessageBox.Show(e.Exception.Message, "Jirka Store");
        Application.Run(new StoreForm(args.Contains("--screenshot")));
    }
}

sealed record Settings(string Source);

sealed class StoreForm : Form
{
    readonly string dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JirkaStore");
    readonly string bundled = Path.Combine(AppContext.BaseDirectory, "catalog", "catalog.json");
    readonly PortableInstaller installer;
    readonly TextBox source = new() { Dock = DockStyle.Fill, AccessibleName = "Cesta nebo HTTPS adresa katalogu" };
    readonly TextBox search = new() { Width = 260, PlaceholderText = "Hledat aplikaci…", AccessibleName = "Hledat aplikaci" };
    readonly ComboBox category = new() { Width = 190, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Platforma" };
    readonly ComboBox filter = new() { Width = 165, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Stav aplikací" };
    readonly SmoothGrid grid = new();
    readonly TextBox details = new() { Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.White, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Margin = new Padding(18), AccessibleName = "Podrobnosti aplikace" };
    readonly Label summary = Style.Label("Načítám knihovnu…"), status = Style.Label("Připraveno");
    readonly Button install = Style.Button("Instalovat", true), launch = Style.Button("Spustit"), repo = Style.Button("Repozitář"), refresh = Style.Button("Obnovit"), cancel = Style.Button("Zrušit");
    readonly System.Windows.Forms.Timer progressTimer = new() { Interval = 100 };
    readonly System.Windows.Forms.Timer refreshTimer = new() { Interval = 30 * 60 * 1000 };
    Catalog catalog = new();
    string loadedSource = "";
    volatile string? pendingProgress;
    CancellationTokenSource? operation;
    bool busy, closeAfter;
    StoreApp? Selected => grid.SelectedRows.Count == 0 ? null : grid.SelectedRows[0].Tag as StoreApp;

    public StoreForm(bool screenshot)
    {
        installer = new PortableInstaller(Path.Combine(dataRoot, "apps"));
        Style.Window(this, "Jirka Store · vaše aplikace na jednom místě");
        using var icon = GetType().Assembly.GetManifestResourceStream("Store.Icon")!;
        Icon = new Icon(icon);
        var layout = Style.Layout(this, 46, 34, 47, 50, 40, -1, 54, 30);
        layout.Controls.Add(Style.Label("Jirka Store", true), 0, 0);
        layout.Controls.Add(Style.Label("Vaše projekty. Jeden katalog. Aplikace pro počítač, domácnost i telefon."), 0, 1);
        var sources = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Margin = Padding.Empty };
        sources.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sources.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        sources.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        sources.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        var browse = Style.Button("Otevřít JSON");
        var online = Style.Button("Online");
        online.Enabled = !string.IsNullOrEmpty(CatalogIO.DefaultSource);
        sources.Controls.Add(source); sources.Controls.Add(browse); sources.Controls.Add(online); sources.Controls.Add(refresh);
        layout.Controls.Add(sources, 0, 2);
        category.Items.Add("Všechny platformy");
        foreach (var c in CatalogIO.Categories) category.Items.Add(CatalogIO.CategoryName(c));
        category.SelectedIndex = 0;
        filter.Items.AddRange(["Všechny aplikace", "Nainstalované", "Aktualizace"]); filter.SelectedIndex = 0;
        layout.Controls.Add(Style.Actions(category, search, filter), 0, 3);
        layout.Controls.Add(summary, 0, 4);
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65)); content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        Style.Grid(grid); grid.ReadOnly = true;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor;
        foreach (var (title, weight) in new[] { ("Aplikace", 35f), ("Platforma", 25f), ("Verze", 15f), ("Stav", 25f) })
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable });
        content.Controls.Add(grid, 0, 0);
        var detailPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20, 16, 20, 16), Margin = new Padding(14, 0, 0, 0) };
        detailPanel.Controls.Add(details); content.Controls.Add(detailPanel, 1, 0);
        layout.Controls.Add(content, 0, 5);
        var storage = Style.Button("Složka aplikací");
        var actions = Style.Actions(install, launch, repo, storage, cancel); actions.Padding = new Padding(0, 12, 0, 0);
        layout.Controls.Add(actions, 0, 6);
        status.AutoEllipsis = true; status.AutoSize = false; status.Dock = DockStyle.Fill;
        layout.Controls.Add(status, 0, 7);
        cancel.Enabled = false;
        source.Text = string.IsNullOrEmpty(CatalogIO.DefaultSource) ? bundled : CatalogIO.DefaultSource;
        try { if (File.Exists(SettingsPath)) source.Text = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath), CatalogIO.Json)?.Source ?? bundled; }
        catch { status.Text = "Nastavení se nepodařilo načíst; používám přibalený katalog."; }
        browse.Click += async (_, _) => { if (busy) return; using var dialog = new OpenFileDialog { Filter = "Katalog JSON|*.json" }; if (dialog.ShowDialog(this) == DialogResult.OK) { source.Text = dialog.FileName; await LoadCatalog(); } };
        online.Click += async (_, _) => { if (busy) return; source.Text = CatalogIO.DefaultSource; await LoadCatalog(); };
        refresh.Click += async (_, _) => await LoadCatalog();
        category.SelectedIndexChanged += (_, _) => Render(); filter.SelectedIndexChanged += (_, _) => Render(); search.TextChanged += (_, _) => Render();
        grid.SelectionChanged += (_, _) => ShowDetails();
        install.Click += async (_, _) => await InstallSelected();
        launch.Click += (_, _) => Guard(() => { if (Selected is { } app && installer.Executable(app.Id) is { } exe) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) }); });
        repo.Click += (_, _) => Guard(() => OpenLink(Selected!.Repository));
        storage.Click += (_, _) => Guard(() => { Directory.CreateDirectory(installer.Root); Process.Start(new ProcessStartInfo(installer.Root) { UseShellExecute = true }); });
        cancel.Click += (_, _) => operation?.Cancel();
        progressTimer.Tick += (_, _) => { if (pendingProgress is { } text) status.Text = text; }; progressTimer.Start();
        refreshTimer.Tick += async (_, _) => { if (!busy && loadedSource.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) await LoadCatalog(); };
        Shown += async (_, _) =>
        {
            await LoadCatalog(fallback: true); refreshTimer.Start();
            if (screenshot)
            {
                await Task.Delay(500);
                status.Text = pendingProgress;
                CatalogIO.WriteAtomic(Path.Combine(AppContext.BaseDirectory, "ui-check.json"), new { category = category.Text, filter = filter.Text, count = grid.Rows.Count, selected = Selected?.Id, installEnabled = install.Enabled });
                using var bitmap = new Bitmap(Width, Height); DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
                bitmap.Save(Path.Combine(AppContext.BaseDirectory, "preview.png")); Close();
            }
        };
        FormClosing += (_, e) => { if (busy) { closeAfter = true; operation?.Cancel(); e.Cancel = true; } };
        FormClosed += (_, _) => { progressTimer.Dispose(); refreshTimer.Dispose(); operation?.Dispose(); };
    }

    string SettingsPath => Path.Combine(dataRoot, "settings.json");
    void Guard(Action action) { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Jirka Store", MessageBoxButtons.OK, MessageBoxIcon.Error); } }
    static void OpenLink(string url) => Process.Start(new ProcessStartInfo(CatalogIO.Https(url).AbsoluteUri) { UseShellExecute = true });

    async Task LoadCatalog(bool fallback = false)
    {
        if (busy) return;
        SetBusy(true); operation = new CancellationTokenSource();
        try
        {
            pendingProgress = "Načítám katalog a hledám aktualizace…";
            var requested = source.Text.Trim();
            var next = await CatalogIO.LoadAsync(requested, operation.Token);
            catalog = next; loadedSource = requested;
            CatalogIO.WriteAtomic(SettingsPath, new Settings(requested));
            Render(); pendingProgress = $"Katalog načten · {DateTime.Now:HH:mm} · {catalog.Apps.Count} aplikací";
        }
        catch (OperationCanceledException) { pendingProgress = "Načítání zrušeno."; }
        catch (Exception ex)
        {
            if (fallback && File.Exists(bundled))
            {
                try { catalog = await CatalogIO.LoadAsync(bundled); loadedSource = bundled; source.Text = bundled; Render(); }
                catch (Exception fallbackError) { pendingProgress = "Chyba přibaleného katalogu: " + fallbackError.Message; return; }
            }
            pendingProgress = "Katalog se nepodařilo obnovit: " + ex.Message + (loadedSource.Length > 0 ? " Používám poslední načtený katalog." : "");
        }
        finally { operation.Dispose(); operation = null; SetBusy(false); }
    }

    string State(StoreApp app)
    {
        if (app.Kind == "draft") return "Připravuje se";
        if (app.Kind == "portable-zip")
        {
            try { var current = installer.GetInstalled(app.Id); return current == null ? "K instalaci" : CatalogIO.IsNewer(app.Version, current.Version) ? "Aktualizace" : "Nainstalováno"; }
            catch { return "Poškozená instalace"; }
        }
        return app.Kind == "apk" ? "Instalace v Androidu" : "Externí instalátor";
    }

    void Render()
    {
        var selected = Selected?.Id;
        grid.Rows.Clear();
        foreach (var app in catalog.Apps.Where(a => (category.SelectedIndex < 1 || a.Category == CatalogIO.Categories[category.SelectedIndex - 1]) && (a.Name + " " + a.Description).Contains(search.Text, StringComparison.CurrentCultureIgnoreCase)))
        {
            var state = State(app);
            if (filter.SelectedIndex == 1 && state is not ("Nainstalováno" or "Aktualizace")) continue;
            if (filter.SelectedIndex == 2 && state != "Aktualizace") continue;
            var i = grid.Rows.Add(app.Name, CatalogIO.CategoryName(app.Category), app.Version, state); grid.Rows[i].Tag = app;
            if (app.Id == selected) grid.CurrentCell = grid.Rows[i].Cells[0];
        }
        summary.Text = $"{grid.Rows.Count} aplikací v tomto pohledu    ·    {catalog.Apps.Count(a => State(a) == "Aktualizace")} aktualizací pro PC";
        ShowDetails();
    }

    void ShowDetails()
    {
        var app = Selected;
        if (app == null) { details.Text = "V tomto pohledu nejsou žádné aplikace."; install.Enabled = launch.Enabled = repo.Enabled = false; return; }
        var state = State(app);
        var explanation = app.Kind switch
        {
            "portable-zip" => "Instalace do vašeho profilu Windows. Před instalací se ověří SHA-256. Předchozí verze zůstává ve složce aplikace. Uživatelská data patří do AppData; data vedle EXE se mezi verzemi nekopírují.",
            "apk" => "Tuto aplikaci nainstalujete z Jirka Store v telefonu nebo tabletu. Windows APK nespouští.",
            "esp-web" => "Otevře instalační stránku pro ESP32. Připojte správnou desku přes USB. Instalaci a aktualizaci firmwaru potvrdíte v prohlížeči.",
            "ha-addon" => "Přidá repozitář do Home Assistantu. Instalaci, zálohu a další aktualizace spravuje váš Home Assistant; stav zařízení zde není zjišťován.",
            "draft" => "Projekt je evidovaný. Instalační balíček nebo instalační stránka ještě nejsou připojené.",
            _ => "Otevře stránku aplikace v prohlížeči."
        };
        details.Text = $"{app.Name}\r\n\r\n{CatalogIO.CategoryName(app.Category)}  ·  {app.Version}\r\n{state}\r\n\r\n{app.Description}\r\n\r\n{explanation}\r\n\r\n{app.Repository}";
        install.Text = app.Kind switch { "portable-zip" => state == "Aktualizace" ? "Aktualizovat" : "Instalovat", "ha-addon" => "Otevřít v HA", "esp-web" => "Nahrát firmware", _ => "Otevřít" };
        install.Enabled = !busy && (app.Kind == "portable-zip" ? state is "K instalaci" or "Aktualizace" : app.Kind is "ha-addon" or "esp-web" or "link");
        launch.Enabled = !busy && app.Kind == "portable-zip" && state is "Nainstalováno" or "Aktualizace";
        repo.Enabled = !busy && !string.IsNullOrEmpty(app.Repository);
    }

    async Task InstallSelected()
    {
        var app = Selected; if (busy || app == null) return;
        if (app.Kind != "portable-zip") { Guard(() => OpenLink(app.Kind == "ha-addon" ? "https://my.home-assistant.io/redirect/supervisor_addon_repository/?repository_url=" + Uri.EscapeDataString(app.Repository) : app.InstallUrl)); return; }
        if (MessageBox.Show(this, $"Nainstalovat {app.Name} {app.Version}?\n\nZdroj katalogu: {loadedSource}\n\nSpouštějte pouze aplikace z důvěryhodného katalogu.", "Instalace aplikace", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        SetBusy(true); operation = new CancellationTokenSource();
        try { await installer.InstallAsync(app, loadedSource, message => pendingProgress = message, operation.Token); Render(); }
        catch (OperationCanceledException) { pendingProgress = "Instalace zrušena. Předchozí verze zůstává aktivní."; }
        catch (Exception ex) { pendingProgress = "Instalace selhala: " + ex.Message; MessageBox.Show(this, ex.Message, "Instalace selhala", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { operation.Dispose(); operation = null; SetBusy(false); }
    }

    void SetBusy(bool value)
    {
        busy = value; refresh.Enabled = !value; cancel.Enabled = value; source.Enabled = !value; ShowDetails();
        if (!value && closeAfter) Close();
    }
}
