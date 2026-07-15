import { useState, useEffect, useMemo, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';
import { Button, Input, Textarea, Select, Card, Spinner, PageHeader, EmptyState } from '../components/ui';
import { ArrowLeft, Clock, Calendar } from 'lucide-react';
import useApiError, { fieldErrorList } from '../hooks/useApiError';
import {
  buildFreeSlots,
  formatWeeklyHoursSummary,
  toDateInputValue,
} from '../utils/bookingSlots';

const APPOINTMENT_TYPES = ['Standard', 'Insurance', 'Emergency', 'Vip'];

export default function BookAppointmentPage() {
  const { doctorId } = useParams();
  const navigate = useNavigate();
  const { patientId } = useAuth();
  const {
    fieldErrors: apiFieldErrors,
    generalError,
    applyError,
    clearErrors: clearApiErrors,
  } = useApiError();

  const [doctor, setDoctor] = useState(null);
  const [doctorLoading, setDoctorLoading] = useState(true);

  const [schedule, setSchedule] = useState(null);
  const [scheduleLoading, setScheduleLoading] = useState(true);
  const [scheduleError, setScheduleError] = useState(null);

  const minDateStr = toDateInputValue(new Date());
  const [selectedDate, setSelectedDate] = useState('');
  const [selectedSlot, setSelectedSlot] = useState(null);

  const [bookedSlots, setBookedSlots] = useState([]);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [slotsError, setSlotsError] = useState(null);

  const [submitting, setSubmitting] = useState(false);

  const {
    register,
    handleSubmit,
    setError,
    clearErrors,
    formState: { errors },
  } = useForm({
    defaultValues: {
      reason: '',
      appointmentType: 'Standard',
    },
  });

  useEffect(() => {
    if (!patientId) {
      toast.error('Create your patient profile first');
      navigate('/create-patient');
    }
  }, [patientId, navigate]);

  // Load doctor + weekly schedule
  useEffect(() => {
    let cancelled = false;
    (async () => {
      setDoctorLoading(true);
      setScheduleLoading(true);
      setScheduleError(null);
      try {
        const [docRes, schedRes] = await Promise.all([
          apiClient.get(`/Doctors/${doctorId}`),
          apiClient.get(`/Doctors/${doctorId}/schedule`),
        ]);
        if (cancelled) return;

        if (docRes.data?.success) setDoctor(docRes.data.data);
        else toast.error(docRes.data?.message || 'Doctor not found');

        if (schedRes.data?.success) setSchedule(schedRes.data.data);
        else {
          setSchedule(null);
          setScheduleError(schedRes.data?.message || 'Could not load doctor schedule');
        }
      } catch {
        if (!cancelled) {
          toast.error('Failed to load doctor details');
          setScheduleError('Failed to load doctor schedule');
        }
      } finally {
        if (!cancelled) {
          setDoctorLoading(false);
          setScheduleLoading(false);
        }
      }
    })();
    return () => { cancelled = true; };
  }, [doctorId]);

  // Load day availability when date changes
  useEffect(() => {
    if (!selectedDate || !doctorId) {
      setBookedSlots([]);
      setSlotsError(null);
      setSelectedSlot(null);
      return undefined;
    }

    let cancelled = false;
    (async () => {
      setSlotsLoading(true);
      setSlotsError(null);
      setSelectedSlot(null);
      try {
        const { data } = await apiClient.get(`/Doctors/${doctorId}/availability`, {
          params: { date: selectedDate },
        });
        if (cancelled) return;
        if (data?.success) {
          setBookedSlots(data.data?.bookedSlots || []);
        } else {
          setBookedSlots([]);
          setSlotsError(data?.message || 'Failed to load availability');
        }
      } catch {
        if (!cancelled) {
          setBookedSlots([]);
          setSlotsError('Failed to load availability for this day');
        }
      } finally {
        if (!cancelled) setSlotsLoading(false);
      }
    })();

    return () => { cancelled = true; };
  }, [selectedDate, doctorId]);

  const weeklySchedule = schedule?.weeklySchedule || [];

  const freeSlots = useMemo(() => {
    if (!selectedDate || scheduleLoading || slotsLoading) return [];
    return buildFreeSlots({
      dateStr: selectedDate,
      weeklySchedule,
      bookedSlots,
    });
  }, [selectedDate, weeklySchedule, bookedSlots, scheduleLoading, slotsLoading]);

  const hoursSummary = useMemo(
    () => formatWeeklyHoursSummary(weeklySchedule),
    [weeklySchedule],
  );

  const onDateChange = useCallback((e) => {
    const value = e.target.value;
    setSelectedDate(value);
    setSelectedSlot(null);
    clearErrors('scheduledTime');
  }, [clearErrors]);

  const selectSlot = useCallback((slot) => {
    setSelectedSlot(slot);
    clearErrors('scheduledTime');
  }, [clearErrors]);

  const mergeFieldError = (name) => {
    const client = errors[name]?.message;
    const server = fieldErrorList(apiFieldErrors, name)
      || fieldErrorList(apiFieldErrors, name === 'scheduledTime' ? 'datetime' : name);
    if (client && server?.length) return [client, ...server];
    if (server?.length) return server;
    if (client) return client;
    return undefined;
  };

  const onSubmit = async (data) => {
    clearApiErrors();
    if (!selectedSlot?.iso) {
      setError('scheduledTime', { type: 'manual', message: 'Please select an available time slot' });
      return;
    }

    setSubmitting(true);
    try {
      const { data: res } = await apiClient.post('/Appointments', {
        patientId,
        doctorId: Number(doctorId),
        scheduledTime: selectedSlot.iso,
        reason: data.reason.trim(),
        appointmentType: data.appointmentType,
      });
      if (res.success) {
        toast.success('Appointment booked! Redirecting to payment...');
        navigate(`/pay/${res.data.id}`);
      } else {
        const parsed = applyError({ response: { status: 400, data: res } });
        Object.entries(parsed.fieldErrors || {}).forEach(([field, msgs]) => {
          if (msgs?.length) setError(field, { type: 'server', message: msgs[0] });
        });
        if (!parsed.hasFieldErrors) {
          toast.error(res.errors?.join('. ') || res.message || 'Booking failed');
        }
      }
    } catch (err) {
      const parsed = applyError(err);
      Object.entries(parsed.fieldErrors || {}).forEach(([field, msgs]) => {
        if (msgs?.length) setError(field, { type: 'server', message: msgs[0] });
      });
      if (!parsed.hasFieldErrors) {
        toast.error(parsed.generalError || err.message || 'Booking failed');
      }
    } finally {
      setSubmitting(false);
    }
  };

  const slotError = mergeFieldError('scheduledTime') || mergeFieldError('datetime');

  return (
    <div className="max-w-lg mx-auto px-4 sm:px-6 py-6 sm:py-12 w-full min-w-0">
      <Button
        variant="ghost"
        size="sm"
        className="mb-4 sm:mb-6 -ml-1 sm:-ml-2"
        onClick={() => navigate('/doctors')}
        leftIcon={<ArrowLeft size={14} />}
        aria-label="Back to doctors list"
      >
        <span className="sm:hidden">Back</span>
        <span className="hidden sm:inline">Back to Doctors</span>
      </Button>

      <PageHeader
        title="Book Appointment"
        subtitle="Pick a free slot based on the doctor’s working hours."
      />

      {doctorLoading || scheduleLoading ? (
        <div className="flex justify-center py-8" role="status" aria-label="Loading doctor">
          <Spinner />
        </div>
      ) : doctor ? (
        <Card className="mb-4 sm:mb-6" aria-label="Selected doctor">
          <p className="text-sm font-medium text-text break-words">Dr. {doctor.fullName}</p>
          <p className="text-xs text-text-muted mt-0.5 break-words">{doctor.specialties?.join(', ')}</p>
          {hoursSummary && (
            <p className="text-xs text-text-secondary mt-2 m-0 inline-flex items-start gap-1.5 break-words">
              <Clock size={12} className="shrink-0 mt-0.5" aria-hidden="true" />
              <span>{hoursSummary}</span>
            </p>
          )}
          {schedule && !schedule.isAcceptingPatients && (
            <p className="text-xs text-status-cancelled-text mt-2 m-0" role="status">
              This doctor is not currently accepting new patients.
            </p>
          )}
        </Card>
      ) : null}

      {scheduleError && !scheduleLoading && (
        <div className="mb-4" role="alert">
          <EmptyState message={scheduleError} actionLabel="Reload" onAction={() => window.location.reload()} />
        </div>
      )}

      <form
        onSubmit={handleSubmit(onSubmit)}
        className="bg-white rounded-xl shadow-card p-4 sm:p-6 border border-border-light min-w-0"
        noValidate
        aria-label="Book appointment form"
      >
        {generalError && (
          <div
            className="mb-4 rounded-lg border border-status-cancelled-text/30 bg-status-cancelled-bg px-3 py-2 text-sm text-status-cancelled-text break-words"
            role="alert"
          >
            {generalError}
          </div>
        )}

        <Input
          label="Date"
          type="date"
          name="appointmentDate"
          id="appointmentDate"
          min={minDateStr}
          value={selectedDate}
          onChange={onDateChange}
          required
          helperText="Only future dates. Slots require at least 1 hour notice."
        />

        <div className="mb-4 min-w-0" role="group" aria-labelledby="slot-picker-label">
          <p id="slot-picker-label" className="block text-sm font-medium text-text mb-1.5">
            Available times
            <span className="text-status-cancelled-text ml-0.5">
              <span aria-hidden="true">*</span>
              <span className="sr-only"> (required)</span>
            </span>
          </p>

          {!selectedDate && (
            <p className="text-sm text-text-muted m-0 inline-flex items-center gap-1.5">
              <Calendar size={14} aria-hidden="true" />
              Select a date to see free slots
            </p>
          )}

          {selectedDate && slotsLoading && (
            <div className="py-4" role="status" aria-label="Loading available times">
              <Spinner size="sm" text="Loading free slots…" />
            </div>
          )}

          {selectedDate && slotsError && !slotsLoading && (
            <p className="text-sm text-status-cancelled-text m-0" role="alert">{slotsError}</p>
          )}

          {selectedDate && !slotsLoading && !slotsError && freeSlots.length === 0 && (
            <EmptyState
              message="No available slots on this day. Try another date."
              icon={<Clock size={24} className="text-text-muted" />}
            />
          )}

          {selectedDate && !slotsLoading && freeSlots.length > 0 && (
            <div
              className="flex flex-wrap gap-2"
              role="listbox"
              aria-label="Free time slots"
              aria-required="true"
            >
              {freeSlots.map((slot) => {
                const selected = selectedSlot?.time === slot.time;
                return (
                  <button
                    key={slot.time}
                    type="button"
                    role="option"
                    aria-selected={selected}
                    className={`min-h-11 min-w-[4.5rem] px-3 py-2 rounded-full text-sm font-medium border transition-all duration-150 cursor-pointer focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary ${
                      selected
                        ? 'bg-primary text-white border-primary shadow-card'
                        : 'bg-white text-text border-border hover:bg-surface'
                    }`}
                    onClick={() => selectSlot(slot)}
                  >
                    {slot.label}
                  </button>
                );
              })}
            </div>
          )}

          {slotError && (
            <p className="mt-2 text-xs text-status-cancelled-text m-0" role="alert">
              {Array.isArray(slotError) ? slotError[0] : slotError}
            </p>
          )}

          {selectedSlot && (
            <p className="mt-2 text-xs text-text-muted m-0" aria-live="polite">
              Selected: <span className="font-medium text-text">{selectedDate} at {selectedSlot.time}</span>
            </p>
          )}
        </div>

        <Textarea
          label="Reason"
          placeholder="Briefly describe your reason (min 10 characters)..."
          error={mergeFieldError('reason')}
          {...register('reason', {
            required: 'Reason is required',
            minLength: { value: 10, message: 'Reason must be at least 10 characters' },
          })}
        />

        <Select
          label="Appointment Type"
          error={mergeFieldError('appointmentType')}
          {...register('appointmentType', { required: 'Appointment type is required' })}
        >
          {APPOINTMENT_TYPES.map((t) => (
            <option key={t} value={t}>{t}</option>
          ))}
        </Select>

        <Button
          type="submit"
          loading={submitting}
          className="w-full mt-2"
          size="lg"
          disabled={!selectedSlot || !!scheduleError || (schedule && !schedule.isAcceptingPatients)}
        >
          Book Appointment
        </Button>
      </form>
    </div>
  );
}
