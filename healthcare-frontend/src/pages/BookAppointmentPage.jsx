import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';

const APPOINTMENT_TYPES = ['Standard', 'Insurance', 'Emergency', 'Vip'];

function toLocalDatetimeString(date) {
  const pad = (n) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

const s = {
  wrapper: { maxWidth: 520, margin: '40px auto', padding: '0 16px' },
  field: { marginBottom: 20 },
  label: { display: 'block', marginBottom: 4, fontWeight: 600, fontSize: 14 },
  input: { width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid #ccc' },
  textarea: { width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid #ccc', minHeight: 80, resize: 'vertical' },
  select: { width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid #ccc', background: '#fff' },
  error: { color: '#dc2626', fontSize: 13, marginTop: 4 },
  submit: { width: '100%', padding: '10px 0', background: '#2563eb', color: '#fff', border: 'none', borderRadius: 6, fontSize: 16 },
  back: { display: 'block', marginBottom: 16, color: '#2563eb', cursor: 'pointer', background: 'none', border: 'none', fontSize: 14, padding: 0 },
};

export default function BookAppointmentPage() {
  const { doctorId } = useParams();
  const navigate = useNavigate();
  const { patientId } = useAuth();

  const [doctor, setDoctor] = useState(null);
  const [datetime, setDatetime] = useState('');
  const [reason, setReason] = useState('');
  const [appointmentType, setAppointmentType] = useState('Standard');
  const [errors, setErrors] = useState({});
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!patientId) {
      toast.error('Create your patient profile first');
      navigate('/create-patient');
    }
  }, [patientId, navigate]);

  useEffect(() => {
    (async () => {
      try {
        const { data } = await apiClient.get(`/Doctors/${doctorId}`);
        if (data.success) setDoctor(data.data);
        else toast.error(data.message || 'Doctor not found');
      } catch {
        toast.error('Failed to load doctor details');
      }
    })();
  }, [doctorId]);

  const minDate = toLocalDatetimeString(new Date(Date.now() + 3600000));

  const validate = () => {
    const e = {};
    if (!datetime) e.datetime = 'Date and time is required';
    else {
      const dt = new Date(datetime);
      if (dt < new Date()) e.datetime = 'Cannot be in the past';
      const mins = dt.getMinutes();
      if (mins % 30 !== 0) e.datetime = 'Time must be in 30-minute intervals (e.g. 09:00, 09:30)';
    }
    if (!reason || reason.trim().length < 10) e.reason = 'Reason must be at least 10 characters';
    if (!appointmentType) e.appointmentType = 'Appointment type is required';
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!validate()) return;
    setSubmitting(true);
    try {
      const { data } = await apiClient.post('/Appointments', {
        patientId,
        doctorId: Number(doctorId),
        scheduledTime: new Date(datetime).toISOString(),
        reason: reason.trim(),
        appointmentType,
      });
      if (data.success) {
        toast.success('Appointment booked successfully!');
        navigate('/my-appointments');
      } else {
        const msg = data.errors?.join('. ') || data.message || 'Booking failed';
        toast.error(msg);
      }
    } catch (err) {
      const serverMsg = err.response?.data?.errors?.join('. ') || err.response?.data?.message || err.message;
      toast.error(serverMsg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div style={s.wrapper}>
      <button style={s.back} onClick={() => navigate('/doctors')}>← Back to Doctors</button>
      <h1>Book Appointment</h1>

      {doctor && (
        <p style={{ marginBottom: 20, fontSize: 15, color: '#555' }}>
          Dr. {doctor.fullName} — {doctor.specialties?.join(', ')}
        </p>
      )}

      <form onSubmit={handleSubmit}>
        <div style={s.field}>
          <label style={s.label}>Date & Time</label>
          <input
            type="datetime-local"
            style={s.input}
            value={datetime}
            min={minDate}
            step="1800"
            onChange={(e) => setDatetime(e.target.value)}
          />
          {errors.datetime && <div style={s.error}>{errors.datetime}</div>}
        </div>

        <div style={s.field}>
          <label style={s.label}>Reason</label>
          <textarea
            style={s.textarea}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Briefly describe your reason (min 10 characters)..."
          />
          {errors.reason && <div style={s.error}>{errors.reason}</div>}
        </div>

        <div style={s.field}>
          <label style={s.label}>Appointment Type</label>
          <select style={s.select} value={appointmentType} onChange={(e) => setAppointmentType(e.target.value)}>
            {APPOINTMENT_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
          </select>
          {errors.appointmentType && <div style={s.error}>{errors.appointmentType}</div>}
        </div>

        <button type="submit" style={s.submit} disabled={submitting}>
          {submitting ? 'Booking...' : 'Book Appointment'}
        </button>
      </form>
    </div>
  );
}
