# Jirka Store

Společný katalog vlastních projektů a dva nativní launchery: **Windows 10/11 x64** a **Android 9+**. Samostatný Git repozitář pro obchod; jednotlivé aplikace mají vlastní repozitáře a vydání. [Stáhnout launchery](https://github.com/Jirkas-prog/jirka-store/releases/tag/v1.0.0).

## Spuštění

- Windows: z Releases stáhněte a rozbalte `JirkaStore-Windows-1.0.0.zip`, otevřete `JirkaStore.exe`. Lokální sestavení je v `dist/windows/JirkaStore.exe`. Aplikace obsahuje .NET runtime; celou složku držte pohromadě s katalogem.
- Android: z Releases stáhněte `JirkaStore.apk` (lokálně `dist/android/JirkaStore.apk`) do telefonu a otevřete jej. Povolte instalace z tohoto zdroje, pokud si to Android vyžádá. Při instalaci aplikací ze Storu se stejné oprávnění uděluje Jirka Store.
- Windows má filtry **PC, ESP32, Home Assistant OS, Android, Web, Ostatní**, hledání a pohledy nainstalovaných aplikací a aktualizací. Android zobrazuje Android aplikace, jejich skutečnou instalovanou verzi a tlačítko Spustit.

Přibalený katalog funguje i bez zveřejnění nového repozitáře. Android balíčky Zephyr a FreeCAD jsou navázané na již existující vydání. Lokální Windows sestavení obsahuje instalační ZIP WiFi Scope, pokud se sestavilo s `-UseLocalCatalog`. U dalších vlastních ESP32 projektů je pravdivě uvedeno **Připravuje se**, dokud nejsou vydané konkrétní firmwary.

## Instalace a aktualizace

| Platforma | Postup |
| --- | --- |
| PC | ZIP s relativní cestou k EXE. Stažení / kopie → SHA-256 → kontrolované rozbalení → atomické přepnutí aktivní verze. Není potřeba správce. |
| Android | APK → SHA-256 → ověření packageName a versionCode → systémový instalátor. Android ověří podpis a uživatel potvrdí instalaci. Stav se načítá z PackageManager, nikoli z předpokládaného úspěchu. |
| ESP32 | Otevření projektové HTTPS stránky s ESP Web Tools. Připojení USB, volbu desky a zápis firmwaru provede uživatel v prohlížeči. |
| Home Assistant OS | Otevření odkazu pro přidání repozitáře v HA. Konkrétní doplněk, instalaci, zálohu a aktualizace spravuje Home Assistant. Store nesbírá jeho tokeny ani nepředstírá stav vzdáleného zařízení. |

Windows aktualizuje katalog při otevření, ručně a každých 30 minut během běhu. Android při otevření a tlačítkem Obnovit katalog; po návratu ze systémového instalátoru znovu zjistí instalované verze. Stažení nebo instalaci spouští uživatel. Stejná a starší verze PC balíčku se odmítá. Aktualizace APK vyžadují stejný podpisový klíč a vyšší `versionCode`.

PC instalace jsou v `%LOCALAPPDATA%/JirkaStore/apps/<id>/versions/`. Předchozí verze se nemaže, aby zůstala zachována její data. Přepíná se pouze `installed.json`. **Uživatelská data aplikací patří do jejich AppData; soubory vedle EXE se do nové verze automaticky nekopírují.** Zavřete původní aplikaci před spuštěním nové verze. Není zde automatická migrace nastavení cizích programů ani tichá instalace MSI/EXE.

## Struktura Gitu

```text
jirka-store/
  src/Store.Windows/      Windows launcher
  src/Store.Android/      Android launcher
  src/Store.Core/         katalog, ověřování, stahování, PC instalace
  catalog/catalog.json   publikovaný seznam aplikací
  projects/index.json    společné členění projektů a repozitářů
  shared/                přibalený společný WorkFlow styl
  tools/                 inventura, balení a podepisování
  tests/Store.Checks/     spustitelné regresní kontroly
  local/                 inventura, lokální katalog a podpisový klíč — mimo Git
  packages/              instalační ZIP — mimo Git
  dist/                  EXE a APK — mimo Git
```

Existující repozitáře se neslučují násilně a nepřejmenovávají: například MyBrowser a Fakturocel zůstávají ve společném `home-assistant-addons`, protože tak funguje HA repozitář. Velká videa, 3D soubory, klony cizích projektů ani přihlašovací údaje se nehromadně nepublikují.

`python tools/inventory.py` načte 1. úroveň projektových složek a repozitáře maximálně tři úrovně pod nimi. Výsledek je v `local/PROJECTS.md` a `local/project-inventory.json`; kategorie a názvy jsou návrhy k ověření. Registr `projects/index.json` obsahuje prozkoumané projekty. Nové repozitáře pojmenovávejte bez číselného lokálního prefixu, malými písmeny s pomlčkami. Kategorie patří do registru a GitHub topics, ne do přejmenování zavedených repozitářů.

## Přidání aplikace

Do `catalog/catalog.json` přidejte unikátní `id`, `name`, `category`, `description`, `version` a `kind`. Kategorie: `pc`, `android`, `esp32`, `homeassistant`, `web`, `other`. Verze katalogu má formát `1.2.3` nebo `1.2.3.4`; příponu alpha/beta uvádějte v názvu a popisu. APK se porovnává podle celočíselného `versionCode`.

- `portable-zip`: `url`, `sha256`, `entryPoint` (např. `MojeApp.exe`). ZIP obsahuje soubory aplikace přímo v kořeni.
- `apk`: `url`, `sha256`, `packageName`, `versionCode`.
- `esp-web`: `installUrl` na instalační stránku konkrétního firmwaru, nikoli přímo na BIN.
- `ha-addon`: `repository` s HA repozitářem aplikací.
- `link`: `installUrl` pro webový projekt. `draft`: zatím bez vydání.

Balíčky hostujte v GitHub Releases daného projektu, katalog na HTTPS. Katalog je důvěryhodný seznam spustitelného kódu: SHA-256 zajišťuje integritu proti obsahu katalogu, ne nezávislý podpis vydavatele. Měňte zdroj jen na katalog, jehož správci důvěřujete. Záměrně nepodporujeme instalační skripty z katalogu. Soukromé GitHub repozitáře tento klient bez tokenu nestahuje.

Lokální balíček existující aplikace:

```powershell
.\tools\Package-Windows.ps1 -AppId wifi-scope -SourceDirectory '..\44_WIFI_analizer\dist' -EntryPoint WifiScope.exe -Version 1.1.0
```

Vznikne neměnný ZIP a oddělený `local/catalog.local.json`. Pro další balíček použijte nové číslo verze. Parametr `-DownloadUrl https://github.com/.../releases/download/v.../app.zip` aktualizuje veřejný katalog; před jeho zveřejněním nahrajte odpovídající ZIP. Git ukládá zdroje a katalog, Releases instalační soubory.

Pro vlastní ESP32 připravte firmware a manifest přes [ESP Web Tools](https://esphome.github.io/esp-web-tools/); pro HA dodržte [strukturu repozitáře aplikací](https://contributors.home-assistant.io/apps/guides/repository/). Store deleguje instalaci na tyto existující nástroje.

## Sestavení a kontrola

Vyžaduje .NET 8 SDK; Android také .NET Android workload, Android SDK 34 / build-tools 34 a JDK 17. Žádné balíčky NuGet třetích stran.

```powershell
.\build.ps1 -Android -UseLocalCatalog
# nebo jen Windows:
.\build.ps1
# regresní testy a skutečné stažení malého APK z GitHubu:
dotnet run --project tests/Store.Checks -c Release -- catalog/catalog.json --online
```

`build.ps1` přijímá vlastní `-AndroidSdk` a `-JavaSdk`. Android APK se podepisuje vlastním klíčem vytvořeným v `local/signing/`. **Zálohujte celou tuto složku bezpečně mimo Git; bez původního klíče nepůjdou aktualizovat již nainstalované kopie obchodu.** Nejde o Android debug podpis. Heslo se neukládá do zdrojů ani do argumentů procesů jako prostý text.

Windows styl je kompilační kopie vašeho `D:/20_WorkFlow/_Vzor_aplikaci/WorkflowStyle.cs`, takže repozitář funguje i na jiném počítači. Ikona je vlastní vektorový motiv ve stejné barevné rodině.

## Publikování launcheru

Veřejný repozitář launcherů je `Jirkas-prog/jirka-store`; výchozí katalog je `https://raw.githubusercontent.com/Jirkas-prog/jirka-store/main/catalog/catalog.json`. Nové instalace načítají tuto adresu automaticky, při výpadku mají přibalený katalog. Uživatel může zvolit vlastní HTTPS zdroj, Windows také lokální JSON. Ostatní projekty se zveřejňují jednotlivě; jejich soukromé zdroje launcher nepublikuje. Veřejný katalog je použitelný bez přihlášení; soukromé distribuční zdroje by vyžadovaly doplnění autentizace klientů.

Při novém vydání obchodu zvyšte verze v projektech, u Androidu i `ApplicationVersion`, sestavte, ověřte podpis a nahrajte EXE balíček a APK do nového Release. Veřejné balíčky vytváří `tools/Package-Launchers.ps1` výhradně z EXE, veřejného katalogu a README; lokální aplikační ZIP ani klíče se nepřibalují. Potom aktualizujte katalog, u `jirka-store-android` zejména verzi, `versionCode`, URL a SHA-256. Android tak může aktualizovat i samotný obchod přes systémový instalátor. Novou Windows verzi obchodu rozbalte vedle staré; instalované aplikace zůstanou v profilu uživatele. Automatické přepisování právě běžícího Windows obchodu není implementováno.
