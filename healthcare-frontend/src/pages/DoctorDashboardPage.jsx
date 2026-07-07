import { useState, useEffect, useMemo } from 'react';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';
import { STATUS_COLORS } from '../theme';
const TABS = ['All', 'Scheduled', 'Confirmed', 'Completed', 'Cancelled', 'NoShow'];

const s = {
  wrapper: { maxWidth: 900, margin: '24px auto', padding: '0 16px' },
  tabs: { display: 'flex', gap: 4, marginBottom: 20, flexWrap: 'wrap' },
  tab: (active) => ({
    padding: '8px 16px', fontSize: 13, borderRadius: 6, border: '1px solid #ddd',
    background: active ? '#2563eb' : '#f9f9f9', color: active ? '#fff' : '#333',
    cursor: 'pointer', fontWeight: active ? 600 : 400,
  }),
  count: { marginLeft: 6, fontSize: 11, opacity: 0.7 },
  card: { border: '1px solid #ddd', borderRadius: 8, padding: 16, marginBottom: 12, background: '#fff' },
  header: { display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 8 },
  ref: { fontWeight: 700, fontSize: 15 },
  badge: (status) => ({
    display: 'inline-block', fontSize: 12, padding: '3px 10px', borderRadius: 12, fontWeight: 600,
    background: (STATUS_COLORS[status] || { bg: '#f3f4f6' }).bg,
    color: (STATUS_COLORS[status] || { color: '#333' }).color,
  }),
  meta: { fontSize: 13, color: '#666', marginBottom: 4 },
  actions: { marginTop: 10, display: 'flex', gap: 8, flexWrap: 'wrap' },
  actBtn: (bg, color) => ({ padding: '6px 14px', fontSize: 13, background: bg, color: color, border: '1px solid', borderRadius: 6, cursor: 'pointer' }),
  lookup: { maxWidth: 400, margin: '80px auto', textAlign: 'center' },
  lookupInput: { padding: '8px 12px', width: '100%', borderRadius: 6, border: '1px solid #ccc', marginBottom: 12 },
  lookupResult: { padding: 12, border: '1px solid #ddd', borderRadius: 8, cursor: 'pointer', background: '#fff', textAlign: 'left' },
  loading: { textAlign: 'center', padding: 40, color: '#888' },
  empty: { textAlign: 'center', padding: 40, color: '#888' },
  overlay: { position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 },
  modal: { background: '#fff', borderRadius: 8, padding: 24, width: 440, maxWidth: '90vw' },
  modalLabel: { display: 'block', fontWeight: 600, fontSize: 14, marginBottom: 4 },
  modalTextarea: { width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid #ccc', minHeight: 80, resize: 'vertical' },
  modalCheck: { display: 'flex', alignItems: 'center', gap: 8, marginBottom: 12 },
  modalActions: { display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 16 },
  modalCancel: { padding: '8px 16px', border: '1px solid #ccc', borderRadius: 6, background: '#fff' },
  modalConfirm: (bg) => ({ padding: '8px 16px', background: bg, color: '#fff', border: 'none', borderRadius: 6 }),
};

export default function DoctorDashboardPage() {
  const { doctorId, setDoctorId } = useAuth();
  const [allAppts, setAllAppts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState('All');

  // doctor lookup state
  const [searchEmail, setSearchEmail] = useState('');
  const [searching, setSearching] = useState(false);
  const [searchResults, setSearchResults] = useState([]);

  // modals
  const [modal, setModal] = useState(null);
  const [notes, setNotes] = useState('');
  const [notesErr, setNotesErr] = useState('');
  const [overridePayment, setOverridePayment] = useState(false);
  const [overrideReason, setOverrideReason] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const fetchAppts = async () => {
    if (!doctorId) return;
    setLoading(true);
    try {
      const { data } = await apiClient.get(`/Appointments/doctor/${doctorId}`, { params: { pageSize: 100 } });
      if (data.success) setAllAppts(data.data.items);
      else toast.error(data.message || 'Failed to load appointments');
    } catch { toast.error('Failed to load appointments'); }
    finally { setLoading(false); }
  };

  useEffect(() => { fetchAppts(); }, [doctorId]);

  const filtered = useMemo(() => {
    const filterByStatus = (status) => allAppts.filter((a) => a.status === status);
    return {
      All: allAppts, Scheduled: filterByStatus('Scheduled'),
      Confirmed: filterByStatus('Confirmed'), Completed: filterByStatus('Completed'),
      Cancelled: filterByStatus('Cancelled'), NoShow: filterByStatus('NoShow'),
    };
  }, [allAppts]);

  const searchDoctor = async () => {
    setSearching(true);
    try {
      const { data } = await apiClient.get('/Doctors', { params: { pageSize: 200 } });
      if (data.success) {
        const matches = data.data.items.filter((d) =>
          d.email.toLowerCase().includes(searchEmail.toLowerCase()),
        );
        setSearchResults(matches);
        if (matches.length === 0) toast.error('No doctors found with that email');
      }
    } catch { toast.error('Search failed'); }
    finally { setSearching(false); }
  };

  const selectDoctor = (doc) => {
    setDoctorId(doc.id);
    setSearchEmail('');
    setSearchResults([]);
  };

  const doConfirm = async (appt) => {
    setSubmitting(true);
    try {
      const { data } = await apiClient.put(`/Appointments/${appt.id}/confirm`, {
        appointmentId: appt.id,
        overridePaymentRequirement: overridePayment,
        overrideReason: overridePayment ? overrideReason : null,
      });
      if (data.success) { toast.success('Appointment confirmed'); setModal(null); fetchAppts(); }
      else toast.error(data.errors?.join('. ') || data.message || 'Confirm failed');
    } catch (err) { toast.error(err.response?.data?.errors?.join('. ') || 'Confirm failed'); }
    finally { setSubmitting(false); }
  };

  const doComplete = async (appt) => {
    if (!notes || notes.trim().length < 20) { setNotesErr('Doctor notes must be at least 20 characters'); return; }
    setSubmitting(true);
    try {
      const { data } = await apiClient.put(`/Appointments/${appt.id}/complete`, {
        appointmentId: appt.id, doctorNotes: notes.trim(),
      });
      if (data.success) { toast.success('Appointment completed'); setModal(null); setNotes(''); fetchAppts(); }
      else toast.error(data.errors?.join('. ') || data.message || 'Complete failed');
    } catch (err) { toast.error(err.response?.data?.errors?.join('. ') || 'Complete failed'); }
    finally { setSubmitting(false); }
  };

  const doNoShow = async (appt) => {
    setSubmitting(true);
    try {
      const { data } = await apiClient.put(`/Appointments/${appt.id}/mark-no-show`);
      if (data.success) { toast.success('Marked as No-Show'); fetchAppts(); }
      else toast.error(data.message || 'Failed');
    } catch (err) { toast.error(err.response?.data?.message || 'Failed'); }
    finally { setSubmitting(false); }
  };

  const openConfirm = (appt) => {
    setOverridePayment(false);
    setOverrideReason('');
    setModal({ type: 'confirm', appt });
  };
  const openComplete = (appt) => {
    setNotes('');
    setNotesErr('');
    setModal({ type: 'complete', appt });
  };

  if (!doctorId) {
    return (
      <div style={s.lookup}>
        <h2>Doctor Lookup</h2>
        <p style={{ color: '#666', fontSize: 14, marginBottom: 16 }}>Search for your doctor profile by email.</p>
        <input style={s.lookupInput} placeholder="Your email address..." value={searchEmail} onChange={(e) => setSearchEmail(e.target.value)} />
        <button style={{ padding: '8px 24px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: 6 }} disabled={searching} onClick={searchDoctor}>{searching ? 'Searching...' : 'Search'}</button>
        {searchResults.map((doc) => (
          <div key={doc.id} style={{ ...s.lookupResult, marginTop: 12 }} onClick={() => selectDoctor(doc)}>
            <strong>Dr. {doc.fullName}</strong>
            <div style={{ fontSize: 13, color: '#666' }}>{doc.email} — {doc.specialties.join(', ')}</div>
          </div>
        ))}
      </div>
    );
  }

  if (loading) return <div style={s.wrapper}><div style={s.loading}>Loading appointments...</div></div>;

  const currentList = filtered[activeTab] || allAppts;

  return (
    <div style={s.wrapper}>
      <h1>Doctor Dashboard</h1>
      <div style={s.tabs}>
        {TABS.map((t) => (
          <button key={t} style={s.tab(activeTab === t)} onClick={() => setActiveTab(t)}>
            {t}
            <span style={s.count}>({filtered[t]?.length || 0})</span>
          </button>
        ))}
      </div>

      {currentList.length === 0 ? (
        <div style={s.empty}>No appointments in this status.</div>
      ) : (
        currentList.map((appt) => (
          <div key={appt.id} style={s.card}>
            <div style={s.header}>
              <div>
                <span style={s.ref}>{appt.referenceCode}</span>
                <span style={{ marginLeft: 10, ...s.badge(appt.status) }}>{appt.status}</span>
              </div>
              <div style={{ fontSize: 13, color: '#888' }}>{appt.scheduledDate}</div>
            </div>
            <div style={s.meta}>Patient: {appt.patient?.fullName} ({appt.patient?.email})</div>
            <div style={s.meta}>Time: {appt.scheduledTimeFormatted} — Type: {appt.reason}</div>
            <div style={{ fontSize: 13, color: '#333', marginTop: 4 }}>{appt.reason}</div>
            {appt.doctorNotes && <div style={{ fontSize: 13, color: '#3730a3', marginTop: 4 }}>Notes: {appt.doctorNotes}</div>}
            {appt.cancellationReason && <div style={{ fontSize: 13, color: '#991b1b', marginTop: 4 }}>Cancel reason: {appt.cancellationReason}</div>}

            <div style={s.actions}>
              {appt.status === 'Scheduled' && (
                <>
                  <button style={s.actBtn('#d1fae5', '#065f46')} onClick={() => openConfirm(appt)}>Confirm</button>
                  <button style={s.actBtn('#fee2e2', '#991b1b')} onClick={() => openComplete(appt)}>Complete</button>
                  <button style={s.actBtn('#f3e8ff', '#6b21a8')} onClick={() => doNoShow(appt)}>No-Show</button>
                </>
              )}
              {appt.status === 'Confirmed' && (
                <button style={s.actBtn('#e0e7ff', '#3730a3')} onClick={() => openComplete(appt)}>Complete</button>
              )}
            </div>
          </div>
        ))
      )}

      {/* Confirm Modal */}
      {modal?.type === 'confirm' && (
        <div style={s.overlay} onClick={() => setModal(null)}>
          <div style={s.modal} onClick={(e) => e.stopPropagation()}>
            <h3 style={{ margin: '0 0 12px' }}>Confirm Appointment</h3>
            <p style={{ fontSize: 14, color: '#555', marginBottom: 16 }}>{modal.appt.referenceCode} — {modal.appt.patient?.fullName}</p>
            <label style={s.modalCheck}>
              <input type="checkbox" checked={overridePayment} onChange={(e) => setOverridePayment(e.target.checked)} />
              Override payment requirement
            </label>
            {overridePayment && (
              <div style={{ marginBottom: 12 }}>
                <label style={s.modalLabel}>Override Reason</label>
                <input style={{ width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid #ccc' }} value={overrideReason} onChange={(e) => setOverrideReason(e.target.value)} />
              </div>
            )}
            <div style={s.modalActions}>
              <button style={s.modalCancel} onClick={() => setModal(null)}>Cancel</button>
              <button style={s.modalConfirm('#059669')} disabled={submitting} onClick={() => doConfirm(modal.appt)}>{submitting ? '...' : 'Confirm'}</button>
            </div>
          </div>
        </div>
      )}

      {/* Complete Modal */}
      {modal?.type === 'complete' && (
        <div style={s.overlay} onClick={() => setModal(null)}>
          <div style={s.modal} onClick={(e) => e.stopPropagation()}>
            <h3 style={{ margin: '0 0 12px' }}>Complete Appointment</h3>
            <p style={{ fontSize: 14, color: '#555', marginBottom: 16 }}>{modal.appt.referenceCode} — {modal.appt.patient?.fullName}</p>
            <label style={s.modalLabel}>Doctor Notes (min 20 characters)</label>
            <textarea style={s.modalTextarea} value={notes} onChange={(e) => { setNotes(e.target.value); setNotesErr(''); }} placeholder="Enter your clinical notes..." />
            {notesErr && <div style={{ color: '#dc2626', fontSize: 13, marginTop: 4 }}>{notesErr}</div>}
            <div style={s.modalActions}>
              <button style={s.modalCancel} onClick={() => { setModal(null); setNotes(''); }}>Cancel</button>
              <button style={s.modalConfirm('#3730a3')} disabled={submitting} onClick={() => doComplete(modal.appt)}>{submitting ? '...' : 'Complete'}</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
