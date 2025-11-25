import { useEffect, useState } from 'react';
import { apiClient } from '../api/client';
import { BookingType, Role } from '../types';
import { useAuth } from '../hooks/useAuth';

interface TimeEntry {
  id: number;
  type: string;
  timestampUtc: string;
  source: string;
}

export default function TimeTrackingPage() {
  const [entries, setEntries] = useState<TimeEntry[]>([]);
  const [summary, setSummary] = useState<{ date: string; readable: string }[]>([]);
  const { user } = useAuth();
  const [employeeId, setEmployeeId] = useState<number | ''>(user?.employeeId ?? '');

  const loadEntries = async () => {
    if (!employeeId) {
      setEntries([]);
      setSummary([]);
      return;
    }
    const now = new Date();
    const month = `${now.getUTCFullYear()}-${String(now.getUTCMonth() + 1).padStart(2, '0')}`;
    const data = await apiClient.get(`/time/monthly?employeeId=${employeeId}&month=${month}`);
    setEntries(data.entries);
    setSummary(data.summary);
  };

  useEffect(() => {
    setEmployeeId(user?.employeeId ?? '');
  }, [user]);

  useEffect(() => {
    loadEntries();
  }, [employeeId]);

  const stamp = async (type: BookingType) => {
    if (!employeeId) return;
    await apiClient.post('/time/stamp', { employeeId, type, source: 'WEB' });
    loadEntries();
  };

  return (
    <div>
      <div className="card">
        <h3>Zeiterfassung</h3>
        {!employeeId && <p className="text-muted">Kein Mitarbeiter gewählt. Bitte ID eintragen oder HR um Verknüpfung bitten.</p>}
        {(user?.role === ('ADMIN' as Role) || user?.role === ('HR' as Role) || user?.role === ('TEAM_LEAD' as Role)) && (
          <div className="form-row">
            <label>Mitarbeiter-ID</label>
            <input
              type="number"
              value={employeeId}
              onChange={(e) => setEmployeeId(e.target.value ? Number(e.target.value) : '')}
            />
          </div>
        )}
        <div className="form-row">
          <button onClick={() => stamp('KOMMEN')} disabled={!employeeId}>
            Kommen
          </button>
          <button onClick={() => stamp('GEHEN')} disabled={!employeeId}>
            Gehen
          </button>
          <button onClick={() => stamp('PAUSE_START')} disabled={!employeeId}>
            Pause starten
          </button>
          <button onClick={() => stamp('PAUSE_ENDE')} disabled={!employeeId}>
            Pause beenden
          </button>
        </div>
      </div>
      <div className="card">
        <h3>Monatsübersicht</h3>
        <table className="table">
          <thead>
            <tr>
              <th>Datum</th>
              <th>Typ</th>
              <th>Uhrzeit</th>
              <th>Quelle</th>
            </tr>
          </thead>
          <tbody>
            {entries.map((entry) => (
              <tr key={entry.id}>
                <td>{new Date(entry.timestampUtc).toLocaleDateString()}</td>
                <td>{entry.type}</td>
                <td>{new Date(entry.timestampUtc).toLocaleTimeString()}</td>
                <td>{entry.source}</td>
              </tr>
            ))}
          </tbody>
        </table>
        <h4>Summen</h4>
        <ul>
          {summary.map((item) => (
            <li key={item.date}>
              {item.date}: {item.readable}
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
