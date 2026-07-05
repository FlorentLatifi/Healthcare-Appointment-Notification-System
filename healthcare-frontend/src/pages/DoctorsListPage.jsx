import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';

const s = {
  wrapper: { maxWidth: 900, margin: '40px auto', padding: '0 16px' },
  search: { padding: '8px 12px', width: 280, borderRadius: 6, border: '1px solid #ccc', marginBottom: 24 },
  grid: { display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', gap: 16 },
  card: { border: '1px solid #ddd', borderRadius: 8, padding: 16, background: '#fff' },
  name: { margin: '0 0 4px', fontSize: 18 },
  specialty: { fontSize: 13, color: '#555', marginBottom: 6 },
  fee: { fontSize: 14, fontWeight: 600, color: '#059669', marginBottom: 8 },
  badge: {
    display: 'inline-block', fontSize: 12, padding: '2px 8px', borderRadius: 12,
    background: '#dbeafe', color: '#1e40af',
  },
  bookBtn: { width: '100%', padding: '8px 0', marginTop: 12, background: '#2563eb', color: '#fff', border: 'none', borderRadius: 6 },
  loading: { textAlign: 'center', padding: 40, color: '#888' },
  empty: { textAlign: 'center', padding: 40, color: '#888', fontSize: 16 },
};

export default function DoctorsListPage() {
  const { patientId, user } = useAuth();
  const navigate = useNavigate();
  const [doctors, setDoctors] = useState([]);
  const [filter, setFilter] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        const { data } = await apiClient.get('/Doctors/accepting-patients', { params: { pageSize: 100 } });
        if (data.success) {
          setDoctors(data.data.items);
        } else {
          toast.error(data.message || 'Failed to load doctors');
        }
      } catch {
        toast.error('Failed to load doctors');
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const specialties = [...new Set(doctors.flatMap((d) => d.specialties))].sort();
  const filtered = filter
    ? doctors.filter((d) =>
        d.specialties.some((s) => s.toLowerCase().includes(filter.toLowerCase())),
      )
    : doctors;

  if (loading) return <div style={s.wrapper}><div style={s.loading}>Loading doctors...</div></div>;

  return (
    <div style={s.wrapper}>
      <h1>Available Doctors</h1>

      <input
        style={s.search}
        placeholder="Filter by specialty..."
        value={filter}
        onChange={(e) => setFilter(e.target.value)}
      />

      {specialties.length > 0 && (
        <div style={{ marginBottom: 20, display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          {specialties.map((sp) => (
            <button
              key={sp}
              style={{
                fontSize: 12, padding: '4px 12px', borderRadius: 16, border: '1px solid #ccc',
                background: filter === sp ? '#2563eb' : '#f3f4f6',
                color: filter === sp ? '#fff' : '#333', cursor: 'pointer',
              }}
              onClick={() => setFilter(filter === sp ? '' : sp)}
            >
              {sp}
            </button>
          ))}
        </div>
      )}

      {filtered.length === 0 ? (
        <div style={s.empty}>No doctors found matching this specialty.</div>
      ) : (
        <div style={s.grid}>
          {filtered.map((doc) => (
            <div key={doc.id} style={s.card}>
              <h3 style={s.name}>Dr. {doc.fullName}</h3>
              <div style={s.specialty}>{doc.specialties.join(', ')}</div>
              <div style={s.fee}>{doc.consultationFeeCurrency} {doc.consultationFeeAmount}</div>
              <span style={s.badge}>{doc.yearsOfExperience} yr{doc.yearsOfExperience !== 1 ? 's' : ''}</span>
              <button
                style={s.bookBtn}
                onClick={() => {
                  if (!patientId && user?.role === 'Patient') {
                    toast.error('Create your patient profile first');
                    navigate('/create-patient');
                    return;
                  }
                  navigate(`/book-appointment/${doc.id}`);
                }}
              >
                Book Appointment
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
