import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';

const STATUS_COLORS = {
  Scheduled: { bg: '#dbeafe', color: '#1e40af' },
  Confirmed: { bg: '#d1fae5', color: '#065f46' },
  InProgress: { bg: '#fef3c7', color: '#92400e' },
  Completed: { bg: '#e0e7ff', color: '#3730a3' },
  Cancelled: { bg: '#fee2e2', color: '#991b1b' },
  NoShow: { bg: '#f3e8ff', color: '#6b21a8' },
};

const s = {
  wrapper: { maxWidth: 800, margin: '40px auto', padding: '0 16px' },
  card: { border: '1px solid #ddd', borderRadius: 8, padding: 16, marginBottom: 12, background: '#fff' },
  header: { display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 8 },
  ref: { fontWeight: 700, fontSize: 15 },
  badge: (status) => ({
    display: 'inline-block', fontSize: 12, padding: '3px 10px', borderRadius: 12,
    fontWeight: 600,
    background: (STATUS_COLORS[status] || { bg: '#f3f4f6' }).bg,
    color: (STATUS_COLORS[status] || { color: '#333' }).color,
  }),
  meta: { fontSize: 13, color: '#666', marginBottom: 4 },
  reason: { fontSize: 14, color: '#333', marginTop: 4 },
  actions: { marginTop: 10 },
  cancelBtn: { padding: '6px 16px', fontSize: 13, background: '#fee2e2', color: '#991b1b', border: '1px solid #fecaca', borderRadius: 6 },
  loading: { textAlign: 'center', padding: 40, color: '#888' },
  empty: { textAlign: 'center', padding: 40, color: '#888', fontSize: 16 },
  // modal
  overlay: { position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 },
  modal: { background: '#fff', borderRadius: 8, padding: 24, width: 400, maxWidth: '90vw' },
  modalTitle: { margin: '0 0 16px' },
  modalTextarea: { width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid #ccc', minHeight: 80, resize: 'vertical' },
  modalActions: { display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 16 },
  modalCancel: { padding: '8px 16px', border: '1px solid #ccc', borderRadius: 6, background: '#fff' },
  modalConfirm: { padding: '8px 16px', background: '#dc2626', color: '#fff', border: 'none', borderRadius: 6 },
};

export default function MyAppointmentsPage() {
  const { patientId } = useAuth();
  const navigate = useNavigate();
  const [appointments, setAppointments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [cancelling, setCancelling] = useState(null);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelError, setCancelError] = useState('');

  useEffect(() => {
    if (!patientId) {
      toast.error('Create your patient profile first');
      navigate('/create-patient');
      return;
    }
    fetchAppointments();
  }, [patientId]);

  const fetchAppointments = async () => {
    setLoading(true);
    try {
      const { data } = await apiClient.get(`/Appointments/patient/${patientId}`, { params: { pageSize: 50 } });
      if (data.success) setAppointments(data.data.items);
      else toast.error(data.message || 'Failed to load appointments');
    } catch {
      toast.error('Failed to load appointments');
    } finally {
      setLoading(false);
    }
  };

  const openCancelModal = (appt) => {
    setCancelling(appt);
    setCancelReason('');
    setCancelError('');
  };

  const confirmCancel = async () => {
    if (!cancelReason || cancelReason.trim().length < 10) {
      setCancelError('Cancellation reason must be at least 10 characters');
      return;
    }
    try {
      const { data } = await apiClient.put(`/Appointments/${cancelling.id}/cancel`, {
        appointmentId: cancelling.id,
        cancellationReason: cancelReason.trim(),
      });
      if (data.success) {
        toast.success('Appointment cancelled');
        setCancelling(null);
        fetchAppointments();
      } else {
        toast.error(data.errors?.join('. ') || data.message || 'Cancellation failed');
      }
    } catch (err) {
      toast.error(err.response?.data?.errors?.join('. ') || err.response?.data?.message || 'Cancellation failed');
    }
  };

  if (loading) return <div style={s.wrapper}><div style={s.loading}>Loading appointments...</div></div>;

  return (
    <div style={s.wrapper}>
      <h1>My Appointments</h1>

      {appointments.length === 0 ? (
        <div style={s.empty}>
          <p>No appointments found.</p>
          <button onClick={() => navigate('/doctors')} style={{ marginTop: 8, padding: '8px 20px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: 6 }}>
            Browse Doctors
          </button>
        </div>
      ) : (
        appointments.map((appt) => (
          <div key={appt.id} style={s.card}>
            <div style={s.header}>
              <div>
                <span style={s.ref}>{appt.referenceCode}</span>
                <span style={{ marginLeft: 10, ...s.badge(appt.status) }}>{appt.status}</span>
              </div>
              <div style={{ fontSize: 13, color: '#888' }}>{appt.scheduledDate}</div>
            </div>
            <div style={s.meta}>
              Dr. {appt.doctor?.fullName} — {appt.scheduledTimeFormatted}
            </div>
            <div style={s.meta}>Type: {appt.reason}</div>
            <div style={s.reason}>{appt.reason}</div>
            {appt.cancellationReason && (
              <div style={{ ...s.reason, color: '#991b1b', fontSize: 13, marginTop: 4 }}>
                Cancellation reason: {appt.cancellationReason}
              </div>
            )}
            {appt.status === 'Scheduled' && (
              <div style={s.actions}>
                <button style={s.cancelBtn} onClick={() => openCancelModal(appt)}>Cancel</button>
              </div>
            )}
          </div>
        ))
      )}

      {/* Cancel Modal */}
      {cancelling && (
        <div style={s.overlay} onClick={() => setCancelling(null)}>
          <div style={s.modal} onClick={(e) => e.stopPropagation()}>
            <h3 style={s.modalTitle}>Cancel Appointment</h3>
            <p style={{ fontSize: 14, color: '#666', marginBottom: 12 }}>
              {cancelling.referenceCode} — Dr. {cancelling.doctor?.fullName}
            </p>
            <textarea
              style={s.modalTextarea}
              placeholder="Reason for cancellation (min 10 characters)..."
              value={cancelReason}
              onChange={(e) => { setCancelReason(e.target.value); setCancelError(''); }}
            />
            {cancelError && <div style={{ color: '#dc2626', fontSize: 13, marginTop: 4 }}>{cancelError}</div>}
            <div style={s.modalActions}>
              <button style={s.modalCancel} onClick={() => setCancelling(null)}>Keep</button>
              <button style={s.modalConfirm} onClick={confirmCancel}>Confirm Cancel</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
