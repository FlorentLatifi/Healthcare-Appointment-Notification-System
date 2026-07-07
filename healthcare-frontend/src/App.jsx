import { lazy, Suspense } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { AuthProvider } from './context/AuthContext';
import ErrorBoundary from './components/ErrorBoundary';
import ProtectedRoute from './components/ProtectedRoute';
import Layout from './components/Layout';

const LoginPage = lazy(() => import('./pages/LoginPage'));
const RegisterPage = lazy(() => import('./pages/RegisterPage'));
const DashboardPage = lazy(() => import('./pages/DashboardPage'));
const DoctorsListPage = lazy(() => import('./pages/DoctorsListPage'));
const BookAppointmentPage = lazy(() => import('./pages/BookAppointmentPage'));
const MyAppointmentsPage = lazy(() => import('./pages/MyAppointmentsPage'));
const CreatePatientProfilePage = lazy(() => import('./pages/CreatePatientProfilePage'));
const DoctorDashboardPage = lazy(() => import('./pages/DoctorDashboardPage'));
const AdminDashboardPage = lazy(() => import('./pages/AdminDashboardPage'));
const ForbiddenPage = lazy(() => import('./pages/ForbiddenPage'));

const loadingStyle = { textAlign: 'center', padding: 40, color: '#888' };

function ProtectedLayout({ children, allowedRoles }) {
  return (
    <ProtectedRoute allowedRoles={allowedRoles}>
      <Layout>{children}</Layout>
    </ProtectedRoute>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Toaster position="top-right" />
        <ErrorBoundary>
          <Suspense fallback={<div style={loadingStyle}>Loading...</div>}>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/403" element={<ForbiddenPage />} />
          <Route path="/dashboard" element={<ProtectedLayout><DashboardPage /></ProtectedLayout>} />
          <Route path="/doctors" element={<ProtectedLayout><DoctorsListPage /></ProtectedLayout>} />
          <Route path="/book-appointment/:doctorId" element={<ProtectedLayout allowedRoles={['Patient']}><BookAppointmentPage /></ProtectedLayout>} />
          <Route path="/my-appointments" element={<ProtectedLayout allowedRoles={['Patient']}><MyAppointmentsPage /></ProtectedLayout>} />
          <Route path="/create-patient" element={<ProtectedLayout allowedRoles={['Patient']}><CreatePatientProfilePage /></ProtectedLayout>} />
          <Route path="/doctor-dashboard" element={<ProtectedLayout allowedRoles={['Doctor']}><DoctorDashboardPage /></ProtectedLayout>} />
          <Route path="/admin" element={<ProtectedLayout allowedRoles={['Admin']}><AdminDashboardPage /></ProtectedLayout>} />
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
        </Suspense>
        </ErrorBoundary>
      </AuthProvider>
    </BrowserRouter>
  );
}
