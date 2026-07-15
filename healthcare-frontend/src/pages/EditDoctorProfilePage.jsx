import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';
import { Button, Input, Select, PageHeader, Spinner, Modal } from '../components/ui';
import { SPECIALTIES, DEFAULT_SPECIALTY } from '../constants/specialties';

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

function mapApiErrors(err, data) {
  const list = data?.errors || err?.response?.data?.errors;
  if (Array.isArray(list) && list.length) return list.join('. ');
  return data?.message || err?.response?.data?.message || err?.message || 'Request failed';
}

export default function EditDoctorProfilePage() {
  const { doctorId, logout } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState(emptyForm);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [showDelete, setShowDelete] = useState(false);
  const [fieldErrors, setFieldErrors] = useState({});

  const set = (field) => (e) => {
    setForm((prev) => ({ ...prev, [field]: e.target.value }));
    setFieldErrors((prev) => ({ ...prev, [field]: undefined }));
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
        const { data } = await apiClient.get(`/Doctors/${doctorId}`);
        if (cancelled) return;
        if (!data.success || !data.data) {
          toast.error(data.message || 'Failed to load profile');
          return;
        }
        const d = data.data;
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
        subtitle="Update your professional and contact details."
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
            Save changes
          </Button>
          <Button type="button" variant="secondary" className="w-full sm:w-auto" onClick={() => navigate('/doctor-dashboard')}>
            Cancel
          </Button>
        </div>
      </form>

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
