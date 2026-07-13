import { useState, useEffect, useMemo } from 'react';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';
import { Button, Card, Badge, Spinner, EmptyState, Modal, Input, Textarea, Select, PageHeader } from '../components/ui';
import { Calendar, User, FileText, CheckCircle, XCircle } from 'lucide-react';
import { APPOINTMENT_STATUS } from '../constants/appointmentStatus';

const TABS = ['All', APPOINTMENT_STATUS.PENDING, APPOINTMENT_STATUS.CONFIRMED, APPOINTMENT_STATUS.COMPLETED, APPOINTMENT_STATUS.CANCELLED, APPOINTMENT_STATUS.NO_SHOW];

const SPECIALTIES = [
  'GeneralPractice', 'Cardiology', 'Dermatology', 'Orthopedics', 'Pediatrics',
  'Neurology', 'Psychiatry', 'Gynecology', 'Ophthalmology', 'Otorhinolaryngology',
  'Oncology', 'Surgery',
];

const emptyDoctorForm = {
  firstName: '',
  lastName: '',
  email: '',
  phoneNumber: '',
  licenseNumber: '',
  specialty: 'GeneralPractice',
  consultationFeeAmount: '50',
  consultationFeeCurrency: 'USD',
  yearsOfExperience: '5',
};

export default function DoctorDashboardPage() {
  // doctorId comes only from JWT claims (login / refreshSession) — never client-side spoofing.
  const { doctorId, refreshSession } = useAuth();
  const [allAppts, setAllAppts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState('All');

  const [doctorForm, setDoctorForm] = useState(emptyDoctorForm);
  const [creatingProfile, setCreatingProfile] = useState(false);

  const [modal, setModal] = useState(null);
  const [notes, setNotes] = useState('');
  const [notesErr, setNotesErr] = useState('');
  const [overridePayment, setOverridePayment] = useState(false);
  const [overrideReason, setOverrideReason] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const setDoctorField = (field) => (e) => setDoctorForm((prev) => ({ ...prev, [field]: e.target.value }));

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
      All: allAppts, [APPOINTMENT_STATUS.PENDING]: filterByStatus(APPOINTMENT_STATUS.PENDING),
      [APPOINTMENT_STATUS.CONFIRMED]: filterByStatus(APPOINTMENT_STATUS.CONFIRMED), [APPOINTMENT_STATUS.COMPLETED]: filterByStatus(APPOINTMENT_STATUS.COMPLETED),
      [APPOINTMENT_STATUS.CANCELLED]: filterByStatus(APPOINTMENT_STATUS.CANCELLED), [APPOINTMENT_STATUS.NO_SHOW]: filterByStatus(APPOINTMENT_STATUS.NO_SHOW),
    };
  }, [allAppts]);

  const createDoctorProfile = async (e) => {
    e.preventDefault();
    setCreatingProfile(true);
    try {
      const payload = {
        ...doctorForm,
        consultationFeeAmount: Number(doctorForm.consultationFeeAmount),
        yearsOfExperience: Number(doctorForm.yearsOfExperience),
      };
      const { data } = await apiClient.post('/Doctors', payload);
      if (!data.success) {
        toast.error(data.errors?.join('. ') || data.message || 'Failed to create doctor profile');
        return;
      }
      // Re-issue JWT so doctor_id claim is present for subsequent API calls.
      await refreshSession();
      toast.success('Doctor profile created and linked to your account');
    } catch (err) {
      toast.error(err.response?.data?.errors?.join('. ') || err.response?.data?.message || err.message || 'Failed to create doctor profile');
    } finally {
      setCreatingProfile(false);
    }
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
      <div className="max-w-lg mx-auto px-4 sm:px-6 py-6 sm:py-12 w-full min-w-0">
        <PageHeader
          title="Create Doctor Profile"
          subtitle="Your account is not linked to a doctor profile yet. Create one to manage appointments. Your session will refresh automatically so API calls use the new doctor_id claim."
        />
        <form
          onSubmit={createDoctorProfile}
          className="bg-white rounded-xl shadow-card p-4 sm:p-6 border border-border-light min-w-0"
          noValidate
          aria-label="Create doctor profile form"
        >
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-0 sm:gap-x-3">
            <Input label="First Name" name="firstName" value={doctorForm.firstName} onChange={setDoctorField('firstName')} required autoComplete="given-name" />
            <Input label="Last Name" name="lastName" value={doctorForm.lastName} onChange={setDoctorField('lastName')} required autoComplete="family-name" />
          </div>
          <Input label="Email" name="email" type="email" value={doctorForm.email} onChange={setDoctorField('email')} required autoComplete="email" />
          <Input label="Phone Number" name="phoneNumber" type="tel" value={doctorForm.phoneNumber} onChange={setDoctorField('phoneNumber')} required autoComplete="tel" />
          <Input label="License Number" name="licenseNumber" value={doctorForm.licenseNumber} onChange={setDoctorField('licenseNumber')} required />
          <Select label="Specialty" name="specialty" value={doctorForm.specialty} onChange={setDoctorField('specialty')}>
            {SPECIALTIES.map((s) => <option key={s} value={s}>{s}</option>)}
          </Select>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-0 sm:gap-x-3">
            <Input label="Fee Amount" name="consultationFeeAmount" type="number" min="0" step="0.01" value={doctorForm.consultationFeeAmount} onChange={setDoctorField('consultationFeeAmount')} required />
            <Input label="Currency" name="consultationFeeCurrency" value={doctorForm.consultationFeeCurrency} onChange={setDoctorField('consultationFeeCurrency')} required />
            <Input label="Years Experience" name="yearsOfExperience" type="number" min="0" value={doctorForm.yearsOfExperience} onChange={setDoctorField('yearsOfExperience')} required />
          </div>
          <Button type="submit" loading={creatingProfile} className="w-full mt-2" size="lg">
            Create Profile
          </Button>
        </form>
      </div>
    );
  }

  if (loading) return <div className="max-w-4xl mx-auto px-4 sm:px-6 py-6 sm:py-12"><Spinner /></div>;

  const currentList = filtered[activeTab] || allAppts;

  return (
    <div className="max-w-4xl mx-auto px-4 sm:px-6 py-6 sm:py-12">
      <PageHeader title="Doctor Dashboard" />

      <div className="flex flex-wrap gap-1.5 mb-4 sm:mb-6 -mx-1 px-1 overflow-x-auto pb-1">
        {TABS.map((t) => (
          <Button
            key={t}
            variant={activeTab === t ? 'primary' : 'secondary'}
            size="sm"
            className="shrink-0"
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
              <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-2 mb-3">
                <div className="flex flex-wrap items-center gap-2 min-w-0">
                  <span className="text-sm font-semibold text-text break-all">{appt.referenceCode}</span>
                  <Badge status={appt.status} />
                </div>
                <span className="text-xs text-text-muted shrink-0">{appt.scheduledDate}</span>
              </div>
              <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-text-muted mb-1">
                <span className="inline-flex items-center gap-1"><User size={12} className="shrink-0" />{appt.patient?.fullName}</span>
                <span className="inline-flex items-center gap-1"><Calendar size={12} className="shrink-0" />{appt.scheduledTimeFormatted}</span>
              </div>
              <p className="text-sm text-text mt-1 break-words">{appt.reason}</p>
              {appt.doctorNotes && <p className="text-xs text-status-completed-text mt-2 inline-flex items-start gap-1 break-words"><FileText size={12} className="shrink-0 mt-0.5" />Notes: {appt.doctorNotes}</p>}
              {appt.cancellationReason && <p className="text-xs text-status-cancelled-text mt-2 inline-flex items-start gap-1 break-words"><XCircle size={12} className="shrink-0 mt-0.5" />Cancel reason: {appt.cancellationReason}</p>}

              <div className="flex flex-col sm:flex-row flex-wrap gap-2 mt-3 pt-3 border-t border-border-light">
                {appt.status === APPOINTMENT_STATUS.PENDING && (
                  <>
                    <Button variant="primary" size="sm" className="w-full sm:w-auto" leftIcon={<CheckCircle size={14} />} onClick={() => openConfirm(appt)}>Confirm</Button>
                    <Button variant="secondary" size="sm" className="w-full sm:w-auto" onClick={() => openComplete(appt)}>Complete</Button>
                    <Button variant="ghost" size="sm" className="w-full sm:w-auto" onClick={() => doNoShow(appt)}>No-Show</Button>
                  </>
                )}
                {appt.status === 'Confirmed' && (
                  <Button variant="primary" size="sm" className="w-full sm:w-auto" onClick={() => openComplete(appt)}>Complete</Button>
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
            <Button variant="secondary" className="w-full sm:w-auto" onClick={() => setModal(null)}>Cancel</Button>
            <Button loading={submitting} className="w-full sm:w-auto" onClick={() => doConfirm(modal.appt)}>Confirm</Button>
          </>
        }
      >
        <p className="text-sm text-text-muted mb-4 break-words">{modal?.appt.referenceCode} — {modal?.appt.patient?.fullName}</p>
        <label className="flex items-start sm:items-center gap-2 text-sm text-text mb-4">
          <input
            type="checkbox"
            checked={overridePayment}
            onChange={(e) => setOverridePayment(e.target.checked)}
            className="rounded border-border text-primary focus:ring-primary mt-0.5 sm:mt-0 min-w-4 min-h-4"
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
            <Button variant="secondary" className="w-full sm:w-auto" onClick={() => { setModal(null); setNotes(''); }}>Cancel</Button>
            <Button loading={submitting} className="w-full sm:w-auto" onClick={() => doComplete(modal.appt)}>Complete</Button>
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
