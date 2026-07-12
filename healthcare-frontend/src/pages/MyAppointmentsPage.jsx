import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';
import { Button, Card, Badge, Spinner, EmptyState, Modal, Textarea, PageHeader } from '../components/ui';
import { Calendar, Clock, AlertCircle, CalendarClock, CreditCard } from 'lucide-react';
import { APPOINTMENT_STATUS } from '../constants/appointmentStatus';

export default function MyAppointmentsPage() {
  const { patientId } = useAuth();
  const navigate = useNavigate();
  const [appointments, setAppointments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState(false);
  const [cancelling, setCancelling] = useState(null);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelError, setCancelError] = useState('');
  const [paymentStatuses, setPaymentStatuses] = useState({});

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

  if (loading) return <div className="max-w-3xl mx-auto px-4 sm:px-6 py-6 sm:py-12"><Spinner /></div>;

  if (fetchError) {
    return (
      <div className="max-w-3xl mx-auto px-4 sm:px-6 py-6 sm:py-12">
        <PageHeader title="My Appointments" />
        <EmptyState message="Failed to load appointments." actionLabel="Retry" onAction={fetchAppointments} />
      </div>
    );
  }

  return (
    <div className="max-w-3xl mx-auto px-4 sm:px-6 py-6 sm:py-12">
      <PageHeader title="My Appointments" />

      {appointments.length === 0 ? (
        <EmptyState
          icon={<CalendarClock size={24} className="text-text-muted" />}
          message="No appointments found."
          actionLabel="Browse Doctors"
          onAction={() => navigate('/doctors')}
        />
      ) : (
        <div className="space-y-3">
          {appointments.map((appt) => (
            <Card key={appt.id}>
              <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-2 mb-3">
                <div className="flex flex-wrap items-center gap-2 min-w-0">
                  <span className="text-sm font-semibold text-text break-all">{appt.referenceCode}</span>
                  <Badge status={appt.status} />
                </div>
                <span className="text-xs text-text-muted shrink-0">{appt.scheduledDate}</span>
              </div>
              <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-text-muted mb-1">
                <span className="inline-flex items-center gap-1">
                  <Calendar size={12} className="shrink-0" />
                  Dr. {appt.doctor?.fullName}
                </span>
                <span className="inline-flex items-center gap-1">
                  <Clock size={12} className="shrink-0" />
                  {appt.scheduledTimeFormatted}
                </span>
              </div>
              <p className="text-sm text-text mt-1 break-words">{appt.reason}</p>
              {appt.cancellationReason && (
                <p className="text-xs text-status-cancelled-text mt-2 inline-flex items-start gap-1 break-words">
                  <AlertCircle size={12} className="shrink-0 mt-0.5" />
                  Cancellation reason: {appt.cancellationReason}
                </p>
              )}
              {appt.status === APPOINTMENT_STATUS.PENDING && (
                <div className="mt-3 pt-3 border-t border-border-light space-y-2">
                  <div className="flex flex-col xs:flex-row sm:flex-row sm:items-center sm:justify-between gap-2">
                    <span className="text-xs text-text-muted inline-flex items-center gap-1">
                      <CreditCard size={12} className="shrink-0" />
                      {paymentStatuses[appt.id] === 'Succeeded' ? 'Paid' : 'Payment required'}
                    </span>
                    <Button
                      variant="primary"
                      size="sm"
                      className="w-full sm:w-auto"
                      leftIcon={<CreditCard size={14} />}
                      onClick={() => navigate(`/pay/${appt.id}`)}
                    >
                      Pay Now
                    </Button>
                  </div>
                  <div className="flex justify-stretch sm:justify-end">
                    <Button variant="ghost" size="sm" className="w-full sm:w-auto" onClick={() => openCancelModal(appt)}>Cancel</Button>
                  </div>
                </div>
              )}
            </Card>
          ))}
        </div>
      )}

      <Modal
        open={!!cancelling}
        onClose={() => setCancelling(null)}
        title="Cancel Appointment"
        footer={
          <>
            <Button variant="secondary" className="w-full sm:w-auto" onClick={() => setCancelling(null)}>Keep</Button>
            <Button variant="danger" className="w-full sm:w-auto" onClick={confirmCancel}>Confirm Cancel</Button>
          </>
        }
      >
        <p className="text-sm text-text-muted mb-4">
          {cancelling?.referenceCode} — Dr. {cancelling?.doctor?.fullName}
        </p>
        <Textarea
          placeholder="Reason for cancellation (min 10 characters)..."
          value={cancelReason}
          onChange={(e) => { setCancelReason(e.target.value); setCancelError(''); }}
          error={cancelError}
        />
      </Modal>
    </div>
  );
}
