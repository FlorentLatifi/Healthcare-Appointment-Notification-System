import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';
import { Button, Input, Textarea, Select, Card, Spinner, PageHeader } from '../components/ui';
import { ArrowLeft } from 'lucide-react';
import useApiError, { fieldErrorList } from '../hooks/useApiError';

const APPOINTMENT_TYPES = ['Standard', 'Insurance', 'Emergency', 'Vip'];

function toLocalDatetimeString(date) {
  const pad = (n) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

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
  const [submitting, setSubmitting] = useState(false);

  const minDate = toLocalDatetimeString(new Date(Date.now() + 3600000));

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm({
    defaultValues: {
      scheduledTime: '',
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

  useEffect(() => {
    (async () => {
      setDoctorLoading(true);
      try {
        const { data } = await apiClient.get(`/Doctors/${doctorId}`);
        if (data.success) setDoctor(data.data);
        else toast.error(data.message || 'Doctor not found');
      } catch {
        toast.error('Failed to load doctor details');
      } finally {
        setDoctorLoading(false);
      }
    })();
  }, [doctorId]);

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
    setSubmitting(true);
    try {
      const { data: res } = await apiClient.post('/Appointments', {
        patientId,
        doctorId: Number(doctorId),
        scheduledTime: new Date(data.scheduledTime).toISOString(),
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

  return (
    <div className="max-w-lg mx-auto px-4 sm:px-6 py-6 sm:py-12">
      <Button
        variant="ghost"
        size="sm"
        className="mb-4 sm:mb-6 -ml-1 sm:-ml-2"
        onClick={() => navigate('/doctors')}
      >
        <ArrowLeft size={14} />
        Back to Doctors
      </Button>

      <PageHeader title="Book Appointment" />

      {doctorLoading ? (
        <div className="flex justify-center py-8"><Spinner /></div>
      ) : doctor ? (
        <Card className="mb-4 sm:mb-6">
          <p className="text-sm font-medium text-text">Dr. {doctor.fullName}</p>
          <p className="text-xs text-text-muted mt-0.5 break-words">{doctor.specialties?.join(', ')}</p>
        </Card>
      ) : null}

      <form
        onSubmit={handleSubmit(onSubmit)}
        className="bg-white rounded-xl shadow-card p-4 sm:p-6 border border-border-light"
        noValidate
      >
        {generalError && (
          <div
            className="mb-4 rounded-lg border border-status-cancelled-text/30 bg-status-cancelled-bg px-3 py-2 text-sm text-status-cancelled-text"
            role="alert"
          >
            {generalError}
          </div>
        )}

        <Input
          label="Date & Time"
          type="datetime-local"
          min={minDate}
          step="1800"
          className="min-w-0"
          error={mergeFieldError('scheduledTime') || mergeFieldError('datetime')}
          {...register('scheduledTime', {
            required: 'Date and time is required',
            validate: {
              future: (v) => {
                if (!v) return true;
                return new Date(v) >= new Date() || 'Cannot be in the past';
              },
              interval: (v) => {
                if (!v) return true;
                const mins = new Date(v).getMinutes();
                return mins % 30 === 0 || 'Time must be in 30-minute intervals (e.g. 09:00, 09:30)';
              },
            },
          })}
        />

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

        <Button type="submit" loading={submitting} className="w-full mt-2" size="lg">
          Book Appointment
        </Button>
      </form>
    </div>
  );
}
