import { useState } from 'react';
import toast from 'react-hot-toast';
import { CalendarPlus } from 'lucide-react';
import { Button } from './ui';
import { downloadAppointmentIcs } from '../services/calendarApi';

/**
 * Downloads GET /Appointments/{id}/calendar.ics as appointment.ics (or custom name).
 */
export default function AddToCalendarButton({
  appointmentId,
  referenceCode,
  className = '',
  variant = 'secondary',
  size = 'sm',
  disabled = false,
}) {
  const [loading, setLoading] = useState(false);

  const onClick = async () => {
    if (!appointmentId || loading) return;
    setLoading(true);
    try {
      const safeRef = referenceCode
        ? String(referenceCode).replace(/[^\w.-]+/g, '_')
        : null;
      await downloadAppointmentIcs(appointmentId, {
        filename: safeRef ? `${safeRef}.ics` : 'appointment.ics',
      });
      toast.success('Calendar file downloaded');
    } catch (err) {
      const msg =
        err.response?.status === 403
          ? 'You are not allowed to export this appointment.'
          : err.response?.status === 404
            ? 'Appointment not found.'
            : err.message || 'Failed to download calendar file';
      toast.error(msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Button
      type="button"
      variant={variant}
      size={size}
      className={`w-full sm:w-auto ${className}`}
      leftIcon={<CalendarPlus size={14} />}
      loading={loading}
      disabled={disabled || !appointmentId}
      onClick={onClick}
      aria-label="Add appointment to calendar"
    >
      Add to calendar
    </Button>
  );
}
