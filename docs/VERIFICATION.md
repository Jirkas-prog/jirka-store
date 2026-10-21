# Ověření 21. října 2026

- Windows: sestavené samostatné EXE pro Windows x64 včetně runtime, nulový počet chyb a varování při kompilaci zdrojů. Otevření a vykreslení skutečného WinForms formuláře ověřeno režimem `--screenshot`.
- Android: sestavené APK pro Android 9+, nulový počet chyb a varování při kompilaci. Samostatný stabilní RSA podpis `CN=Jirka Store`; ověřeno pomocí Android `apksigner verify`.
- Společné jádro: 26 regresních kontrol parsování, instalace, aktualizace, zachování staré verze, odmítnutí downgrade, kontrolního součtu, úniku cest ze ZIP, chybějícího EXE, zrušení a zachování již existujícího cíle stahování; navíc validace skutečného katalogu.
- Integrační kontrola: stažení existujícího Zephyr APK z GitHub Releases a ověření SHA-256. Jeho `packageName` a `versionCode=10002` byly ověřeny také Android `aapt` proti skutečnému APK.
- Integrační kontrola: instalace skutečného lokálního balíčku WiFi Scope do dočasného adresáře, kontrola existence deklarovaného EXE, úklid pouze testovacího adresáře. Uživatelova běžná instalace se tím nevytváří ani nepřepisuje.
- Inventura: 48 lokálních projektových složek, 9 Git repozitářů při vynechání výzkumných klonů, závislostí a archivů verzí. Celá inventura zůstává v ignorované složce `local`.

Na počítači nebyl připojený Android telefon ani spuštěný emulátor (`adb devices` bez zařízení). Interakce se systémovým Android instalátorem tedy zatím nebyla vyzkoušena na zařízení. Nebyla provedena instalace firmwaru do fyzického ESP32 ani změna v Home Assistantu. Odkazy delegují tyto kroky na odpovídající nativní nástroje.

Centrální veřejný repozitář je `Jirkas-prog/jirka-store`. Ostatní projekty zůstávají ve vlastních repozitářích; součástí veřejného vydání launcheru jsou pouze jeho EXE, APK a katalog. Lokální balíček WiFi Scope zůstává pouze lokální.

Publikováno [zkušební vydání 1.0.0](https://github.com/Jirkas-prog/jirka-store/releases/tag/v1.0.0). Veřejný katalog byl načten bez autentizace skutečným klientem a prošel validací. SHA-256 obou balíčků vrácené GitHub Releases souhlasí s lokálními soubory. První [GitHub Actions sestavení a regresní kontroly](https://github.com/Jirkas-prog/jirka-store/actions/runs/39646457704) skončily úspěšně.
