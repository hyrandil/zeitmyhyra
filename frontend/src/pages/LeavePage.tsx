import { useEffect, useState } from 'react';
import { apiClient } from '../api/client';
import { useAuth } from '../hooks/useAuth';
import { Role } from '../types';

interface LeaveRequest {
  id: number;
  startDate: string;
  endDate: string;
  leaveType: string;
  status: string;
  comment?: string;
}

export default function LeavePage() {
  const [requests, setRequests] = useState<LeaveRequest[]>([]);
  const [form, setForm] = useState({ startDate: '', endDate: '', leaveType: 'VACATION', comment: '' });
  const { user } = useAuth();
  const [employeeId, setEmployeeId] = useState<number | ''>(user?.employeeId ?? '');

  const load = async () => {
    if (!employeeId) {
      setRequests([]);
      return;
    }
    const data = await apiClient.get(`/leave/${employeeId}`);
    setRequests(data);
  };

  useEffect(() => {
    setEmployeeId(user?.employeeId ?? '');
  }, [user]);

  useEffect(() => {
    load();
  }, [employeeId]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!employeeId) return;
    await apiClient.post('/leave', { ...form, employeeId });
    setForm({ startDate: '', endDate: '', leaveType: 'VACATION', comment: '' });
    load();
  };

  return (
    <div className="grid">
      <div className="card">
        <h3>Urlaubsantrag stellen</h3>
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
        <form onSubmit={submit}>
          <div className="form-row">
            <label>Von</label>
            <input type="date" value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} />
            <label>Bis</label>
            <input type="date" value={form.endDate} onChange={(e) => setForm({ ...form, endDate: e.target.value })} />
          </div>
          <div className="form-row">
            <label>Art</label>
            <select value={form.leaveType} onChange={(e) => setForm({ ...form, leaveType: e.target.value })}>
              <option value="VACATION">Urlaub</option>
              <option value="SICKNESS">Krankheit</option>
              <option value="SPECIAL">Sonderurlaub</option>
            </select>
          </div>
          <div className="form-row">
            <label>Kommentar</label>
            <input value={form.comment} onChange={(e) => setForm({ ...form, comment: e.target.value })} />
          </div>
          <button type="submit" disabled={!employeeId}>
            Antrag senden
          </button>
        </form>
      </div>
      <div className="card">
        <h3>Meine Anträge</h3>
        <table className="table">
          <thead>
            <tr>
              <th>Zeitraum</th>
              <th>Art</th>
              <th>Status</th>
              <th>Kommentar</th>
            </tr>
          </thead>
          <tbody>
            {requests.map((req) => (
              <tr key={req.id}>
                <td>
                  {new Date(req.startDate).toLocaleDateString()} - {new Date(req.endDate).toLocaleDateString()}
                </td>
                <td>{req.leaveType}</td>
                <td>{req.status}</td>
                <td>{req.comment}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
