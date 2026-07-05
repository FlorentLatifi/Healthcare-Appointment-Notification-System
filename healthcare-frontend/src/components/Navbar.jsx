import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

const s = {
  bar: {
    display: 'flex', alignItems: 'center', gap: 24, padding: '12px 24px',
    background: '#1e293b', color: '#fff', position: 'sticky', top: 0, zIndex: 100,
  },
  brand: { fontWeight: 700, fontSize: 16, cursor: 'pointer', marginRight: 'auto' },
  link: (active) => ({
    background: 'none', border: 'none', color: active ? '#93c5fd' : '#cbd5e1',
    cursor: 'pointer', fontSize: 14, padding: '4px 0', borderBottom: active ? '2px solid #93c5fd' : '2px solid transparent',
  }),
  logout: { background: '#475569', border: 'none', color: '#fff', padding: '6px 16px', borderRadius: 4, cursor: 'pointer', fontSize: 13 },
};

export default function Navbar() {
  const { user, logout, patientId, doctorId } = useAuth();
  const navigate = useNavigate();
  const loc = useLocation();

  const links = [
    { label: 'Dashboard', path: '/dashboard', show: true },
  ];

  if (user?.role === 'Patient') {
    links.push(
      { label: 'Doctors', path: '/doctors', show: true },
      { label: 'My Appointments', path: '/my-appointments', show: !!patientId },
    );
  }
  if (user?.role === 'Doctor') {
    links.push({ label: 'Doctor Dashboard', path: '/doctor-dashboard', show: true });
  }
  if (user?.role === 'Admin') {
    links.push({ label: 'Admin', path: '/admin', show: true });
  }

  return (
    <nav style={s.bar}>
      <div style={s.brand} onClick={() => navigate('/dashboard')}>Healthcare</div>
      {links.filter((l) => l.show).map((l) => (
        <button key={l.path} style={s.link(loc.pathname === l.path)} onClick={() => navigate(l.path)}>
          {l.label}
        </button>
      ))}
      <button style={s.logout} onClick={() => { logout(); navigate('/login', { replace: true }); }}>
        Logout
      </button>
    </nav>
  );
}
