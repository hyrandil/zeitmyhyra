# Setup Leitfaden

1. **Web veröffentlichen**
   - `dotnet publish -c Release -r win-x64 /p:PublishSingleFile=true`
   - Output entpacken, `appsettings.json` und `data/` verbleiben im Ordner.
2. **Admin-Login**
   - Standard: `admin` / `ChangeMe123!`
   - In `appsettings.json` anpassen.
3. **Reader registrieren**
   - Im Web UI Reader anlegen, API Key regenerieren und für Client notieren.
4. **Reader-Client konfigurieren**
   - `readerclientsettings.json` mit `ServerUrl`, `ApiKey`, `ReaderId` ausfüllen.
   - Client per Autostart oder Task Scheduler starten.
5. **Offline Queue**
   - Fällt Server aus, sammelt Client Einträge in `./queue/queue.jsonl`.
   - Beim nächsten Start/Online werden Einträge automatisch gesendet.
