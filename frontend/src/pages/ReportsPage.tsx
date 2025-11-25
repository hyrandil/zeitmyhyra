import { useEffect, useState } from 'react';
import { apiClient } from '../api/client';
import { useAuth } from '../hooks/useAuth';
import { Role } from '../types';

export default function ReportsPage() {
  const [month, setMonth] = useState(() => {
    const now = new Date();
    return `${now.getUTCFullYear()}-${String(now.getUTCMonth() + 1).padStart(2, '0')}`;
  });
  const [report, setReport] = useState<any>(null);
  const { user } = useAuth();
  const [employeeId, setEmployeeId] = useState<number | ''>(user?.employeeId ?? '');

  const load = async () => {
    if (!employeeId) {
      setReport(null);
      return;
    }
    const data = await apiClient.get(`/reports/monthly?employeeId=${employeeId}&month=${month}`);
    setReport(data);
  };

  useEffect(() => {
    setEmployeeId(user?.employeeId ?? '');
  }, [user]);

  useEffect(() => {
    load();
  }, [employeeId]);

  const downloadJson = () => {
    if (!report) return;
    const blob = new Blob([JSON.stringify(report, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `report-${month}.json`;
    a.click();
  };

  return (
    <div className="card">
      <h3>Berichte</h3>
      <div className="form-row">
        <label>Monat</label>
        <input type="month" value={month} onChange={(e) => setMonth(e.target.value)} />
        <button onClick={load}>Bericht laden</button>
        <button onClick={downloadJson}>Als JSON speichern</button>
      </div>
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
      {!employeeId && <p className="text-muted">Kein Mitarbeiter gewählt. Bitte ID eintragen oder HR um Verknüpfung bitten.</p>}
      {report && (
        <div>
          <h4>Tageswerte</h4>
          <ul>
            {report.summary.map((item: any) => (
              <li key={item.date}>
                {item.date}: {item.readable}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
