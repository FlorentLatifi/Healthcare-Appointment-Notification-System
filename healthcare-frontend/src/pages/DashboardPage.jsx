import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
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
  Calendar,
  Stethoscope,
  AlertCircle,
  PlusCircle,
  List,
  UserCircle,
} from 'lucide-react';
import { APPOINTMENT_STATUS } from '../constants/appointmentStatus';

const UPCOMING_STATUSES = new Set([
  APPOINTMENT_STATUS.PENDING,
  APPOINTMENT_STATUS.CONFIRMED,
]);

function hasLinkedId(id) {
  return id != null && Number(id) > 0;
}

function parseApptDate(appt) {
  if (appt.scheduledTime) {
    const d = new Date(appt.scheduledTime);
    if (!Number.isNaN(d.getTime())) return d;
  }
  if (appt.scheduledDate) {
    const d = new Date(`${appt.scheduledDate}T12:00:00`);
    if (!Number.isNaN(d.getTime())) return d;
  }
  return null;
}

function isUpcoming(appt) {
  if (!UPCOMING_STATUSES.has(appt.status)) return false;
  const scheduled = parseApptDate(appt);
  if (!scheduled) return true;
  const startOfToday = new Date();
  startOfToday.setHours(0, 0, 0, 0);
  return scheduled >= startOfToday;
}

