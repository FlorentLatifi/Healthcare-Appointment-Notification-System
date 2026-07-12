import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';
import { Button, Input, Textarea, Select, Card, Spinner, PageHeader } from '../components/ui';
import { ArrowLeft } from 'lucide-react';

const APPOINTMENT_TYPES = ['Standard', 'Insurance', 'Emergency', 'Vip'];

function toLocalDatetimeString(date) {
  const pad = (n) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export default function BookAppointmentPage() {
  const { doctorId } = useParams();
  const navigate = useNavigate();
  const { patientId } = useAuth();

  const [doctor, setDoctor] = useState(null);
  const [doctorLoading, setDoctorLoading] = useState(true);
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
        toast.success('Appointment booked! Redirecting to payment...');
        navigate(`/pay/${data.data.id}`);
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

      <form onSubmit={handleSubmit} className="bg-white rounded-xl shadow-card p-4 sm:p-6 border border-border-light">
        <Input
          label="Date & Time"
          type="datetime-local"
          value={datetime}
          min={minDate}
          step="1800"
          onChange={(e) => setDatetime(e.target.value)}
          error={errors.datetime}
          className="min-w-0"
        />

        <Textarea
          label="Reason"
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder="Briefly describe your reason (min 10 characters)..."
          error={errors.reason}
        />

        <Select label="Appointment Type" value={appointmentType} onChange={(e) => setAppointmentType(e.target.value)} error={errors.appointmentType}>
          {APPOINTMENT_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
        </Select>

        <Button type="submit" loading={submitting} className="w-full mt-2" size="lg">
          Book Appointment
        </Button>
      </form>
    </div>
  );
}
