import { useNavigate } from 'react-router-dom';

const s = {
  wrapper: { textAlign: 'center', padding: '80px 16px' },
  code: { fontSize: 72, fontWeight: 700, color: '#dc2626', margin: '0 0 8px' },
  msg: { fontSize: 18, color: '#666', margin: '0 0 24px' },
  btn: { padding: '10px 24px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: 6, fontSize: 15 },
};

export default function ForbiddenPage() {
  const navigate = useNavigate();
  return (
    <div style={s.wrapper}>
      <p style={s.code}>403</p>
      <p style={s.msg}>You do not have permission to access this page.</p>
      <button style={s.btn} onClick={() => navigate('/dashboard')}>Go to Dashboard</button>
    </div>
  );
}