function formatNextDate(appt) {
  if (!appt) return '—';
  if (appt.scheduledDate) return appt.scheduledDate;
  const d = parseApptDate(appt);
  if (!d) return '—';
  return d.toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

function DashboardSkeleton() {
  return (
    <div className="space-y-6" data-testid="dashboard-skeleton" aria-busy="true">
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
          <p className="text-xl sm:text-2xl lg:text-3xl font-semibold text-text mt-1 tabular-nums break-words">
            {value}
          </p>
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
  const specialty =
    role !== 'Doctor' && Array.isArray(appt.doctor?.specialties)
      ? appt.doctor.specialties.join(', ')
      : appt.doctor?.specialty || null;

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
        {specialty && (
          <span className="inline-flex items-center gap-1 min-w-0">
            <Stethoscope size={12} className="shrink-0" />
            <span className="truncate">{specialty}</span>
          </span>
        )}
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

function QuickActions({ actions, onAction }) {
  return (
    <section aria-labelledby="quick-actions-heading">
      <h3
        id="quick-actions-heading"
        className="text-sm font-medium text-text-secondary uppercase tracking-wider mb-3"
      >
        Quick Actions
      </h3>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
        {actions.map((a) => (
          <Card
            key={a.path + a.title}
            hover
            onClick={() => onAction(a)}
            aria-label={`${a.title}. ${a.desc}`}
          >
            <div className="flex items-center justify-between gap-3 min-w-0">
              <div className="flex items-start gap-3 min-w-0">
                {a.icon && (
                  <div className="shrink-0 w-9 h-9 rounded-lg bg-surface flex items-center justify-center text-primary">
                    {a.icon}
                  </div>
                )}
                <div className="min-w-0">
                  <h4 className="text-sm font-medium text-text">{a.title}</h4>
                  <p className="text-xs text-text-muted mt-0.5 break-words">{a.desc}</p>
                </div>
              </div>
              <ArrowRight size={16} className="text-text-muted shrink-0" aria-hidden="true" />
            </div>
          </Card>
        ))}
      </div>
    </section>
  );
}

export default function DashboardPage() {
  const { user, logout, patientId, doctorId, sessionReady, refreshSession } = useAuth();
  const navigate = useNavigate();

  const [profile, setProfile] = useState(null);
  const [appointments, setAppointments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const claimRefreshAttempted = useRef(false);

  const role = user?.role;
  const linkedId =
    role === 'Patient'
      ? (hasLinkedId(patientId) ? Number(patientId) : null)
      : role === 'Doctor'
        ? (hasLinkedId(doctorId) ? Number(doctorId) : null)
        : null;
  const needsLinkedData = role === 'Patient' || role === 'Doctor';

  // If patient just linked a profile but JWT claims are stale, try one refresh.
  useEffect(() => {
    if (!sessionReady || role !== 'Patient' || hasLinkedId(patientId)) return;
    if (claimRefreshAttempted.current) return;
    claimRefreshAttempted.current = true;
    refreshSession().catch(() => {
      // Still unlinked — empty state CTA will guide them
    });
  }, [sessionReady, role, patientId, refreshSession]);

  const fetchDashboard = useCallback(async () => {
    if (!sessionReady) return;

    if (!needsLinkedData) {
      setLoading(false);
      setError(null);
      setProfile(null);
      setAppointments([]);
      return;
    }

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
  }, [sessionReady, needsLinkedData, linkedId, role]);

  useEffect(() => {
    fetchDashboard();
  }, [fetchDashboard]);

  const upcoming = useMemo(() => {
    return appointments
      .filter(isUpcoming)
      .sort((a, b) => {
        const da = parseApptDate(a)?.getTime() ?? Number.MAX_SAFE_INTEGER;
        const db = parseApptDate(b)?.getTime() ?? Number.MAX_SAFE_INTEGER;
        return da - db;
      });
  }, [appointments]);

  const stats = useMemo(() => {
    const completed = appointments.filter((a) => a.status === APPOINTMENT_STATUS.COMPLETED).length;
    const next = upcoming[0] || null;
    return {
      upcoming: upcoming.length,
      completed,
      nextDate: formatNextDate(next),
      total: appointments.length,
    };
  }, [appointments, upcoming]);

  const firstName = useMemo(() => {
    if (profile?.firstName) return profile.firstName;
    if (profile?.fullName) return profile.fullName.split(/\s+/)[0];
    return null;
  }, [profile]);

  const displayName = useMemo(() => {
    if (role === 'Doctor' && profile?.fullName) return `Dr. ${profile.fullName}`;
    if (firstName) return firstName;
    if (profile?.fullName) return profile.fullName;
    if (user?.username) return user.username;
    return null;
  }, [profile, firstName, role, user?.username]);

  const greetingSubtitle = useMemo(() => {
    if (displayName) {
      return (
        <>
          Welcome back, <span className="font-medium text-text">{displayName}</span>!
        </>
      );
    }
    return 'Welcome back!';
  }, [displayName]);

  const actions = useMemo(() => {
    if (role === 'Patient') {
      const list = [];
      if (!hasLinkedId(patientId)) {
        list.push({
          title: 'Create Patient Profile',
          desc: 'Set up your profile to start booking',
          path: '/create-patient',
          icon: <UserCircle size={18} />,
        });
      } else {
        list.push(
          {
            title: 'Book New Appointment',
            desc: 'Find a doctor and choose a time slot',
            path: '/doctors',
            needsPatientId: true,
            icon: <PlusCircle size={18} />,
          },
          {
            title: 'View My Appointments',
            desc: 'See history, pay, or cancel',
            path: '/my-appointments',
            needsPatientId: true,
            icon: <List size={18} />,
          },
        );
      }
      return list;
    }
    if (role === 'Doctor') {
      return [
        {
          title: 'Doctor Dashboard',
          desc: 'Manage appointments, confirm, complete, mark no-show',
          path: '/doctor-dashboard',
          icon: <Stethoscope size={18} />,
        },
      ];
    }
    if (role === 'Admin') {
      return [
        {
          title: 'Admin Dashboard',
          desc: 'Manage doctors and patients',
          path: '/admin',
          icon: <User size={18} />,
        },
      ];
    }
    return [];
  }, [role, patientId]);

  const handleAction = (a) => {
    if (a.needsPatientId && !hasLinkedId(patientId)) {
      toast.error('Create your patient profile first');
      return navigate('/create-patient');
    }
    navigate(a.path);
  };

  // Session restore in progress
  if (!sessionReady) {
    return (
      <div className="max-w-6xl mx-auto px-4 sm:px-6 py-6 sm:py-10 lg:py-12 w-full min-w-0">
        <PageHeader title="Dashboard" subtitle="Loading your session…" />
        <DashboardSkeleton />
      </div>
    );
  }

  return (
    <div className="max-w-6xl mx-auto px-4 sm:px-6 py-6 sm:py-10 lg:py-12 w-full min-w-0">
      <PageHeader title="Dashboard" subtitle={greetingSubtitle} />

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
                ? 'Create your patient profile to see appointments and stats on your dashboard.'
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
              <div
                className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4"
                data-testid="dashboard-stats"
              >
                <StatCard
                  label="Upcoming Appointments"
                  value={stats.upcoming}
                  icon={<CalendarClock size={18} />}
                  accent="text-primary"
                />
                <StatCard
                  label="Completed"
                  value={stats.completed}
                  icon={<CheckCircle2 size={18} />}
                  accent="text-status-confirmed-text"
                />
                <StatCard
                  label="Next Appointment"
                  value={stats.nextDate}
                  icon={<Calendar size={18} />}
                  accent="text-status-pending-text"
                />
                <StatCard
                  label="Total Appointments"
                  value={stats.total}
                  icon={<List size={18} />}
                  accent="text-text-muted"
                />
              </div>

              <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 sm:gap-6">
                <div className="lg:col-span-1 order-2 lg:order-1">
                  <ProfileSummary profile={profile} role={role} />
                </div>
                <div className="lg:col-span-2 space-y-3 order-1 lg:order-2">
                  <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
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
                      message={
                        role === 'Patient'
                          ? 'No upcoming appointments. Book a visit with a doctor to get started.'
                          : 'No upcoming appointments on your schedule.'
                      }
                      actionLabel={
                        role === 'Patient' ? 'Book Appointment' : 'Open Doctor Dashboard'
                      }
                      onAction={() =>
                        navigate(role === 'Patient' ? '/doctors' : '/doctor-dashboard')
                      }
                    />
                  ) : (
                    <div className="space-y-3" data-testid="upcoming-appointments">
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
