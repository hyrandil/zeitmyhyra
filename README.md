# Zeitmyhyra

Browserbasierte Zeiterfassung mit Node/Express/TypeScript (Backend), React/TypeScript (Frontend) und PostgreSQL über Prisma. Enthält Authentifizierung via JWT, Rollen (Employee, Team Lead, HR, Admin) und CRUD-Oberflächen für Mitarbeiter, Zeiterfassung, Urlaub und Berichte.

## Projektstruktur
```
backend/   # Express + Prisma API
frontend/  # React SPA (Vite)
```

## Backend
1. `.env` aus `.env.example` kopieren und Datenbank-URL/JWT anpassen.
2. Abhängigkeiten installieren und Prisma-Client generieren:
   ```bash
   cd backend
   npm install
   npm run prisma:generate
   # optional: Migration/Seed
   npm run prisma:migrate
   npx ts-node prisma/seed.ts
   ```
3. Entwicklung starten:
   ```bash
   npm run dev
   ```

Wichtige Endpunkte (alle prefixed mit `/api`):
- `POST /auth/login` – Login, liefert JWT
- `POST /auth/register` – Benutzer anlegen (nur Admin)
- `GET /auth/me` – Profil & verknüpfte Employee-ID für eingeloggte Nutzer
- `GET/POST/PUT/DELETE /employees` – Mitarbeiterverwaltung (HR/Admin)
- `POST /time/stamp` – Kommen/Gehen/Pause buchen
- `GET /time/monthly` – Monatsübersicht mit Summen
- `POST /leave` – Urlaubsantrag stellen; `POST /leave/:id/review` – Genehmigen/Ablehnen
- `GET /reports/monthly` – Monatsbericht; `GET /reports/overtime` – Überstunden-Report

Beispiel-Login nach Seed: `admin@example.com` / `admin123`.

## Frontend
1. In separatem Terminal:
   ```bash
   cd frontend
   npm install
   npm run dev
   ```
2. Standard-API-URL ist `http://localhost:4000/api`. Anpassbar via `VITE_API_URL`.

Seiten:
- Login
- Dashboard (Status + letzte Stempelungen)
- Zeiterfassung (Buttons + Monatsübersicht)
- Urlaubsanträge (Formular + eigene Anträge)
- Mitarbeiterverwaltung (Tabelle + Anlage)
- Berichte (Filter + Export JSON)

## Tests
Beispiel-Vitest für Zeitberechnung:
```bash
cd backend
npm test
```
