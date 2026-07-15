import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';
import { Button, Input, Select, PageHeader, Spinner, Modal, Card } from '../components/ui';
import { SPECIALTIES, DEFAULT_SPECIALTY } from '../constants/specialties';
import { Clock } from 'lucide-react';

const emptyForm = {
  firstName: '',
  lastName: '',
  email: '',
  phoneNumber: '',
  licenseNumber: '',
  specialty: DEFAULT_SPECIALTY,
  consultationFeeAmount: '',
  consultationFeeCurrency: 'USD',
  yearsOfExperience: '',
};

/** JS / .NET: Sunday = 0 … Saturday = 6 */
const WEEK_DAYS = [
  { dayOfWeek: 1, label: 'Monday' },
  { dayOfWeek: 2, label: 'Tuesday' },
  { dayOfWeek: 3, label: 'Wednesday' },
  { dayOfWeek: 4, label: 'Thursday' },
  { dayOfWeek: 5, label: 'Friday' },
  { dayOfWeek: 6, label: 'Saturday' },
  { dayOfWeek: 0, label: 'Sunday' },
];

function defaultWeeklySchedule() {
  return WEEK_DAYS.map(({ dayOfWeek }) => ({
    dayOfWeek,
    isWorkingDay: dayOfWeek >= 1 && dayOfWeek <= 5,
    startTime: '08:00',
    endTime: '18:00',
  }));
}

function normalizeSchedule(rows) {
  const map = new Map(
    (Array.isArray(rows) ? rows : []).map((r) => [Number(r.dayOfWeek), r]),
  );
  return WEEK_DAYS.map(({ dayOfWeek }) => {
    const row = map.get(dayOfWeek);
    if (!row) {
      return {
        dayOfWeek,
        isWorkingDay: dayOfWeek >= 1 && dayOfWeek <= 5,
        startTime: '08:00',
        endTime: '18:00',
      };
    }
    return {
      dayOfWeek,
      isWorkingDay: !!row.isWorkingDay,
      startTime: row.startTime || '08:00',
      endTime: row.endTime || '18:00',
    };
  });
}

function mapApiErrors(err, data) {
  const list = data?.errors || err?.response?.data?.errors;
  if (Array.isArray(list) && list.length) return list.join('. ');
  return data?.message || err?.response?.data?.message || err?.message || 'Request failed';
}

function validateSchedule(schedule) {
  for (const row of schedule) {
    if (!row.isWorkingDay) continue;
    if (!row.startTime || !row.endTime) {
      return 'Working days need both start and end times.';
    }
    if (row.startTime >= row.endTime) {
      return 'Start time must be before end time on every working day.';
    }
  }
  return null;
}

