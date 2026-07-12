import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useAuth } from '../context/AuthContext';
import { Card, Button, PageHeader } from '../components/ui';
import { LogOut, ArrowRight } from 'lucide-react';

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

export default function DashboardPage() {
  const { user, logout, patientId } = useAuth();
  const navigate = useNavigate();

  const actions = [...(ROLE_ACTIONS[user?.role] || [])];
  if (user?.role === 'Patient' && !patientId) {
    actions.unshift({
      title: 'Create Patient Profile', desc: 'Set up your profile to start booking', path: '/create-patient',
    });
  }

  return (
    <div className="max-w-lg mx-auto px-4 sm:px-6 py-6 sm:py-12">
      <PageHeader
        title="Dashboard"
        subtitle={<>Welcome, <span className="font-medium text-text">{user?.username}</span></>}
      />

      {actions.length > 0 && (
        <div>
          <h3 className="text-sm font-medium text-text-secondary uppercase tracking-wider mb-3">Quick Actions</h3>
          <div className="space-y-3">
            {actions.map((a) => (
              <Card key={a.path} hover onClick={() => {
                if (a.needsPatientId && !patientId) {
                  toast.error('Create your patient profile first');
                  return navigate('/create-patient');
                }
                navigate(a.path);
              }}>
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
      )}

      <div className="mt-8 pt-6 border-t border-border-light">
        <Button variant="ghost" className="w-full sm:w-auto" leftIcon={<LogOut size={14} />} onClick={() => { logout(); navigate('/login', { replace: true }); }}>
          Logout
        </Button>
      </div>
    </div>
  );
}
