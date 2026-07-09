import { useState, useEffect, useMemo } from 'react';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';
import { Button, Card, Badge, Spinner, EmptyState, Modal, Input, Textarea, PageHeader } from '../components/ui';
import { Calendar, Clock, User, FileText, CheckCircle, XCircle } from 'lucide-react';

const TABS = ['All', 'Scheduled', 'Confirmed', 'Completed', 'Cancelled', 'NoShow'];

export default function DoctorDashboardPage() {
  const { doctorId, setDoctorId } = useAuth();
  const [allAppts, setAllAppts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState('All');

  const [searchEmail, setSearchEmail] = useState('');
  const [searching, setSearching] = useState(false);
  const [searchResults, setSearchResults] = useState([]);

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
      <div className="max-w-md mx-auto px-4 py-16 text-center">
        <h2 className="text-xl font-semibold text-text tracking-tight mb-2">Doctor Lookup</h2>
        <p className="text-sm text-text-muted mb-6">Search for your doctor profile by email.</p>
        <div className="bg-white rounded-xl shadow-card p-6 border border-border-light">
          <Input
            placeholder="Your email address..."
            value={searchEmail}
            onChange={(e) => setSearchEmail(e.target.value)}
          />
          <Button loading={searching} className="w-full" onClick={searchDoctor}>Search</Button>
          {searchResults.map((doc) => (
            <Card
              key={doc.id}
              hover
              className="mt-3 text-left"
              onClick={() => selectDoctor(doc)}
            >
              <p className="text-sm font-medium text-text">Dr. {doc.fullName}</p>
              <p className="text-xs text-text-muted mt-0.5">{doc.email} — {doc.specialties.join(', ')}</p>
            </Card>
          ))}
        </div>
      </div>
    );
  }

  if (loading) return <div className="max-w-4xl mx-auto px-4 py-12"><Spinner /></div>;

  const currentList = filtered[activeTab] || allAppts;

  return (
    <div className="max-w-4xl mx-auto px-4 py-12">
      <PageHeader title="Doctor Dashboard" />

      <div className="flex flex-wrap gap-1 mb-6">
        {TABS.map((t) => (
          <Button
            key={t}
            variant={activeTab === t ? 'primary' : 'secondary'}
            size="sm"
            onClick={() => setActiveTab(t)}
          >
            {t}
            <span className="ml-1 opacity-60">({filtered[t]?.length || 0})</span>
          </Button>
        ))}
      </div>

      {currentList.length === 0 ? (
        <EmptyState message="No appointments in this status." />
      ) : (
        <div className="space-y-3">
          {currentList.map((appt) => (
            <Card key={appt.id}>
              <div className="flex items-start justify-between mb-3">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-semibold text-text">{appt.referenceCode}</span>
                  <Badge status={appt.status} />
                </div>
                <span className="text-xs text-text-muted">{appt.scheduledDate}</span>
              </div>
              <div className="flex flex-wrap items-center gap-3 text-xs text-text-muted mb-1">
                <span className="inline-flex items-center gap-1"><User size={12} />{appt.patient?.fullName}</span>
                <span className="inline-flex items-center gap-1"><Calendar size={12} />{appt.scheduledTimeFormatted}</span>
                <span className="inline-flex items-center gap-1"><FileText size={12} />{appt.reason}</span>
              </div>
              <p className="text-sm text-text mt-1">{appt.reason}</p>
              {appt.doctorNotes && <p className="text-xs text-status-completed-text mt-2 inline-flex items-center gap-1"><FileText size={12} />Notes: {appt.doctorNotes}</p>}
              {appt.cancellationReason && <p className="text-xs text-status-cancelled-text mt-2 inline-flex items-center gap-1"><XCircle size={12} />Cancel reason: {appt.cancellationReason}</p>}

              <div className="flex flex-wrap gap-2 mt-3 pt-3 border-t border-border-light">
                {appt.status === 'Scheduled' && (
                  <>
                    <Button variant="primary" size="sm" leftIcon={<CheckCircle size={14} />} onClick={() => openConfirm(appt)}>Confirm</Button>
                    <Button variant="secondary" size="sm" onClick={() => openComplete(appt)}>Complete</Button>
                    <Button variant="ghost" size="sm" onClick={() => doNoShow(appt)}>No-Show</Button>
                  </>
                )}
                {appt.status === 'Confirmed' && (
                  <Button variant="primary" size="sm" onClick={() => openComplete(appt)}>Complete</Button>
                )}
              </div>
            </Card>
          ))}
        </div>
      )}

      <Modal
        open={modal?.type === 'confirm'}
        onClose={() => setModal(null)}
        title="Confirm Appointment"
        footer={
          <>
            <Button variant="secondary" onClick={() => setModal(null)}>Cancel</Button>
            <Button loading={submitting} onClick={() => doConfirm(modal.appt)}>Confirm</Button>
          </>
        }
      >
        <p className="text-sm text-text-muted mb-4">{modal?.appt.referenceCode} — {modal?.appt.patient?.fullName}</p>
        <label className="flex items-center gap-2 text-sm text-text mb-4">
          <input
            type="checkbox"
            checked={overridePayment}
            onChange={(e) => setOverridePayment(e.target.checked)}
            className="rounded border-border text-primary focus:ring-primary"
          />
          Override payment requirement
        </label>
        {overridePayment && (
          <Input label="Override Reason" value={overrideReason} onChange={(e) => setOverrideReason(e.target.value)} />
        )}
      </Modal>

      <Modal
        open={modal?.type === 'complete'}
        onClose={() => { setModal(null); setNotes(''); }}
        title="Complete Appointment"
        footer={
          <>
            <Button variant="secondary" onClick={() => { setModal(null); setNotes(''); }}>Cancel</Button>
            <Button loading={submitting} onClick={() => doComplete(modal.appt)}>Complete</Button>
          </>
        }
      >
        <p className="text-sm text-text-muted mb-4">{modal?.appt.referenceCode} — {modal?.appt.patient?.fullName}</p>
        <Textarea
          label="Doctor Notes (min 20 characters)"
          value={notes}
          onChange={(e) => { setNotes(e.target.value); setNotesErr(''); }}
          placeholder="Enter your clinical notes..."
          error={notesErr}
        />
      </Modal>
    </div>
  );
}
