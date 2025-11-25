import { useEffect, useState } from 'react';
import { apiClient } from '../api/client';
import { Role } from '../types';
import { useAuth } from '../hooks/useAuth';

interface Employee {
  id: number;
  personnelNumber: string;
  name: string;
  email: string;
  department: string;
  location: string;
  role: Role;
}

export default function EmployeesPage() {
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [filter, setFilter] = useState('');
  const [form, setForm] = useState<Partial<Employee> & { workScheduleId?: number; startDate?: string }>({
    personnelNumber: '',
    name: '',
    email: '',
    department: '',
    location: '',
    role: 'EMPLOYEE',
    workScheduleId: 1,
    startDate: new Date().toISOString().slice(0, 10)
  });
  const { user } = useAuth();
  const canManage = user?.role === 'HR' || user?.role === 'ADMIN';

  const load = async () => {
    const data = await apiClient.get('/employees');
    setEmployees(data);
  };

  useEffect(() => {
    load();
  }, []);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    await apiClient.post('/employees', form);
    setForm({ ...form, personnelNumber: '', name: '', email: '' });
    load();
  };

  const filtered = employees.filter((e) => e.name.toLowerCase().includes(filter.toLowerCase()) || e.email.includes(filter));

  return (
    <div className="grid">
      {canManage && (
        <div className="card">
          <h3>Mitarbeiter anlegen</h3>
          <form onSubmit={submit}>
            <div className="form-row">
              <input placeholder="Personalnummer" value={form.personnelNumber || ''} onChange={(e) => setForm({ ...form, personnelNumber: e.target.value })} />
              <input placeholder="Name" value={form.name || ''} onChange={(e) => setForm({ ...form, name: e.target.value })} />
            </div>
            <div className="form-row">
              <input placeholder="E-Mail" value={form.email || ''} onChange={(e) => setForm({ ...form, email: e.target.value })} />
              <input placeholder="Standort" value={form.location || ''} onChange={(e) => setForm({ ...form, location: e.target.value })} />
              <input placeholder="Abteilung" value={form.department || ''} onChange={(e) => setForm({ ...form, department: e.target.value })} />
            </div>
            <div className="form-row">
              <label>Rolle</label>
              <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value as Role })}>
                <option value="EMPLOYEE">Mitarbeiter</option>
                <option value="TEAM_LEAD">Teamleiter</option>
                <option value="HR">HR</option>
                <option value="ADMIN">Admin</option>
              </select>
              <label>Start</label>
              <input type="date" value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} />
            </div>
            <button type="submit">Speichern</button>
          </form>
        </div>
      )}
      <div className="card">
        <h3>Mitarbeiterliste</h3>
        <input placeholder="Filter nach Name/E-Mail" value={filter} onChange={(e) => setFilter(e.target.value)} />
        <table className="table">
          <thead>
            <tr>
              <th>#</th>
              <th>Name</th>
              <th>E-Mail</th>
              <th>Abteilung</th>
              <th>Rolle</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((emp) => (
              <tr key={emp.id}>
                <td>{emp.personnelNumber}</td>
                <td>{emp.name}</td>
                <td>{emp.email}</td>
                <td>{emp.department}</td>
                <td>{emp.role}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
