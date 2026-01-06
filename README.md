# CanteenRFID

Portable ASP.NET Core + SQLite Lösung für Kantinen-RFID-Erfassung und Reader-Client.

## Voraussetzungen
- Windows 10/11 oder Windows Server
- .NET 8 SDK/Runtime für Entwicklung (für Deployment: veröffentlichter Ordner genügt)

## Projektstruktur
- `src/CanteenRFID.Core` – Modelle, Enums, Regel-Engine
- `src/CanteenRFID.Data` – EF Core DbContext
- `src/CanteenRFID.Web` – ASP.NET Core MVC + Minimal API, Razor Views, Exporte
- `src/CanteenRFID.ReaderClient` – Konsolenclient für Keyboard-Wedge-Reader
- `tests/CanteenRFID.Tests` – Basis-Tests (Rule-Engine)
- `data/canteen.db` – SQLite Datenbank (wird automatisch erzeugt)
- `logs/` – Serilog-Ausgabe

## Web starten
```bash
# Entwicklung
cd src/CanteenRFID.Web
dotnet run
```

Für portable Auslieferung auf Windows:
```bash
dotnet publish -c Release -r win-x64 /p:PublishSingleFile=true
```
Im Publish-Output liegt `CanteenRFID.Web.exe` sowie `appsettings.json`. Datenbank befindet sich unter `./data/canteen.db`.

### Konfiguration
- `appsettings.json` enthält Admin-Anmeldedaten, Logging und Zeitzone.
- Zeitzone Default: `W. Europe Standard Time` (Europe/Berlin).

### Admin-Seed
Beim Start wird der Standard-Admin (User `admin`, Passwort `ChangeMe123!`) auf der Konsole ausgegeben.

## Funktionen (Web)
- Login mit Cookie-Auth (Rollen Admin/Viewer)
- Benutzerverwaltung (CRUD, Personalnummer/UID eindeutig)
- Stempelungen anzeigen und filtern (Meal-Type, Zeitraum, Suche)
- Meal-Type-Regeln verwalten + Neu-Berechnung
- Readerverwaltung mit API-Key-Generierung (Hash in DB)
- Exporte: Excel (ClosedXML) & PDF (QuestPDF) mit Summary + RawEvents
- Minimal API `/api/v1/stamps` für Reader (Header `X-API-KEY`)

## Reader-Client
- Konsolenanwendung (Keyboard-Wedge)
- Konfiguration in `readerclientsettings.json` (ServerUrl, ApiKey, ReaderId)
- Offline-Queue unter `./queue/queue.jsonl` – wird beim nächsten Start/Online-Status gesendet
- Start per Autostart oder geplanter Task möglich

## Beispiel-Meal-Rules
- Frühstück: 07:00–10:00 (alle Tage)
- Mittag: 10:00–15:00 (alle Tage)
- Abend: 15:00–20:00 (alle Tage)

## Exporte nutzen
- Navigiere zu **Exporte**, Zeitraum wählen und Excel oder PDF generieren. Dateiname enthält den Zeitraum.

## Tests
```bash
cd tests/CanteenRFID.Tests
 dotnet test
```

