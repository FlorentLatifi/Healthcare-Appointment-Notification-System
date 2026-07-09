import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';
import { Card, Button, Spinner, Input, EmptyState, PageHeader } from '../components/ui';
import { Clock, DollarSign } from 'lucide-react';

export default function DoctorsListPage() {
  const { patientId, user } = useAuth();
  const navigate = useNavigate();
  const [doctors, setDoctors] = useState([]);
  const [filter, setFilter] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        const { data } = await apiClient.get('/Doctors/accepting-patients', { params: { pageSize: 100 } });
        if (data.success) {
          setDoctors(data.data.items);
        } else {
          toast.error(data.message || 'Failed to load doctors');
        }
      } catch {
        toast.error('Failed to load doctors');
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const specialties = [...new Set(doctors.flatMap((d) => d.specialties))].sort();
  const filtered = filter
    ? doctors.filter((d) =>
        d.specialties.some((s) => s.toLowerCase().includes(filter.toLowerCase())),
      )
    : doctors;

  if (loading) return <div className="max-w-4xl mx-auto px-4 py-12"><Spinner /></div>;

  return (
    <div className="max-w-4xl mx-auto px-4 py-12">
      <PageHeader title="Available Doctors" />

      <div className="mb-4">
        <Input
          placeholder="Filter by specialty..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
        />
      </div>

      {specialties.length > 0 && (
        <div className="flex flex-wrap gap-2 mb-6">
          {specialties.map((sp) => (
            <Button
              key={sp}
              variant={filter === sp ? 'primary' : 'secondary'}
              size="sm"
              className="rounded-full"
              onClick={() => setFilter(filter === sp ? '' : sp)}
            >
              {sp}
            </Button>
          ))}
        </div>
      )}

      {filtered.length === 0 ? (
        <EmptyState message="No doctors found matching this specialty." />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {filtered.map((doc) => (
            <Card key={doc.id}>
              <h3 className="text-base font-semibold text-text mb-1">Dr. {doc.fullName}</h3>
              <p className="text-xs text-text-muted mb-2">{doc.specialties.join(', ')}</p>
              <div className="flex items-center gap-3 text-xs text-text-secondary mb-3">
                <span className="inline-flex items-center gap-1">
                  <DollarSign size={12} />
                  {doc.consultationFeeCurrency} {doc.consultationFeeAmount}
                </span>
                <span className="inline-flex items-center gap-1 bg-status-scheduled-bg text-status-scheduled-text px-2 py-0.5 rounded-full">
                  <Clock size={12} />
                  {doc.yearsOfExperience} yr{doc.yearsOfExperience !== 1 ? 's' : ''}
                </span>
              </div>
              <Button size="sm" className="w-full" onClick={() => {
                if (!patientId && user?.role === 'Patient') {
                  toast.error('Create your patient profile first');
                  navigate('/create-patient');
                  return;
                }
                navigate(`/book-appointment/${doc.id}`);
              }}>
                Book Appointment
              </Button>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
