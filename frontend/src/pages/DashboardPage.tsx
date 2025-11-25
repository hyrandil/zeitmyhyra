import { useEffect, useState } from 'react';
import { apiClient } from '../api/client';
import { useAuth } from '../hooks/useAuth';

interface Status {
  lastEntries: any[];
}

export default function DashboardPage() {
  const { user } = useAuth();
  const [status, setStatus] = useState<Status>({ lastEntries: [] });

  useEffect(() => {
    const load = async () => {
      try {
        if (!user?.employeeId) return;
        const now = new Date();
        const month = `${now.getUTCFullYear()}-${String(now.getUTCMonth() + 1).padStart(2, '0')}`;
        const data = await apiClient.get(`/time/monthly?month=${month}`);
        setStatus({ lastEntries: data.entries.slice(-4).reverse() });
      } catch (e) {
        console.warn(e);
      }
    };
    load();
  }, [user]);

  return (
    <div className="grid">
      <div className="card">
        <h3>Willkommen {user?.name}</h3>
        <p>Rolle: {user?.role}</p>
        <p>Aktueller Monat: schnelle Übersicht deiner Buchungen.</p>
        {!user?.employeeId && <p className="text-muted">Kein Mitarbeiter-Profil verknüpft. Bitte HR um Zuweisung.</p>}
      </div>
      <div className="card">
        <h3>Letzte Stempelungen</h3>
        <ul>
          {status.lastEntries.map((entry, idx) => (
            <li key={idx}>
              {entry.type} - {new Date(entry.timestampUtc).toLocaleString()} ({entry.source})
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
