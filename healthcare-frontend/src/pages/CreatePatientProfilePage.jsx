import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';

const GENDERS = ['Male', 'Female', 'Other'];

const s = {
  wrapper: { maxWidth: 520, margin: '40px auto', padding: '0 16px' },
  field: { marginBottom: 16 },
  label: { display: 'block', marginBottom: 4, fontWeight: 600, fontSize: 14 },
  input: { width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid #ccc' },
  select: { width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid #ccc', background: '#fff' },
  row: { display: 'flex', gap: 12 },
  submit: { width: '100%', padding: '10px 0', marginTop: 8, background: '#2563eb', color: '#fff', border: 'none', borderRadius: 6, fontSize: 16 },
};

export default function CreatePatientProfilePage() {
  const { setPatientId } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({
    firstName: '', lastName: '', email: '',
    phoneNumber: '', dateOfBirth: '', gender: 'Male',
    street: '', city: '', state: '', postalCode: '', country: '',
  });
  const [submitting, setSubmitting] = useState(false);

  const set = (field) => (e) => setForm((prev) => ({ ...prev, [field]: e.target.value }));

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      const { data } = await apiClient.post('/Patients', {
        ...form,
        dateOfBirth: new Date(form.dateOfBirth).toISOString(),
      });
      if (data.success) {
        setPatientId(data.data);
        toast.success('Patient profile created!');
        navigate('/doctors');
      } else {
        toast.error(data.errors?.join('. ') || data.message || 'Failed to create profile');
      }
    } catch (err) {
      toast.error(err.response?.data?.errors?.join('. ') || err.response?.data?.message || 'Failed to create profile');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div style={s.wrapper}>
      <h1>Create Patient Profile</h1>
      <p style={{ color: '#666', marginBottom: 20 }}>Fill in your details to start booking appointments.</p>
      <form onSubmit={handleSubmit}>
        <div style={s.row}>
          <div style={{ flex: 1, ...s.field }}>
            <label style={s.label}>First Name</label>
            <input style={s.input} value={form.firstName} onChange={set('firstName')} required />
          </div>
          <div style={{ flex: 1, ...s.field }}>
            <label style={s.label}>Last Name</label>
            <input style={s.input} value={form.lastName} onChange={set('lastName')} required />
          </div>
        </div>
        <div style={s.field}>
          <label style={s.label}>Email</label>
          <input style={s.input} type="email" value={form.email} onChange={set('email')} required />
        </div>
        <div style={s.field}>
          <label style={s.label}>Phone Number</label>
          <input style={s.input} value={form.phoneNumber} onChange={set('phoneNumber')} required />
        </div>
        <div style={s.row}>
          <div style={{ flex: 1, ...s.field }}>
            <label style={s.label}>Date of Birth</label>
            <input style={s.input} type="date" value={form.dateOfBirth} onChange={set('dateOfBirth')} required />
          </div>
          <div style={{ flex: 1, ...s.field }}>
            <label style={s.label}>Gender</label>
            <select style={s.select} value={form.gender} onChange={set('gender')}>
              {GENDERS.map((g) => <option key={g}>{g}</option>)}
            </select>
          </div>
        </div>
        <div style={s.field}>
          <label style={s.label}>Street</label>
          <input style={s.input} value={form.street} onChange={set('street')} />
        </div>
        <div style={s.row}>
          <div style={{ flex: 1, ...s.field }}>
            <label style={s.label}>City</label>
            <input style={s.input} value={form.city} onChange={set('city')} />
          </div>
          <div style={{ flex: 1, ...s.field }}>
            <label style={s.label}>State</label>
            <input style={s.input} value={form.state} onChange={set('state')} />
          </div>
        </div>
        <div style={s.row}>
          <div style={{ flex: 1, ...s.field }}>
            <label style={s.label}>Postal Code</label>
            <input style={s.input} value={form.postalCode} onChange={set('postalCode')} />
          </div>
          <div style={{ flex: 1, ...s.field }}>
            <label style={s.label}>Country</label>
            <input style={s.input} value={form.country} onChange={set('country')} />
          </div>
        </div>
        <button type="submit" style={s.submit} disabled={submitting}>
          {submitting ? 'Creating...' : 'Create Profile'}
        </button>
      </form>
    </div>
  );
}
