import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export default function DashboardPage() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <div style={{ maxWidth: 600, margin: '80px auto', padding: '0 16px' }}>
      <h1>Dashboard</h1>
      <p>Welcome, <strong>{user?.username}</strong>!</p>
      <p>Role: {user?.role}</p>
      <button onClick={handleLogout} style={{ padding: '8px 24px', marginTop: 16 }}>
        Logout
      </button>
    </div>
  );
}
