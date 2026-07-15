import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';
import { Card, Button, Spinner, Input, EmptyState, PageHeader } from '../components/ui';
import { Clock, DollarSign } from 'lucide-react';
import { formatWeeklyHoursSummary } from '../utils/bookingSlots';

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
          const items = data.data.items || [];
          // Prefer schedule embedded on doctor DTO; fall back to /schedule for older responses.
          const enriched = await Promise.all(
            items.map(async (doc) => {
              if (Array.isArray(doc.weeklySchedule) && doc.weeklySchedule.length) {
                return doc;
              }
              try {
                const sched = await apiClient.get(`/Doctors/${doc.id}/schedule`);
                if (sched.data?.success) {
                  return { ...doc, weeklySchedule: sched.data.data?.weeklySchedule || [] };
                }
              } catch {
                // ignore — card still works without hours
              }
              return doc;
            }),
          );
          setDoctors(enriched);
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

  if (loading) {
    return (
      <div className="max-w-4xl mx-auto px-4 sm:px-6 py-6 sm:py-12 w-full min-w-0">
        <div role="status" aria-label="Loading doctors" className="flex justify-center py-8">
          <Spinner />
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto px-4 sm:px-6 py-6 sm:py-12 w-full min-w-0">
      <PageHeader title="Available Doctors" />

      <div className="mb-4 min-w-0">
        <Input
          label="Filter by specialty"
          id="doctor-specialty-filter"
          placeholder="e.g. Cardiology..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          autoComplete="off"
        />
      </div>

      {specialties.length > 0 && (
        <div
          className="flex flex-wrap gap-2 mb-6"
          role="group"
          aria-label="Specialty filters"
        >
          {specialties.map((sp) => (
            <Button
              key={sp}
              variant={filter === sp ? 'primary' : 'secondary'}
              size="sm"
              className="rounded-full"
              aria-pressed={filter === sp}
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
            <Card key={doc.id} className="min-w-0 flex flex-col">
              <h3 className="text-base font-semibold text-text mb-1 break-words">Dr. {doc.fullName}</h3>
              <p className="text-xs text-text-muted mb-2 break-words">{doc.specialties.join(', ')}</p>
              <div className="flex flex-wrap items-center gap-2 sm:gap-3 text-xs text-text-secondary mb-2">
                <span className="inline-flex items-center gap-1">
                  <DollarSign size={12} aria-hidden="true" />
                  {doc.consultationFeeCurrency} {doc.consultationFeeAmount}
                </span>
                <span className="inline-flex items-center gap-1 bg-status-scheduled-bg text-status-scheduled-text px-2 py-0.5 rounded-full">
                  <Clock size={12} aria-hidden="true" />
                  {doc.yearsOfExperience} yr{doc.yearsOfExperience !== 1 ? 's' : ''}
                </span>
              </div>
              {formatWeeklyHoursSummary(doc.weeklySchedule) && (
                <p className="text-xs text-text-muted m-0 mb-3 break-words inline-flex items-start gap-1">
                  <Clock size={12} className="shrink-0 mt-0.5" aria-hidden="true" />
                  <span>{formatWeeklyHoursSummary(doc.weeklySchedule)}</span>
                </p>
              )}
              <Button
                size="sm"
                className="w-full mt-auto"
                onClick={() => {
                  if (!patientId && user?.role === 'Patient') {
                    toast.error('Create your patient profile first');
                    navigate('/create-patient');
                    return;
                  }
                  navigate(`/book-appointment/${doc.id}`);
                }}
              >
                Book Appointment
              </Button>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
