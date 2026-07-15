import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';
import { Button, Input, Select, PageHeader, Spinner, Modal } from '../components/ui';

const GENDERS = ['Male', 'Female', 'Other'];

const emptyForm = {
  firstName: '', lastName: '', email: '',
  phoneNumber: '', dateOfBirth: '', gender: 'Male',
  street: '', city: '', state: '', postalCode: '', country: '',
};

function toDateInput(value) {
  if (!value) return '';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return String(value).slice(0, 10);
  return d.toISOString().slice(0, 10);
}

function mapApiErrors(err, data) {
  const list = data?.errors || err?.response?.data?.errors;
  if (Array.isArray(list) && list.length) return list.join('. ');
  return data?.message || err?.response?.data?.message || err?.message || 'Request failed';
}

export default function EditPatientProfilePage() {
  const { patientId, logout } = useAuth();
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
    if (!patientId) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const { data } = await apiClient.get(`/Patients/${patientId}`);
        if (cancelled) return;
        if (!data.success || !data.data) {
          toast.error(data.message || 'Failed to load profile');
          return;
        }
        const p = data.data;
        setForm({
          firstName: p.firstName || '',
          lastName: p.lastName || '',
          email: p.email || '',
          phoneNumber: p.phoneNumber || '',
          dateOfBirth: toDateInput(p.dateOfBirth),
          gender: p.gender || 'Male',
          street: p.street || '',
          city: p.city || '',
          state: p.state || '',
          postalCode: p.postalCode || '',
          country: p.country || '',
        });
      } catch (err) {
        if (!cancelled) toast.error(mapApiErrors(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [patientId]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!patientId) return;
    setSubmitting(true);
    setFieldErrors({});
    try {
      const { data } = await apiClient.put(`/Patients/${patientId}`, {
        ...form,
        dateOfBirth: new Date(form.dateOfBirth).toISOString(),
      });
      if (data.success) {
        toast.success('Profile updated');
        navigate('/dashboard');
      } else {
        toast.error(mapApiErrors(null, data));
      }
    } catch (err) {
      const api = err.response?.data;
      // ProblemDetails-style field errors from ValidationFilter
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
    if (!patientId) return;
    setDeleting(true);
    try {
      await apiClient.delete(`/Patients/${patientId}`);
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

  if (!patientId) {
    return (
      <div className="max-w-lg mx-auto px-4 sm:px-6 py-6 sm:py-12 w-full min-w-0">
        <PageHeader title="Edit Patient Profile" subtitle="No linked patient profile found." />
        <Button className="w-full" onClick={() => navigate('/create-patient')}>Create Profile</Button>
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
        title="Edit Patient Profile"
        subtitle="Update your contact and personal details."
      />

      <form
        onSubmit={handleSubmit}
        className="bg-white rounded-xl shadow-card p-4 sm:p-6 border border-border-light min-w-0"
        noValidate
        aria-label="Edit patient profile form"
      >
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-0 sm:gap-x-3">
          <Input label="First Name" name="firstName" value={form.firstName} onChange={set('firstName')} error={fieldErrors.firstName} required autoComplete="given-name" />
          <Input label="Last Name" name="lastName" value={form.lastName} onChange={set('lastName')} error={fieldErrors.lastName} required autoComplete="family-name" />
        </div>
        <Input label="Email" name="email" type="email" value={form.email} onChange={set('email')} error={fieldErrors.email} required autoComplete="email" />
        <Input label="Phone Number" name="phoneNumber" type="tel" value={form.phoneNumber} onChange={set('phoneNumber')} error={fieldErrors.phoneNumber} required autoComplete="tel" />
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-0 sm:gap-x-3">
          <Input label="Date of Birth" name="dateOfBirth" type="date" value={form.dateOfBirth} onChange={set('dateOfBirth')} error={fieldErrors.dateOfBirth} required />
          <Select label="Gender" name="gender" value={form.gender} onChange={set('gender')} error={fieldErrors.gender}>
            {GENDERS.map((g) => <option key={g} value={g}>{g}</option>)}
          </Select>
        </div>
        <Input label="Street" name="street" value={form.street} onChange={set('street')} error={fieldErrors.street} required autoComplete="street-address" />
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-0 sm:gap-x-3">
          <Input label="City" name="city" value={form.city} onChange={set('city')} error={fieldErrors.city} required autoComplete="address-level2" />
          <Input label="State" name="state" value={form.state} onChange={set('state')} error={fieldErrors.state} required autoComplete="address-level1" />
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-0 sm:gap-x-3">
          <Input label="Postal Code" name="postalCode" value={form.postalCode} onChange={set('postalCode')} error={fieldErrors.postalCode} required autoComplete="postal-code" />
          <Input label="Country" name="country" value={form.country} onChange={set('country')} error={fieldErrors.country} required autoComplete="country-name" />
        </div>

        <div className="flex flex-col sm:flex-row gap-2 mt-2">
          <Button type="submit" loading={submitting} className="w-full sm:flex-1" size="lg">
            Save changes
          </Button>
          <Button type="button" variant="secondary" className="w-full sm:w-auto" onClick={() => navigate('/dashboard')}>
            Cancel
          </Button>
        </div>
      </form>

      <div className="mt-6 p-4 sm:p-5 rounded-xl border border-status-cancelled-text/20 bg-white shadow-card">
        <h3 className="text-sm font-semibold text-text m-0 mb-1">Danger zone</h3>
        <p className="text-sm text-text-muted m-0 mb-3">
          Deactivate your patient profile. You will be signed out. Historical appointments are retained.
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
        title="Delete patient profile?"
        footer={
          <>
            <Button variant="secondary" className="w-full sm:w-auto" disabled={deleting} onClick={() => setShowDelete(false)}>
              Cancel
            </Button>
            <Button
              className="w-full sm:w-auto"
              loading={deleting}
              onClick={handleDelete}
            >
              Delete and sign out
            </Button>
          </>
        }
      >
        <p className="text-sm text-text m-0">
          This soft-deletes your profile and unlinks it from your account. You cannot book appointments until you create a new profile.
        </p>
      </Modal>
    </div>
  );
}
