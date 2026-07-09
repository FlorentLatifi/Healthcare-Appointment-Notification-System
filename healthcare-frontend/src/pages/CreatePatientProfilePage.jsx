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
    <div className="max-w-lg mx-auto px-4 py-12">
      <PageHeader title="Create Patient Profile" subtitle="Fill in your details to start booking appointments." />

      <form onSubmit={handleSubmit} className="bg-white rounded-xl shadow-card p-6 border border-border-light">
        <div className="grid grid-cols-2 gap-3">
          <Input label="First Name" value={form.firstName} onChange={set('firstName')} required />
          <Input label="Last Name" value={form.lastName} onChange={set('lastName')} required />
        </div>
        <Input label="Email" type="email" value={form.email} onChange={set('email')} required />
        <Input label="Phone Number" value={form.phoneNumber} onChange={set('phoneNumber')} required />
        <div className="grid grid-cols-2 gap-3">
          <Input label="Date of Birth" type="date" value={form.dateOfBirth} onChange={set('dateOfBirth')} required />
          <Select label="Gender" value={form.gender} onChange={set('gender')}>
            {GENDERS.map((g) => <option key={g}>{g}</option>)}
          </Select>
        </div>
        <Input label="Street" value={form.street} onChange={set('street')} />
        <div className="grid grid-cols-2 gap-3">
          <Input label="City" value={form.city} onChange={set('city')} />
          <Input label="State" value={form.state} onChange={set('state')} />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Input label="Postal Code" value={form.postalCode} onChange={set('postalCode')} />
          <Input label="Country" value={form.country} onChange={set('country')} />
        </div>
        <Button type="submit" loading={submitting} className="w-full mt-2" size="lg">
          Create Profile
        </Button>
      </form>
    </div>
  );
}
