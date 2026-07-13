import { useState, useEffect, useCallback, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import apiClient from '../services/apiClient';
import { useAuth } from '../context/AuthContext';
import {
  Card,
  Button,
  PageHeader,
  Skeleton,
  EmptyState,
  Badge,
} from '../components/ui';
import {
  LogOut,
  ArrowRight,
  Clock,
  User,
  Mail,
  Phone,
  MapPin,
  CalendarClock,
  CheckCircle2,
  XCircle,
  AlertCircle,
  Stethoscope,
} from 'lucide-react';
import { APPOINTMENT_STATUS } from '../constants/appointmentStatus';

const ROLE_ACTIONS = {
  Patient: [
    { title: 'Browse Doctors', desc: 'Find available doctors and book appointments', path: '/doctors', needsPatientId: true },
    { title: 'My Appointments', desc: 'View and manage your appointments', path: '/my-appointments', needsPatientId: true },
  ],
  Doctor: [
    { title: 'Doctor Dashboard', desc: 'Manage appointments, confirm, complete, mark no-show', path: '/doctor-dashboard' },
  ],
  Admin: [
    { title: 'Admin Dashboard', desc: 'Manage doctors and patients', path: '/admin' },
  ],
};

const UPCOMING_STATUSES = new Set([
  APPOINTMENT_STATUS.PENDING,
  APPOINTMENT_STATUS.CONFIRMED,
]);

function isUpcoming(appt) {
  if (!UPCOMING_STATUSES.has(appt.status)) return false;
  if (!appt.scheduledDate) return true;
  // Treat end of scheduled day as still upcoming if status is active
  const scheduled = new Date(`${appt.scheduledDate}T23:59:59`);
  if (Number.isNaN(scheduled.getTime())) return true;
  return scheduled >= new Date(new Date().toDateString());
}

function DashboardSkeleton() {
  return (
    <div className="space-y-6" data-testid="dashboard-skeleton">
      <div className="space-y-2">
        <Skeleton width="40%" height="28px" />
        <Skeleton width="55%" height="16px" />
      </div>
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4">
        {[1, 2, 3, 4].map((i) => (
          <Skeleton key={i} variant="card" height="96px" />
        ))}
      </div>
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 sm:gap-6">
        <Skeleton variant="card" height="200px" className="lg:col-span-1" />
        <div className="lg:col-span-2 space-y-3">
          <Skeleton width="30%" height="18px" />
          <Skeleton variant="card" height="100px" />
          <Skeleton variant="card" height="100px" />
        </div>
      </div>
    </div>
  );
}

function StatCard({ label, value, icon, accent = 'text-primary' }) {
  return (
    <Card className="!p-3 sm:!p-4">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="text-xs text-text-muted uppercase tracking-wider truncate">{label}</p>
          <p className="text-2xl sm:text-3xl font-semibold text-text mt-1 tabular-nums">{value}</p>
        </div>
        <div className={`shrink-0 w-9 h-9 rounded-lg bg-surface flex items-center justify-center ${accent}`}>
          {icon}
        </div>
      </div>
    </Card>
  );
}

function AppointmentRow({ appt, role }) {
  const counterpart =
    role === 'Doctor'
      ? appt.patient?.fullName || 'Patient'
      : `Dr. ${appt.doctor?.fullName || 'Doctor'}`;

  return (
    <Card>
      <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-2 mb-2">
        <div className="flex flex-wrap items-center gap-2 min-w-0">
          <span className="text-sm font-semibold text-text break-all">{appt.referenceCode}</span>
          <Badge status={appt.status} />
        </div>
        <span className="text-xs text-text-muted shrink-0">{appt.scheduledDate}</span>
      </div>
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-text-muted">
        <span className="inline-flex items-center gap-1 min-w-0">
          <User size={12} className="shrink-0" />
          <span className="truncate">{counterpart}</span>
        </span>
        <span className="inline-flex items-center gap-1">
          <Clock size={12} className="shrink-0" />
          {appt.scheduledTimeFormatted}
        </span>
      </div>
      {appt.reason && (
        <p className="text-sm text-text mt-2 break-words line-clamp-2">{appt.reason}</p>
      )}
    </Card>
  );
}

function ProfileSummary({ profile, role }) {
  if (!profile) return null;

  const isDoctor = role === 'Doctor';
  const title = isDoctor ? `Dr. ${profile.fullName}` : profile.fullName;

  return (
    <Card>
      <h3 className="text-sm font-medium text-text-secondary uppercase tracking-wider mb-3">
        Profile Summary
      </h3>
      <div className="flex items-start gap-3 mb-4">
        <div className="w-10 h-10 rounded-full bg-surface flex items-center justify-center shrink-0">
          {isDoctor ? (
            <Stethoscope size={18} className="text-primary" />
          ) : (
            <User size={18} className="text-primary" />
          )}
        </div>
        <div className="min-w-0">
          <p className="text-sm font-semibold text-text break-words">{title}</p>
          {isDoctor && profile.specialties?.length > 0 && (
            <p className="text-xs text-text-muted mt-0.5 break-words">
              {profile.specialties.join(', ')}
            </p>
          )}
          {!isDoctor && profile.gender && (
            <p className="text-xs text-text-muted mt-0.5">
              {profile.gender}
              {profile.age != null ? ` · ${profile.age} yrs` : ''}
            </p>
          )}
        </div>
      </div>
      <div className="space-y-2 text-sm text-text-muted">
        {profile.email && (
          <p className="inline-flex items-start gap-2 w-full min-w-0">
            <Mail size={14} className="shrink-0 mt-0.5" />
            <span className="break-all">{profile.email}</span>
          </p>
        )}
        {profile.phoneNumber && (
          <p className="inline-flex items-start gap-2 w-full min-w-0">
            <Phone size={14} className="shrink-0 mt-0.5" />
            <span className="break-words">{profile.phoneNumber}</span>
          </p>
        )}
        {!isDoctor && profile.address && (
          <p className="inline-flex items-start gap-2 w-full min-w-0">
            <MapPin size={14} className="shrink-0 mt-0.5" />
            <span className="break-words">{profile.address}</span>
          </p>
        )}
        {isDoctor && (
          <p className="text-xs text-text-muted">
            {profile.yearsOfExperience != null && (
              <span>{profile.yearsOfExperience} years experience</span>
            )}
            {profile.isAcceptingPatients != null && (
              <span>
                {profile.yearsOfExperience != null ? ' · ' : ''}
                {profile.isAcceptingPatients ? 'Accepting patients' : 'Not accepting patients'}
              </span>
            )}
          </p>
        )}
      </div>
    </Card>
  );
}

export default function DashboardPage() {
  const { user, logout, patientId, doctorId } = useAuth();
  const navigate = useNavigate();

  const [profile, setProfile] = useState(null);
  const [appointments, setAppointments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const role = user?.role;
  const linkedId = role === 'Patient' ? patientId : role === 'Doctor' ? doctorId : null;
  const needsLinkedData = role === 'Patient' || role === 'Doctor';

  const fetchDashboard = useCallback(async () => {
    // Admin (or unlinked roles) — no remote dashboard data required
    if (!needsLinkedData) {
      setLoading(false);
      setError(null);
      setProfile(null);
      setAppointments([]);
      return;
    }

    // Patient/Doctor without profile link — show setup CTA, not an error
    if (!linkedId) {
      setLoading(false);
      setError(null);
      setProfile(null);
      setAppointments([]);
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const profileUrl =
        role === 'Patient' ? `/Patients/${linkedId}` : `/Doctors/${linkedId}`;
      const apptsUrl =
        role === 'Patient'
          ? `/Appointments/patient/${linkedId}`
          : `/Appointments/doctor/${linkedId}`;

      const [profileRes, apptsRes] = await Promise.all([
        apiClient.get(profileUrl),
        apiClient.get(apptsUrl, { params: { pageSize: 50 } }),
      ]);

      if (!profileRes.data?.success) {
        throw new Error(profileRes.data?.message || 'Failed to load profile');
      }
      if (!apptsRes.data?.success) {
        throw new Error(apptsRes.data?.message || 'Failed to load appointments');
      }

      setProfile(profileRes.data.data);
      setAppointments(apptsRes.data.data?.items ?? []);
    } catch (err) {
      const message =
        err.response?.data?.message ||
        err.response?.data?.errors?.join?.('. ') ||
        err.message ||
        'Failed to load dashboard';
      setError(message);
      setProfile(null);
      setAppointments([]);
    } finally {
      setLoading(false);
    }
  }, [needsLinkedData, linkedId, role]);

  useEffect(() => {
    fetchDashboard();
  }, [fetchDashboard]);

  const upcoming = useMemo(
    () => appointments.filter(isUpcoming),
    [appointments],
  );

  const stats = useMemo(() => {
    const upcomingCount = upcoming.length;
    const completed = appointments.filter((a) => a.status === APPOINTMENT_STATUS.COMPLETED).length;
    const cancelled = appointments.filter((a) => a.status === APPOINTMENT_STATUS.CANCELLED).length;
    const pending = appointments.filter((a) => a.status === APPOINTMENT_STATUS.PENDING).length;
    return {
      upcoming: upcomingCount,
      completed,
      cancelled,
      pending,
      total: appointments.length,
    };
  }, [appointments, upcoming]);

  const displayName = useMemo(() => {
    if (profile?.fullName) {
      return role === 'Doctor' ? `Dr. ${profile.fullName}` : profile.fullName;
    }
    if (profile?.firstName) {
      return profile.firstName;
    }
    return user?.username || 'there';
  }, [profile, role, user?.username]);

  const actions = useMemo(() => {
    const list = [...(ROLE_ACTIONS[role] || [])];
    if (role === 'Patient' && !patientId) {
      list.unshift({
        title: 'Create Patient Profile',
        desc: 'Set up your profile to start booking',
        path: '/create-patient',
      });
    }
    return list;
  }, [role, patientId]);

  const handleAction = (a) => {
    if (a.needsPatientId && !patientId) {
      toast.error('Create your patient profile first');
      return navigate('/create-patient');
    }
    navigate(a.path);
  };

  return (
    <div className="max-w-6xl mx-auto px-4 sm:px-6 py-6 sm:py-10 lg:py-12">
      <PageHeader
        title="Dashboard"
        subtitle={
          <>
            Welcome, <span className="font-medium text-text">{displayName}</span>
          </>
        }
      />

      {loading && <DashboardSkeleton />}

      {!loading && error && (
        <EmptyState
          icon={<AlertCircle size={24} className="text-status-cancelled-text" />}
          message={error || 'Failed to load dashboard data.'}
          actionLabel="Retry"
          onAction={fetchDashboard}
        />
      )}

      {!loading && !error && needsLinkedData && !linkedId && (
        <div className="space-y-6">
          <EmptyState
            icon={<User size={24} className="text-text-muted" />}
            message={
              role === 'Patient'
                ? 'Create your patient profile to see appointments and stats.'
                : 'Link your doctor profile to see your schedule and stats.'
            }
            actionLabel={role === 'Patient' ? 'Create Patient Profile' : 'Go to Doctor Dashboard'}
            onAction={() =>
              navigate(role === 'Patient' ? '/create-patient' : '/doctor-dashboard')
            }
          />
          {actions.length > 0 && (
            <QuickActions actions={actions} onAction={handleAction} />
          )}
        </div>
      )}

      {!loading && !error && (!needsLinkedData || linkedId) && (
        <div className="space-y-6 sm:space-y-8">
          {needsLinkedData && linkedId && (
            <>
              {/* Quick stats */}
              <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4">
                <StatCard
                  label="Upcoming"
                  value={stats.upcoming}
                  icon={<CalendarClock size={18} />}
                  accent="text-primary"
                />
                <StatCard
                  label="Pending"
                  value={stats.pending}
                  icon={<Clock size={18} />}
                  accent="text-status-pending-text"
                />
                <StatCard
                  label="Completed"
                  value={stats.completed}
                  icon={<CheckCircle2 size={18} />}
                  accent="text-status-confirmed-text"
                />
                <StatCard
                  label="Cancelled"
                  value={stats.cancelled}
                  icon={<XCircle size={18} />}
                  accent="text-status-cancelled-text"
                />
              </div>

              {/* Profile + upcoming appointments */}
              <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 sm:gap-6">
                <div className="lg:col-span-1">
                  <ProfileSummary profile={profile} role={role} />
                </div>
                <div className="lg:col-span-2 space-y-3">
                  <div className="flex flex-col xs:flex-row sm:flex-row sm:items-center sm:justify-between gap-2">
                    <h3 className="text-sm font-medium text-text-secondary uppercase tracking-wider">
                      Upcoming Appointments
                    </h3>
                    {role === 'Patient' && (
                      <Button
                        variant="ghost"
                        size="sm"
                        className="w-full sm:w-auto justify-center"
                        onClick={() => navigate('/my-appointments')}
                      >
                        View all
                      </Button>
                    )}
                    {role === 'Doctor' && (
                      <Button
                        variant="ghost"
                        size="sm"
                        className="w-full sm:w-auto justify-center"
                        onClick={() => navigate('/doctor-dashboard')}
                      >
                        Manage
                      </Button>
                    )}
                  </div>

                  {upcoming.length === 0 ? (
                    <EmptyState
                      icon={<CalendarClock size={24} className="text-text-muted" />}
                      message="No upcoming appointments."
                      actionLabel={
                        role === 'Patient' ? 'Browse Doctors' : 'Open Doctor Dashboard'
                      }
                      onAction={() =>
                        navigate(role === 'Patient' ? '/doctors' : '/doctor-dashboard')
                      }
                    />
                  ) : (
                    <div className="space-y-3">
                      {upcoming.slice(0, 5).map((appt) => (
                        <AppointmentRow key={appt.id} appt={appt} role={role} />
                      ))}
                    </div>
                  )}
                </div>
              </div>
            </>
          )}

          {actions.length > 0 && (
            <QuickActions actions={actions} onAction={handleAction} />
          )}
        </div>
      )}

      <div className="mt-8 pt-6 border-t border-border-light">
        <Button
          variant="ghost"
          className="w-full sm:w-auto"
          leftIcon={<LogOut size={14} />}
          onClick={() => {
            logout();
            navigate('/login', { replace: true });
          }}
        >
          Logout
        </Button>
      </div>
    </div>
  );
}

function QuickActions({ actions, onAction }) {
  return (
    <div>
      <h3 className="text-sm font-medium text-text-secondary uppercase tracking-wider mb-3">
        Quick Actions
      </h3>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        {actions.map((a) => (
          <Card key={a.path} hover onClick={() => onAction(a)}>
            <div className="flex items-center justify-between gap-3">
              <div className="min-w-0">
                <h4 className="text-sm font-medium text-text">{a.title}</h4>
                <p className="text-xs text-text-muted mt-0.5 break-words">{a.desc}</p>
              </div>
              <ArrowRight size={16} className="text-text-muted shrink-0" />
            </div>
          </Card>
        ))}
      </div>
    </div>
  );
}
