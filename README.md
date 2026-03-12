# CanteenRFID

Portable ASP.NET Core + SQLite Lösung für Kantinen-RFID-Erfassung und Reader-Client.

## Voraussetzungen
- Windows 10/11 oder Windows Server
- .NET 8 SDK für Entwicklung (für Deployment reicht veröffentlichter Ordner)
- Node.js 18+ falls die npm-Kommandos genutzt werden sollen

## Projektstruktur
- `src/CanteenRFID.Core` – Modelle, Enums, Regel-Engine
- `src/CanteenRFID.Data` – EF Core DbContext und Seed-Daten
- `src/CanteenRFID.Web` – ASP.NET Core MVC + Minimal API, Razor Views, Exporte, Serilog
- `src/CanteenRFID.ReaderClient` – Konsolenclient für Keyboard-Wedge-Reader
- `tests/CanteenRFID.Tests` – Basis-Tests (Rule-Engine)
- `data/canteen.db` – SQLite Datenbank (wird automatisch erzeugt)
- `logs/` – Serilog-Ausgabe

## npm-Kommandos
Die Anwendung kann über npm-Skripte gestartet/gebaut werden (führt intern dotnet-Befehle aus):

```bash
npm install  # nur einmal nötig
npm run build     # baut die Solution
npm run start:web # startet das Web-Projekt via dotnet run
npm test          # ruft dotnet test auf
```

## Web starten
```bash
# Entwicklung
cd src/CanteenRFID.Web
dotnet run
```

### Installation unter Linux (Debian/Ubuntu)
Die Anwendung läuft auch unter Linux; empfohlen wird ein aktuelles Debian/Ubuntu.

1. .NET 8 Runtime/SDK installieren (Microsoft-Paketquelle einbinden):
   ```bash
   sudo apt update
   sudo apt install -y wget gpg apt-transport-https
   wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
   sudo dpkg -i packages-microsoft-prod.deb
   rm packages-microsoft-prod.deb
   sudo apt update
   sudo apt install -y dotnet-sdk-8.0
   ```
   *(Für Ubuntu ggf. `ubuntu/22.04` in der URL wählen.)*

2. (Optional) Node.js installieren, falls die npm-Skripte genutzt werden sollen:
   ```bash
   sudo apt install -y nodejs npm
   ```

3. Projekt bauen/veröffentlichen:
   ```bash
   dotnet publish src/CanteenRFID.Web -c Release -r linux-x64 --self-contained false
   ```
   Der Publish-Ordner liegt unter `src/CanteenRFID.Web/bin/Release/net8.0/linux-x64/publish` und enthält `CanteenRFID.Web` (ausführbar), `appsettings.json` sowie `data/canteen.db` (wird bei Bedarf erzeugt).

4. Starten (Beispiel):
   ```bash
   cd src/CanteenRFID.Web/bin/Release/net8.0/linux-x64/publish
   ./CanteenRFID.Web
   ```

5. Firewall/Portfreigabe beachten (Standard: Port 5000/5001). Für Systemd-Betrieb kann ein Service-File erstellt werden, das das veröffentlichte Binary aufruft.

Für portable Auslieferung auf Windows:
```bash
# Web-Server als Single-File (empfohlen):
dotnet publish src/CanteenRFID.Web -c Release -r win-x64 /p:PublishSingleFile=true

# Reader-Client als Single-File (optional):
dotnet publish src/CanteenRFID.ReaderClient -c Release -r win-x64 /p:PublishSingleFile=true
```
Im Publish-Output liegt `CanteenRFID.Web.exe` sowie `appsettings.json`. Datenbank befindet sich unter `./data/canteen.db`.
Bitte keine Lösung/Library-Projekte mit `PublishSingleFile=true` veröffentlichen (CanteenRFID.Core, CanteenRFID.Data, Tests sind nicht publishbar).

### Konfiguration
- `appsettings.json` enthält Admin-Anmeldedaten (werden beim ersten Start in `./data/admin.secret.json` gehasht abgelegt), Logging und Zeitzone.
- Zeitzone Default: `W. Europe Standard Time` (Europe/Berlin).

### Admin-Passwort ändern/zurücksetzen
- **Im Web**: Angemeldete Admins können unter **Passwort ändern** (Navigationsleiste) das Kennwort nach Eingabe des aktuellen Passworts setzen. Die Änderung wird gehasht in `./data/admin.secret.json` gespeichert.
- **Commandline-Reset (z. B. bei vergessenem Passwort)**: Im Publish-Ordner ausführen und neues Passwort übergeben:
  ```bash
  # Windows
  CanteenRFID.Web.exe --reset-admin-password "NeuesSicheresPasswort!"
  # Linux/macOS
  ./CanteenRFID.Web --reset-admin-password "NeuesSicheresPasswort!"
  ```
  Der Befehl überschreibt das gespeicherte Passwort (Username bleibt gleich) und beendet anschließend die Anwendung.