export default function EditDoctorProfilePage() {
  const { doctorId, logout } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState(emptyForm);
  const [schedule, setSchedule] = useState(defaultWeeklySchedule);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [savingSchedule, setSavingSchedule] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [showDelete, setShowDelete] = useState(false);
  const [fieldErrors, setFieldErrors] = useState({});
  const [scheduleError, setScheduleError] = useState(null);

  const set = (field) => (e) => {
    setForm((prev) => ({ ...prev, [field]: e.target.value }));
    setFieldErrors((prev) => ({ ...prev, [field]: undefined }));
  };

  const updateDay = (dayOfWeek, patch) => {
    setSchedule((prev) =>
      prev.map((row) => (row.dayOfWeek === dayOfWeek ? { ...row, ...patch } : row)),
    );
    setScheduleError(null);
  };

  useEffect(() => {
    if (!doctorId) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const [profileRes, scheduleRes] = await Promise.all([
          apiClient.get(`/Doctors/${doctorId}`),
          apiClient.get(`/Doctors/${doctorId}/schedule`),
        ]);
        if (cancelled) return;

        if (!profileRes.data?.success || !profileRes.data.data) {
          toast.error(profileRes.data?.message || 'Failed to load profile');
        } else {
          const d = profileRes.data.data;
          setForm({
            firstName: d.firstName || '',
            lastName: d.lastName || '',
            email: d.email || '',
            phoneNumber: d.phoneNumber || '',
            licenseNumber: d.licenseNumber || '',
            specialty: d.specialties?.[0] || DEFAULT_SPECIALTY,
            consultationFeeAmount: d.consultationFeeAmount != null ? String(d.consultationFeeAmount) : '',
            consultationFeeCurrency: d.consultationFeeCurrency || 'USD',
            yearsOfExperience: d.yearsOfExperience != null ? String(d.yearsOfExperience) : '',
          });
        }

        if (scheduleRes.data?.success && scheduleRes.data.data?.weeklySchedule) {
          setSchedule(normalizeSchedule(scheduleRes.data.data.weeklySchedule));
        } else if (profileRes.data?.data?.weeklySchedule?.length) {
          setSchedule(normalizeSchedule(profileRes.data.data.weeklySchedule));
        }
      } catch (err) {
        if (!cancelled) toast.error(mapApiErrors(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [doctorId]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!doctorId) return;
    setSubmitting(true);
    setFieldErrors({});
    try {
      const { data } = await apiClient.put(`/Doctors/${doctorId}`, {
        ...form,
        consultationFeeAmount: Number(form.consultationFeeAmount),
        yearsOfExperience: Number(form.yearsOfExperience),
      });
      if (data.success) {
        toast.success('Profile updated');
        navigate('/doctor-dashboard');
      } else {
        toast.error(mapApiErrors(null, data));
      }
    } catch (err) {
      const api = err.response?.data;
      const errors = api?.errors;
      if (errors && typeof errors === 'object' && !Array.isArray(errors)) {
        const next = {};
        Object.entries(errors).forEach(([key, msgs]) => {
          const field = key.replace(/^\$?\./, '').split('.').pop();
          next[field.charAt(0).toLowerCase() + field.slice(1)] = Array.isArray(msgs) ? msgs[0] : String(msgs);
        });
        setFieldErrors(next);
      }
      toast.error(mapApiErrors(err, api));
    } finally {
      setSubmitting(false);
    }
  };

  const handleSaveSchedule = async () => {
    if (!doctorId) return;
    const clientErr = validateSchedule(schedule);
    if (clientErr) {
      setScheduleError(clientErr);
      return;
    }
    setSavingSchedule(true);
    setScheduleError(null);
    try {
      const payload = {
        weeklySchedule: schedule.map((row) => ({
          dayOfWeek: row.dayOfWeek,
          isWorkingDay: row.isWorkingDay,
          startTime: row.isWorkingDay ? row.startTime : null,
          endTime: row.isWorkingDay ? row.endTime : null,
        })),
      };
      const { data } = await apiClient.put(`/Doctors/${doctorId}/schedule`, payload);
      if (data.success) {
        toast.success('Working hours updated — booking slots will use the new times.');
      } else {
        setScheduleError(mapApiErrors(null, data));
        toast.error(mapApiErrors(null, data));
      }
    } catch (err) {
      const msg = mapApiErrors(err);
      setScheduleError(msg);
      toast.error(msg);
    } finally {
      setSavingSchedule(false);
    }
  };

  const handleDelete = async () => {
    if (!doctorId) return;
    setDeleting(true);
    try {
      await apiClient.delete(`/Doctors/${doctorId}`);
      toast.success('Profile deleted');
      setShowDelete(false);
      await logout();
      navigate('/login', { replace: true });
    } catch (err) {
      toast.error(mapApiErrors(err));
    } finally {
      setDeleting(false);
    }
  };

  if (!doctorId) {
    return (
      <div className="max-w-lg mx-auto px-4 sm:px-6 py-6 sm:py-12 w-full min-w-0">
        <PageHeader title="Edit Doctor Profile" subtitle="No linked doctor profile found." />
        <Button className="w-full" onClick={() => navigate('/doctor-dashboard')}>Create Profile</Button>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="max-w-lg mx-auto px-4 sm:px-6 py-6 sm:py-12">
        <Spinner />
      </div>
    );
  }

  return (
    <div className="max-w-lg mx-auto px-4 sm:px-6 py-6 sm:py-12 w-full min-w-0">
      <PageHeader
        title="Edit Doctor Profile"
        subtitle="Update your professional details and weekly working hours."
      />

      <form
        onSubmit={handleSubmit}
        className="bg-white rounded-xl shadow-card p-4 sm:p-6 border border-border-light min-w-0"
        noValidate
        aria-label="Edit doctor profile form"
      >
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-0 sm:gap-x-3">
          <Input label="First Name" name="firstName" value={form.firstName} onChange={set('firstName')} error={fieldErrors.firstName} required autoComplete="given-name" />
          <Input label="Last Name" name="lastName" value={form.lastName} onChange={set('lastName')} error={fieldErrors.lastName} required autoComplete="family-name" />
        </div>
        <Input label="Email" name="email" type="email" value={form.email} onChange={set('email')} error={fieldErrors.email} required autoComplete="email" />
        <Input label="Phone Number" name="phoneNumber" type="tel" value={form.phoneNumber} onChange={set('phoneNumber')} error={fieldErrors.phoneNumber} required autoComplete="tel" />
        <Input label="License Number" name="licenseNumber" value={form.licenseNumber} onChange={set('licenseNumber')} error={fieldErrors.licenseNumber} required />
        <Select label="Specialty" name="specialty" value={form.specialty} onChange={set('specialty')} error={fieldErrors.specialty}>
          {SPECIALTIES.map((s) => <option key={s} value={s}>{s}</option>)}
        </Select>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-0 sm:gap-x-3">
          <Input label="Fee Amount" name="consultationFeeAmount" type="number" min="0" step="0.01" value={form.consultationFeeAmount} onChange={set('consultationFeeAmount')} error={fieldErrors.consultationFeeAmount} required />
          <Select label="Currency" name="consultationFeeCurrency" value={form.consultationFeeCurrency} onChange={set('consultationFeeCurrency')} error={fieldErrors.consultationFeeCurrency}>
            <option value="USD">USD</option>
            <option value="EUR">EUR</option>
            <option value="GBP">GBP</option>
          </Select>
          <Input label="Years Experience" name="yearsOfExperience" type="number" min="0" value={form.yearsOfExperience} onChange={set('yearsOfExperience')} error={fieldErrors.yearsOfExperience} required />
        </div>

        <div className="flex flex-col sm:flex-row gap-2 mt-2">
          <Button type="submit" loading={submitting} className="w-full sm:flex-1" size="lg">
            Save profile
          </Button>
          <Button type="button" variant="secondary" className="w-full sm:w-auto" onClick={() => navigate('/doctor-dashboard')}>
            Cancel
          </Button>
        </div>
      </form>

      <Card className="mt-6 min-w-0" aria-label="Working hours">
        <div className="flex items-start gap-2 mb-1">
          <Clock size={18} className="text-primary shrink-0 mt-0.5" aria-hidden="true" />
          <div className="min-w-0">
            <h2 className="text-base font-semibold text-text m-0">Working hours</h2>
            <p className="text-xs text-text-muted m-0 mt-0.5 break-words">
              Patients only see free slots inside these hours. Changes apply to new bookings immediately.
            </p>
          </div>
        </div>

        <ul className="list-none m-0 p-0 mt-4 space-y-3" data-testid="working-hours-editor">
          {WEEK_DAYS.map(({ dayOfWeek, label }) => {
            const row = schedule.find((s) => s.dayOfWeek === dayOfWeek) || {
              dayOfWeek,
              isWorkingDay: false,
              startTime: '08:00',
              endTime: '18:00',
            };
            return (
              <li
                key={dayOfWeek}
                className="rounded-lg border border-border-light p-3 sm:p-3.5 bg-surface/40"
              >
                <div className="flex flex-col sm:flex-row sm:items-center gap-2 sm:gap-3">
                  <label className="inline-flex items-center gap-2 min-h-11 sm:min-w-[7.5rem] cursor-pointer shrink-0">
                    <input
                      type="checkbox"
                      className="w-4 h-4 accent-primary"
                      checked={row.isWorkingDay}
                      onChange={(e) => updateDay(dayOfWeek, { isWorkingDay: e.target.checked })}
                      aria-label={`${label} is a working day`}
                    />
                    <span className="text-sm font-medium text-text">{label}</span>
                  </label>

                  {row.isWorkingDay ? (
                    <div className="grid grid-cols-2 gap-2 flex-1 min-w-0">
                      <div className="min-w-0">
                        <label htmlFor={`start-${dayOfWeek}`} className="sr-only">
                          {label} start time
                        </label>
                        <input
                          id={`start-${dayOfWeek}`}
                          type="time"
                          step={1800}
                          value={row.startTime}
                          onChange={(e) => updateDay(dayOfWeek, { startTime: e.target.value })}
                          className="w-full min-h-11 sm:min-h-10 px-2 py-2 rounded-md border border-border bg-white text-sm text-text focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none"
                        />
                      </div>
                      <div className="min-w-0">
                        <label htmlFor={`end-${dayOfWeek}`} className="sr-only">
                          {label} end time
                        </label>
                        <input
                          id={`end-${dayOfWeek}`}
                          type="time"
                          step={1800}
                          value={row.endTime}
                          onChange={(e) => updateDay(dayOfWeek, { endTime: e.target.value })}
                          className="w-full min-h-11 sm:min-h-10 px-2 py-2 rounded-md border border-border bg-white text-sm text-text focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none"
                        />
                      </div>
                    </div>
                  ) : (
                    <p className="text-sm text-text-muted m-0 sm:flex-1">Day off</p>
                  )}
                </div>
              </li>
            );
          })}
        </ul>

        {scheduleError && (
          <p className="mt-3 text-xs text-status-cancelled-text m-0" role="alert">{scheduleError}</p>
        )}

        <Button
          type="button"
          className="w-full mt-4"
          size="lg"
          loading={savingSchedule}
          onClick={handleSaveSchedule}
          data-testid="save-working-hours"
        >
          Save working hours
        </Button>
      </Card>

      <div className="mt-6 p-4 sm:p-5 rounded-xl border border-status-cancelled-text/20 bg-white shadow-card">
        <h3 className="text-sm font-semibold text-text m-0 mb-1">Danger zone</h3>
        <p className="text-sm text-text-muted m-0 mb-3">
          Deactivate your doctor profile. You will be signed out. Past appointments remain for records.
        </p>
        <Button
          type="button"
          variant="secondary"
          className="w-full sm:w-auto text-status-cancelled-text border-status-cancelled-text/40"
          onClick={() => setShowDelete(true)}
        >
          Delete profile
        </Button>
      </div>

      <Modal
        open={showDelete}
        onClose={() => !deleting && setShowDelete(false)}
        title="Delete doctor profile?"
        footer={
          <>
            <Button variant="secondary" className="w-full sm:w-auto" disabled={deleting} onClick={() => setShowDelete(false)}>
              Cancel
            </Button>
            <Button className="w-full sm:w-auto" loading={deleting} onClick={handleDelete}>
              Delete and sign out
            </Button>
          </>
        }
      >
        <p className="text-sm text-text m-0">
          This soft-deactivates your doctor profile and unlinks it from your account.
        </p>
      </Modal>
    </div>
  );
}
