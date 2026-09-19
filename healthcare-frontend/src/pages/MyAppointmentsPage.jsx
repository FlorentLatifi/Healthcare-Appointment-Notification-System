import { useState, useEffect, useCallback, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';
import { Button, Card, Badge, Spinner, EmptyState, Modal, Textarea, PageHeader } from '../components/ui';
import AddToCalendarButton from '../components/AddToCalendarButton';
import {
  Calendar,
  Clock,
  AlertCircle,
  CalendarClock,
  CreditCard,
  XCircle,
  CalendarPlus,
} from 'lucide-react';
import { APPOINTMENT_STATUS } from '../constants/appointmentStatus';

function doctorIdOf(appt) {
  const id = appt?.doctorId ?? appt?.doctor?.id;
  return id != null && Number(id) > 0 ? Number(id) : null;
}

function canPatientCancel(status) {
  return status === APPOINTMENT_STATUS.PENDING || status === APPOINTMENT_STATUS.CONFIRMED;
}

export default function MyAppointmentsPage() {
  const { patientId } = useAuth();
  const navigate = useNavigate();
  const [appointments, setAppointments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState(false);
  const [cancelling, setCancelling] = useState(null);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelError, setCancelError] = useState('');
  const [cancellingSubmit, setCancellingSubmit] = useState(false);
  const [paymentStatuses, setPaymentStatuses] = useState({});
  /** After cancel: offer book-again with same doctor when possible */
  const [rebookPrompt, setRebookPrompt] = useState(null);
  const cancelReasonRef = useRef(null);

  useEffect(() => {
    if (!patientId) {
      toast.error('Create your patient profile first');
      navigate('/create-patient');
      return;
    }
    fetchAppointments();
  }, [patientId]);

  const fetchPaymentStatus = useCallback(async (appointmentId) => {
    try {
      const { data } = await apiClient.get(`/Payments/appointment/${appointmentId}`);
      if (data.success && data.data) {
        setPaymentStatuses((prev) => ({ ...prev, [appointmentId]: data.data.status }));
      } else {
        setPaymentStatuses((prev) => ({ ...prev, [appointmentId]: null }));
      }
    } catch {
      setPaymentStatuses((prev) => ({ ...prev, [appointmentId]: null }));
    }
  }, []);

  useEffect(() => {
    const pendingIds = appointments
      .filter((a) => a.status === APPOINTMENT_STATUS.PENDING)
      .map((a) => a.id);
    if (pendingIds.length > 0) {
      pendingIds.forEach((id) => fetchPaymentStatus(id));
    }
  }, [appointments, fetchPaymentStatus]);

  const fetchAppointments = async () => {
    setLoading(true);
    setFetchError(false);
    try {
      const { data } = await apiClient.get(`/Appointments/patient/${patientId}`, { params: { pageSize: 50 } });
      if (data.success) setAppointments(data.data.items);
      else { toast.error(data.message || 'Failed to load appointments'); setFetchError(true); }
    } catch {
      toast.error('Failed to load appointments');
      setFetchError(true);
    } finally {
      setLoading(false);
    }
  };

  const goBook = (docId) => {
    if (docId) navigate(`/book-appointment/${docId}`);
    else navigate('/doctors');
  };

  const openCancelModal = (appt) => {
    setCancelling(appt);
    setCancelReason('');
    setCancelError('');
  };

  const confirmCancel = async () => {
    if (!cancelReason || cancelReason.trim().length < 10) {
      setCancelError('Cancellation reason must be at least 10 characters');
      cancelReasonRef.current?.focus?.();
      return;
    }
    setCancellingSubmit(true);
    try {
      const { data } = await apiClient.put(`/Appointments/${cancelling.id}/cancel`, {
        appointmentId: cancelling.id,
        cancellationReason: cancelReason.trim(),
      });
      if (data.success) {
        const docId = doctorIdOf(cancelling);
        const doctorName = cancelling.doctor?.fullName || 'your doctor';
        setRebookPrompt({
          doctorId: docId,
          doctorName,
          referenceCode: cancelling.referenceCode,
        });
        toast.success(
          'Appointment cancelled. You can book a new time whenever you are ready.',
          { duration: 5000 },
        );
        setCancelling(null);
        fetchAppointments();
      } else {
        toast.error(data.errors?.join('. ') || data.message || 'Cancellation failed');
      }
    } catch (err) {
      toast.error(err.response?.data?.errors?.join('. ') || err.response?.data?.message || 'Cancellation failed');
    } finally {
      setCancellingSubmit(false);
    }
  };

  if (loading) {
    return (
      <div className="max-w-3xl mx-auto px-4 sm:px-6 py-6 sm:py-12 w-full min-w-0">
        <div role="status" aria-label="Loading appointments" className="flex justify-center py-8">
          <Spinner />
        </div>
      </div>
    );
  }

  if (fetchError) {
    return (
      <div className="max-w-3xl mx-auto px-4 sm:px-6 py-6 sm:py-12 w-full min-w-0">
        <PageHeader title="My Appointments" />
        <EmptyState message="Failed to load appointments." actionLabel="Retry" onAction={fetchAppointments} />
      </div>
    );
  }

  return (
    <div className="max-w-3xl mx-auto px-4 sm:px-6 py-6 sm:py-12 w-full min-w-0">
      <PageHeader
        title="My Appointments"
        subtitle="Need a different time? Cancel the appointment and book a new one — there is no separate reschedule step."
        actions={
          <Button
            variant="secondary"
            size="sm"
            className="w-full sm:w-auto"
            leftIcon={<CalendarPlus size={14} />}
            onClick={() => navigate('/doctors')}
          >
            Book new appointment
          </Button>
        }
      />

      {rebookPrompt && (
        <div
          className="mb-4 sm:mb-6 p-4 sm:p-5 rounded-xl border border-primary/25 bg-primary-50/80 shadow-card"
          role="status"
          aria-live="polite"
          data-testid="rebook-prompt"
        >
          <p className="text-sm font-semibold text-text m-0 mb-1">
            Appointment cancelled
            {rebookPrompt.referenceCode ? ` (${rebookPrompt.referenceCode})` : ''}
          </p>
          <p className="text-sm text-text-muted m-0 mb-3 break-words">
            Want a different time with Dr. {rebookPrompt.doctorName}? Book a new appointment — free slots only.
          </p>
          <div className="flex flex-col sm:flex-row gap-2">
            <Button
              className="w-full sm:w-auto"
              leftIcon={<CalendarPlus size={14} />}
              onClick={() => {
                const id = rebookPrompt.doctorId;
                setRebookPrompt(null);
                goBook(id);
              }}
            >
              Book a new one
              {rebookPrompt.doctorId ? ' with same doctor' : ''}
            </Button>
            <Button
              variant="secondary"
              className="w-full sm:w-auto"
              onClick={() => setRebookPrompt(null)}
            >
              Dismiss
            </Button>
          </div>
        </div>
      )}

      {appointments.length === 0 ? (
        <EmptyState
          icon={<CalendarClock size={24} className="text-text-muted" />}
          message="No appointments yet. Book a visit when you are ready — if you need to change a time later, cancel and book again."
          actionLabel="Browse doctors"
          onAction={() => navigate('/doctors')}
        />
      ) : (
        <ul className="space-y-3 list-none m-0 p-0" aria-label="Your appointments">
          {appointments.map((appt) => {
            const docId = doctorIdOf(appt);
            const isCancellable = canPatientCancel(appt.status);
            const isCancelled = appt.status === APPOINTMENT_STATUS.CANCELLED;

            return (
              <li key={appt.id}>
                <Card className="min-w-0">
                  <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-2 mb-3">
                    <div className="flex flex-wrap items-center gap-2 min-w-0">
                      <span className="text-sm font-semibold text-text break-all">{appt.referenceCode}</span>
                      <Badge status={appt.status} />
                    </div>
                    <span className="text-xs text-text-muted shrink-0">{appt.scheduledDate}</span>
                  </div>
                  <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-text-muted mb-1">
                    <span className="inline-flex items-center gap-1 min-w-0">
                      <Calendar size={12} className="shrink-0" aria-hidden="true" />
                      <span className="break-words">Dr. {appt.doctor?.fullName}</span>
                    </span>
                    <span className="inline-flex items-center gap-1">
                      <Clock size={12} className="shrink-0" aria-hidden="true" />
                      {appt.scheduledTimeFormatted}
                    </span>
                  </div>
                  <p className="text-sm text-text mt-1 break-words">{appt.reason}</p>
                  {appt.cancellationReason && (
                    <p className="text-xs text-status-cancelled-text mt-2 inline-flex items-start gap-1 break-words">
                      <AlertCircle size={12} className="shrink-0 mt-0.5" aria-hidden="true" />
                      Cancellation reason: {appt.cancellationReason}
                    </p>
                  )}

                  <div className="mt-3 pt-3 border-t border-border-light space-y-3">
                    {!isCancelled && (
                      <div className="flex flex-col sm:flex-row flex-wrap gap-2">
                        <AddToCalendarButton
                          appointmentId={appt.id}
                          referenceCode={appt.referenceCode}
                        />
                      </div>
                    )}

                    {appt.status === APPOINTMENT_STATUS.PENDING && (
                      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
                        <span className="text-xs text-text-muted inline-flex items-center gap-1">
                          <CreditCard size={12} className="shrink-0" aria-hidden="true" />
                          {paymentStatuses[appt.id] === 'Succeeded' ? 'Paid' : 'Payment required'}
                        </span>
                        <Button
                          variant="primary"
                          size="sm"
                          className="w-full sm:w-auto"
                          leftIcon={<CreditCard size={14} />}
                          onClick={() => navigate(`/pay/${appt.id}`)}
                        >
                          Pay now
                        </Button>
                      </div>
                    )}

                    {isCancellable && (
                      <div className="space-y-2">
                        <p className="text-xs text-text-muted m-0 break-words">
                          Need a different time? Cancel this appointment and book a new one — we do not offer in-place reschedule.
                        </p>
                        <Button
                          variant="danger"
                          size="sm"
                          className="w-full sm:w-auto"
                          leftIcon={<XCircle size={14} />}
                          onClick={() => openCancelModal(appt)}
                        >
                          Cancel appointment
                        </Button>
                      </div>
                    )}

                    {isCancelled && (
                      <div className="space-y-2">
                        <p className="text-xs text-text-muted m-0">
                          This visit was cancelled. Book again if you still need care.
                        </p>
                        <Button
                          variant="secondary"
                          size="sm"
                          className="w-full sm:w-auto"
                          leftIcon={<CalendarPlus size={14} />}
                          onClick={() => goBook(docId)}
                        >
                          Book again
                          {docId ? ' with same doctor' : ''}
                        </Button>
                      </div>
                    )}
                  </div>
                </Card>
              </li>
            );
          })}
        </ul>
      )}

      <Modal
        open={!!cancelling}
        onClose={() => !cancellingSubmit && setCancelling(null)}
        title="Cancel appointment"
        initialFocusRef={cancelReasonRef}
        footer={
          <>
            <Button
              variant="secondary"
              className="w-full sm:w-auto"
              disabled={cancellingSubmit}
              onClick={() => setCancelling(null)}
            >
              Keep appointment
            </Button>
            <Button
              variant="danger"
              className="w-full sm:w-auto"
              loading={cancellingSubmit}
              onClick={confirmCancel}
            >
              Confirm cancel
            </Button>
          </>
        }
      >
        <p className="text-sm text-text mb-2 break-words">
          {cancelling?.referenceCode} — Dr. {cancelling?.doctor?.fullName}
        </p>
        <p className="text-sm text-text-muted mb-4 break-words">
          Cancelling frees the slot. To change the time, cancel here and then book a new appointment
          {doctorIdOf(cancelling) ? ' with the same doctor' : ''}. There is no separate reschedule action.
        </p>
        <Textarea
          ref={cancelReasonRef}
          id="cancel-reason"
          label="Cancellation reason"
          placeholder="Reason for cancellation (min 10 characters)..."
          value={cancelReason}
          onChange={(e) => { setCancelReason(e.target.value); setCancelError(''); }}
          error={cancelError}
          helperText="Minimum 10 characters"
          required
        />
      </Modal>
    </div>
  );
}
