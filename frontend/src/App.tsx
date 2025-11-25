import { Routes, Route, Navigate, Link } from 'react-router-dom';
import LoginPage from './pages/LoginPage';
import DashboardPage from './pages/DashboardPage';
import TimeTrackingPage from './pages/TimeTrackingPage';
import LeavePage from './pages/LeavePage';
import EmployeesPage from './pages/EmployeesPage';
import ReportsPage from './pages/ReportsPage';
import { useAuth, AuthProvider } from './hooks/useAuth';

const Protected = ({ children }: { children: JSX.Element }) => {
  const { user } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  return children;
};

const Nav = () => {
  const { user, logout } = useAuth();
  if (!user) return null;
  return (
    <nav className="nav">
      <div className="brand">Zeitmyhyra</div>
      <div className="links">
        <Link to="/">Dashboard</Link>
        <Link to="/time">Zeiterfassung</Link>
        <Link to="/leave">Urlaub</Link>
        <Link to="/reports">Berichte</Link>
        <Link to="/employees">Mitarbeiter</Link>
      </div>
      <button onClick={logout}>Logout</button>
    </nav>
  );
};

function App() {
  return (
    <AuthProvider>
      <div className="app">
        <Nav />
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route
            path="/"
            element={
              <Protected>
                <DashboardPage />
              </Protected>
            }
          />
          <Route
            path="/time"
            element={
              <Protected>
                <TimeTrackingPage />
              </Protected>
            }
          />
          <Route
            path="/leave"
            element={
              <Protected>
                <LeavePage />
              </Protected>
            }
          />
          <Route
            path="/employees"
            element={
              <Protected>
                <EmployeesPage />
              </Protected>
            }
          />
          <Route
            path="/reports"
            element={
              <Protected>
                <ReportsPage />
              </Protected>
            }
          />
        </Routes>
      </div>
    </AuthProvider>
  );
}

export default App;
