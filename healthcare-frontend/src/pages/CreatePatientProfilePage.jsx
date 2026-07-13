import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';
import { Button, Input, Select, PageHeader } from '../components/ui';

const GENDERS = ['Male', 'Female', 'Other'];

export default function CreatePatientProfilePage() {
  const { setPatientId } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({
    firstName: '', lastName: '', email: '',
    phoneNumber: '', dateOfBirth: '', gender: 'Male',
    street: '', city: '', state: '', postalCode: '', country: '',
  });
  const [submitting, setSubmitting] = useState(false);

  const set = (field) => (e) => setForm((prev) => ({ ...prev, [field]: e.target.value }));

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      const { data } = await apiClient.post('/Patients', {
        ...form,
        dateOfBirth: new Date(form.dateOfBirth).toISOString(),
      });
      if (data.success) {
        setPatientId(data.data);
        toast.success('Patient profile created!');
        navigate('/doctors');
      } else {
        toast.error(data.errors?.join('. ') || data.message || 'Failed to create profile');
      }
    } catch (err) {
      toast.error(err.response?.data?.errors?.join('. ') || err.response?.data?.message || 'Failed to create profile');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="max-w-lg mx-auto px-4 sm:px-6 py-6 sm:py-12 w-full min-w-0">
      <PageHeader title="Create Patient Profile" subtitle="Fill in your details to start booking appointments." />

      <form
        onSubmit={handleSubmit}
        className="bg-white rounded-xl shadow-card p-4 sm:p-6 border border-border-light min-w-0"
        noValidate
        aria-label="Create patient profile form"
      >
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-0 sm:gap-x-3">
          <Input label="First Name" name="firstName" value={form.firstName} onChange={set('firstName')} required autoComplete="given-name" />
          <Input label="Last Name" name="lastName" value={form.lastName} onChange={set('lastName')} required autoComplete="family-name" />
        </div>
        <Input label="Email" name="email" type="email" value={form.email} onChange={set('email')} required autoComplete="email" />
        <Input label="Phone Number" name="phoneNumber" type="tel" value={form.phoneNumber} onChange={set('phoneNumber')} required autoComplete="tel" />
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-0 sm:gap-x-3">
          <Input label="Date of Birth" name="dateOfBirth" type="date" value={form.dateOfBirth} onChange={set('dateOfBirth')} required />
          <Select label="Gender" name="gender" value={form.gender} onChange={set('gender')}>
            {GENDERS.map((g) => <option key={g} value={g}>{g}</option>)}
          </Select>
        </div>
        <Input label="Street" name="street" value={form.street} onChange={set('street')} autoComplete="street-address" />
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-0 sm:gap-x-3">
          <Input label="City" name="city" value={form.city} onChange={set('city')} autoComplete="address-level2" />
          <Input label="State" name="state" value={form.state} onChange={set('state')} autoComplete="address-level1" />
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-0 sm:gap-x-3">
          <Input label="Postal Code" name="postalCode" value={form.postalCode} onChange={set('postalCode')} autoComplete="postal-code" />
          <Input label="Country" name="country" value={form.country} onChange={set('country')} autoComplete="country-name" />
        </div>
        <Button type="submit" loading={submitting} className="w-full mt-2" size="lg">
          Create Profile
        </Button>
      </form>
    </div>
  );
}
