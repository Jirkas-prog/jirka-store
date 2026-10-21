# Společné uspořádání

Uživatelem schválený model: **samostatný repozitář pro každý skutečný projekt**, společný veřejný katalog a oba launchery v **jirka-store**. Ostatní projekty se zveřejňují jednotlivě. Nevzniká jeden velký repozitář se všemi 48 pracovními složkami; existující historie zůstávají nedotčené.

| Oblast | Repozitář | Poznámka |
| --- | --- | --- |
| Launchery a katalog | `Jirkas-prog/jirka-store` | Nový společný veřejný repozitář. |
| Android / Zephyr | `Jirkas-prog/termux-zephyr-android` | Existující vydání 1.0.2 napojené na katalog. |
| Android / FreeCAD | `Jirkas-prog/freecad-android` | Existující alpha vydání napojené na katalog. |
| HAOS / MyBrowser a Fakturocel | `Jirkas-prog/home-assistant-addons` | Ponechat společně, jde o nativní HA repozitář. |
| ESP32-S3 Úkoly | `Jirkas-prog/live-esp32-dev` | Zjištěno z lokálního origin; veřejný přístup nevrací repozitář. Neinterpretovat jako smazání. |
| PC / WiFi Scope | navrženo `Jirkas-prog/wifi-scope` | Funkční lokální ZIP. Samostatný repozitář zatím nezaložen. |
| Ostatní projekty | podle `local/PROJECTS.md` | Automatické návrhy názvů a kategorií je nutné věcně ověřit. |

## Pravidla

1. Lokální pořadová čísla (`44_…`) nepatří do nových názvů repozitářů. Použít stabilní `wifi-scope`, `esp-termostat` apod.
2. Kategorie PC / Android / ESP32 / HAOS jsou v `projects/index.json` a katalogu; GitHub nemá složky nad repozitáři. Přehled lze doplnit topics `jirka-app`, `windows`, `android`, `esp32`, `home-assistant`.
3. Zachovat již používaná jména repozitářů, URL i commit historii. Cizí klony nepřevádět na vlastní projekty a nezahrnovat automaticky do migrace.
4. Zdrojové kódy a konfigurace bez tajných údajů patří do Gitu; sestavení do Releases. Vývojářské klíče, tokeny a lokální stavy se nepublikují.
5. Každé vydání má neměnnou URL a verzi, SHA-256 a konkrétní platformu. Katalog se aktualizuje až poté, co je balíček dostupný.
6. Existující pracovní složky se přesouvají až po ověření vazeb v projektech. Samotný sjednocený katalog přesun složek nepotřebuje.

## Další projekty

Veřejný katalog funguje bez přihlašování v obou aplikacích. Soukromé projekty mohou zůstat soukromé nezávisle na launcheru, ale jejich binární balíčky vyžadují veřejný distribuční kanál nebo následné doplnění přihlášení. Lokální WiFi Scope balíček nebyl automaticky zahrnut do veřejného vydání launcheru.

Žádné existující GitHub repozitáře nebyly touto přípravou přejmenovány, přesunuty ani sloučeny. Další projekty z inventury se připojují samostatně po rozhodnutí o jejich rozsahu a viditelnosti.