### Admin-Seed
Beim ersten Start wird der Standard-Admin (User `admin`, Passwort `ChangeMe123!`) auf der Konsole ausgegeben und in `./data/admin.secret.json` abgelegt. Danach sollte das Passwort über die UI oder den Reset-Befehl geändert werden.

## Funktionen (Web)
- Login mit Cookie-Auth (Rollen Admin/Viewer)
- Benutzerverwaltung (CRUD, Personalnummer/UID eindeutig)
- Stempelungen anzeigen und live filtern (Meal-Type, Zeitraum, Suche, Reader)
- Meal-Type-Regeln verwalten + Neu-Berechnung
- Readerverwaltung mit API-Key-Generierung (Hash in DB)
- Exporte: Excel (ClosedXML) & PDF (QuestPDF) mit Summary + RawEvents, optional Filter
- Minimal API `/api/v1/stamps` für Reader (Header `X-API-KEY`), weitere Admin-APIs für Benutzer/Regeln/Reader

## Reader-Client
- Konsolenanwendung (Keyboard-Wedge)
- Konfiguration in `readerclientsettings.json` (ServerUrl, ApiKey, ReaderId, Terminator, PingIntervalSeconds)
- Offline-Queue unter `./queue/queue.jsonl` – wird beim nächsten Start/Online-Status gesendet
- Start per Autostart oder geplanter Task möglich

### Reader einrichten
1. Im Web UI Reader anlegen oder per API erzeugen.
2. API-Key notieren und in `readerclientsettings.json` eintragen.
3. `ServerUrl` und `ReaderId` setzen, dann Client starten. Der Client sendet Stempelungen sowie regelmäßige "Ping"-Signale an `/api/v1/readers/ping`, damit der Server den Online-Status erkennt (Intervall via `PingIntervalSeconds`).

### Reader-Client als Windows Service installieren
1. Reader-Client veröffentlichen (z. B. `dotnet publish src/CanteenRFID.ReaderClient -c Release -r win-x64`).
2. In den Publish-Ordner wechseln und den Service registrieren:
   ```powershell
   sc.exe create CanteenRFID.ReaderClient binPath= "\"C:\Pfad\zur\CanteenRFID.ReaderClient.exe\"" start= delayed-auto
   sc.exe description CanteenRFID.ReaderClient "CanteenRFID Reader Client (RFID/Barcode Eingaben)"
   ```
3. Service starten:
   ```powershell
   sc.exe start CanteenRFID.ReaderClient
   ```
4. Optional beenden/entfernen:
   ```powershell
   sc.exe stop CanteenRFID.ReaderClient
   sc.exe delete CanteenRFID.ReaderClient
   ```
Der Service ist danach unter `services.msc` sichtbar. Konfiguration liegt weiterhin in `readerclientsettings.json` im gleichen Ordner wie die EXE.

## Reader-Display (Web)
Für den Mini-PC kann zusätzlich eine Vollbild-Webanzeige genutzt werden, die nach jeder Stempelung eine Bestätigung zeigt (großer Meal-Name + grüner Haken, optional Mitarbeitername).

1. Im Reader-Adminbereich ein **Reader-Display Passwort** setzen (unter **Reader** in der Tabelle oder beim Anlegen).
2. Im Browser die URL mit Reader-ID und Passwort aufrufen:  
   `http://<server>:5000/ReaderDisplay=<READER_ID>&pw=<PASSWORT>`
3. Die Seite startet automatisch für den Reader. Nach jeder Stempelung erscheint der Meal-Name groß mit grünem Haken und optional `Nachname, Vorname`. Die Anzeige verschwindet nach 5 Sekunden oder bei der nächsten Stempelung.

## Beispiel-Meal-Rules
- Frühstück: 07:00–10:00 (alle Tage)
- Mittag: 10:00–15:00 (alle Tage)
- Abend: 15:00–20:00 (alle Tage)

## Exporte nutzen
- Navigiere zu **Exporte**, Zeitraum wählen und optional Meal-Type/Benutzer filtern.
- Excel enthält Summary + RawEvents, PDF enthält Summary + Tagesübersicht.

## Tests
```bash
npm test
# oder
cd tests/CanteenRFID.Tests
dotnet test
```
