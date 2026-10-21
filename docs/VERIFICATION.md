# Ověření 21. října 2026

## Windows 1.1.0 — zdrojový fork ArtCraftu

- Zdroj: lokální čistý klon `storytold/craft-launcher`, commit `6e7a122866591c067344687c45c92f735a1d189b`. Převzaté zdroje a licence jsou v `launcher/`, změny popisuje `launcher/FORK.md`.
- Skutečné egui rozložení: 14 kombinací rozměrů, měřítka a světlého/tmavého vzhledu; od 520 × 360 do 1390 × 900, včetně 150% měřítka. Ověřené hranice filtrů, navigace, karet, dlouhých textů a tlačítek. Úzký pohled se posouvá svisle, tabulka má vlastní vodorovný posuvník.
- Interakce: filtrování, hledání, tabulka a potvrzení / zrušení instalace. Tři UI testy prošly. Mřížka, tabulka a světlý vzhled byly také vizuálně zkontrolovány na snímcích ze skutečného rendereru egui.
- Store.Core: všech 27 kontrol prošlo, včetně aktualizace, zachování staré verze, odmítnutí downgrade, kontroly SHA-256, cest v ZIP a zrušení operace.
- Store.Host: skutečné sestavené EXE prošlo testem JSON komunikace, instalací lokálního WiFi Scope do dočasného profilu, odmítnutím stejné verze, zachováním katalogu při chybě zdroje, odmítnutím neplatného příkazu a čistým ukončením.
- Původní Rust core: 26 testů prošlo, jeden upstream test vyžadující externí vzorek je označený jako ignorovaný.
- Finální Rust release a samostatná instalační služba byly sestaveny. Windows ZIP má 38 003 466 bajtů, obsahuje jen veřejný katalog, EXE, engine, README a licence; SHA-256 je `4e5e13497d416167f8987ca62e54fd262847f83dcd8cd94f49cc161e86b3a6da`.
- Finální EXE se spustilo. Snímání živého okna přes Computer Use zůstalo blokované stavem `window is minimized`; vizuální kontrola vychází z egui rendereru, nikoli ze snímku živého desktopového okna.
- Android, fyzické ESP32 a Home Assistant se tímto Windows vydáním nemění. Android APK zůstává ve vydání 1.0.0.

## Původní vydání 1.0.0

- Windows: sestavené samostatné EXE pro Windows x64 včetně runtime, nulový počet chyb a varování při kompilaci zdrojů. Otevření a vykreslení skutečného WinForms formuláře ověřeno režimem `--screenshot`.
- Android: sestavené APK pro Android 9+, nulový počet chyb a varování při kompilaci. Samostatný stabilní RSA podpis `CN=Jirka Store`; ověřeno pomocí Android `apksigner verify`.
- Společné jádro: 26 regresních kontrol parsování, instalace, aktualizace, zachování staré verze, odmítnutí downgrade, kontrolního součtu, úniku cest ze ZIP, chybějícího EXE, zrušení a zachování již existujícího cíle stahování; navíc validace skutečného katalogu.
- Integrační kontrola: stažení existujícího Zephyr APK z GitHub Releases a ověření SHA-256. Jeho `packageName` a `versionCode=10002` byly ověřeny také Android `aapt` proti skutečnému APK.
- Integrační kontrola: instalace skutečného lokálního balíčku WiFi Scope do dočasného adresáře, kontrola existence deklarovaného EXE, úklid pouze testovacího adresáře. Uživatelova běžná instalace se tím nevytváří ani nepřepisuje.
- Inventura: 48 lokálních projektových složek, 9 Git repozitářů při vynechání výzkumných klonů, závislostí a archivů verzí. Celá inventura zůstává v ignorované složce `local`.

Na počítači nebyl připojený Android telefon ani spuštěný emulátor (`adb devices` bez zařízení). Interakce se systémovým Android instalátorem tedy zatím nebyla vyzkoušena na zařízení. Nebyla provedena instalace firmwaru do fyzického ESP32 ani změna v Home Assistantu. Odkazy delegují tyto kroky na odpovídající nativní nástroje.

Centrální veřejný repozitář je `Jirkas-prog/jirka-store`. Ostatní projekty zůstávají ve vlastních repozitářích; součástí veřejného vydání launcheru jsou pouze jeho EXE, APK a katalog. Lokální balíček WiFi Scope zůstává pouze lokální.

Publikováno [zkušební vydání 1.0.0](https://github.com/Jirkas-prog/jirka-store/releases/tag/v1.0.0). Veřejný katalog byl načten bez autentizace skutečným klientem a prošel validací. SHA-256 obou balíčků vrácené GitHub Releases souhlasí s lokálními soubory. První [GitHub Actions sestavení a regresní kontroly](https://github.com/Jirkas-prog/jirka-store/actions/runs/39646457704) skončily úspěšně.
