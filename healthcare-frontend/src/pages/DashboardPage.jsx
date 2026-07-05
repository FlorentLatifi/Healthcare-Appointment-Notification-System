import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useAuth } from '../context/AuthContext';

const s = {
  wrapper: { maxWidth: 600, margin: '80px auto', padding: '0 16px' },
  card: { border: '1px solid #ddd', borderRadius: 8, padding: 16, marginBottom: 12, background: '#fff', cursor: 'pointer' },
  cardTitle: { margin: '0 0 4px', fontSize: 16 },
  cardDesc: { margin: 0, fontSize: 13, color: '#666' },
  logout: { padding: '8px 24px', marginTop: 24, background: '#fee2e2', color: '#991b1b', border: '1px solid #fecaca', borderRadius: 6 },
};

export default function DashboardPage() {
  const { user, logout, patientId } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  const patientActions = [
    { title: 'Browse Doctors', desc: 'Find available doctors and book appointments', path: '/doctors' },
    { title: 'My Appointments', desc: 'View and manage your appointments', path: '/my-appointments' },
  ];

  if (!patientId && user?.role === 'Patient') {
    patientActions.unshift({
      title: 'Create Patient Profile',
      desc: 'Set up your profile to start booking',
      path: '/create-patient',
    });
  }

  return (
    <div style={s.wrapper}>
      <h1>Dashboard</h1>
      <p>Welcome, <strong>{user?.username}</strong>!</p>
      <p>Role: {user?.role}</p>

      {user?.role === 'Patient' && (
        <div style={{ marginTop: 24 }}>
          <h3 style={{ marginBottom: 12 }}>Quick Actions</h3>
          {patientActions.map((a) => (
            <div
              key={a.path}
              style={s.card}
              onClick={() => {
                if ((a.path === '/my-appointments' || a.path === '/doctors') && !patientId) {
                  toast.error('Create your patient profile first');
                  navigate('/create-patient');
                  return;
                }
                navigate(a.path);
              }}
            >
              <h4 style={s.cardTitle}>{a.title}</h4>
              <p style={s.cardDesc}>{a.desc}</p>
            </div>
          ))}
        </div>
      )}

      <button onClick={handleLogout} style={s.logout}>
        Logout
      </button>
    </div>
  );
}
