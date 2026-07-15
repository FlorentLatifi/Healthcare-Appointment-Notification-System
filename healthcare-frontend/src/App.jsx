import { lazy, Suspense, useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate, useNavigate } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { AuthProvider } from './context/AuthContext';
import ErrorBoundary from './components/ErrorBoundary';
import ProtectedRoute from './components/ProtectedRoute';
import Layout from './components/Layout';
import { setSessionExpiredHandler } from './utils/sessionExpiry';
import { Loader2 } from 'lucide-react';

const LoginPage = lazy(() => import('./pages/LoginPage'));
const RegisterPage = lazy(() => import('./pages/RegisterPage'));
const ForgotPasswordPage = lazy(() => import('./pages/ForgotPasswordPage'));
const ResetPasswordPage = lazy(() => import('./pages/ResetPasswordPage'));
const DashboardPage = lazy(() => import('./pages/DashboardPage'));
const DoctorsListPage = lazy(() => import('./pages/DoctorsListPage'));
const BookAppointmentPage = lazy(() => import('./pages/BookAppointmentPage'));
const MyAppointmentsPage = lazy(() => import('./pages/MyAppointmentsPage'));
const StripePaymentPage = lazy(() => import('./pages/StripePaymentPage'));
const CreatePatientProfilePage = lazy(() => import('./pages/CreatePatientProfilePage'));
const EditPatientProfilePage = lazy(() => import('./pages/EditPatientProfilePage'));
const EditDoctorProfilePage = lazy(() => import('./pages/EditDoctorProfilePage'));
const DoctorDashboardPage = lazy(() => import('./pages/DoctorDashboardPage'));
const AdminDashboardPage = lazy(() => import('./pages/AdminDashboardPage'));
const AdminAnalyticsPage = lazy(() => import('./pages/AdminAnalyticsPage'));
const AdminAuditLogsPage = lazy(() => import('./pages/AdminAuditLogsPage'));
const ForbiddenPage = lazy(() => import('./pages/ForbiddenPage'));
const SessionExpiredPage = lazy(() => import('./pages/SessionExpiredPage'));
const NotificationsPage = lazy(() => import('./pages/NotificationsPage'));

function ProtectedLayout({ children, allowedRoles }) {
  return (
    <ProtectedRoute allowedRoles={allowedRoles}>
      <Layout>{children}</Layout>
    </ProtectedRoute>
  );
}

function LoadingFallback() {
  return (
    <div className="flex items-center justify-center min-h-[60vh] text-text-muted">
      <Loader2 size={24} className="animate-spin" />
    </div>
  );
}

/** Registers SPA navigation for apiClient session-expiry redirects. */
function SessionExpiryBridge() {
  const navigate = useNavigate();
  useEffect(() => {
    setSessionExpiredHandler(() => {
      navigate('/session-expired', { replace: true });
    });
    return () => setSessionExpiredHandler(null);
  }, [navigate]);
  return null;
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <SessionExpiryBridge />
        <Toaster position="top-right" toastOptions={{
          style: { borderRadius: '8px', fontSize: '14px', padding: '12px 16px' },
        }} />
        <ErrorBoundary>
          <Suspense fallback={<LoadingFallback />}>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/register" element={<RegisterPage />} />
              <Route path="/forgot-password" element={<ForgotPasswordPage />} />
              <Route path="/reset-password" element={<ResetPasswordPage />} />
              <Route path="/403" element={<ForbiddenPage />} />
              <Route path="/session-expired" element={<SessionExpiredPage />} />
              <Route path="/notifications" element={<ProtectedLayout><NotificationsPage /></ProtectedLayout>} />
              <Route path="/dashboard" element={<ProtectedLayout><DashboardPage /></ProtectedLayout>} />
              <Route path="/doctors" element={<ProtectedLayout><DoctorsListPage /></ProtectedLayout>} />
              <Route path="/book-appointment/:doctorId" element={<ProtectedLayout allowedRoles={['Patient']}><BookAppointmentPage /></ProtectedLayout>} />
              <Route path="/my-appointments" element={<ProtectedLayout allowedRoles={['Patient']}><MyAppointmentsPage /></ProtectedLayout>} />
              <Route path="/pay/:appointmentId" element={<ProtectedLayout allowedRoles={['Patient']}><StripePaymentPage /></ProtectedLayout>} />
              <Route path="/create-patient" element={<ProtectedLayout allowedRoles={['Patient']}><CreatePatientProfilePage /></ProtectedLayout>} />
              <Route path="/edit-patient" element={<ProtectedLayout allowedRoles={['Patient']}><EditPatientProfilePage /></ProtectedLayout>} />
              <Route path="/edit-doctor" element={<ProtectedLayout allowedRoles={['Doctor']}><EditDoctorProfilePage /></ProtectedLayout>} />
              <Route path="/doctor-dashboard" element={<ProtectedLayout allowedRoles={['Doctor']}><DoctorDashboardPage /></ProtectedLayout>} />
              <Route path="/admin" element={<ProtectedLayout allowedRoles={['Admin']}><AdminDashboardPage /></ProtectedLayout>} />
              <Route path="/admin/patients" element={<ProtectedLayout allowedRoles={['Admin']}><AdminDashboardPage /></ProtectedLayout>} />
              <Route path="/admin/analytics" element={<ProtectedLayout allowedRoles={['Admin']}><AdminAnalyticsPage /></ProtectedLayout>} />
              <Route path="/admin/audit-logs" element={<ProtectedLayout allowedRoles={['Admin']}><AdminAuditLogsPage /></ProtectedLayout>} />
              <Route path="*" element={<Navigate to="/login" replace />} />
            </Routes>
          </Suspense>
        </ErrorBoundary>
      </AuthProvider>
    </BrowserRouter>
  );
}
