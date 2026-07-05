import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { AuthProvider } from './context/AuthContext';
import ProtectedRoute from './components/ProtectedRoute';
import Layout from './components/Layout';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import DashboardPage from './pages/DashboardPage';
import DoctorsListPage from './pages/DoctorsListPage';
import BookAppointmentPage from './pages/BookAppointmentPage';
import MyAppointmentsPage from './pages/MyAppointmentsPage';
import CreatePatientProfilePage from './pages/CreatePatientProfilePage';
import DoctorDashboardPage from './pages/DoctorDashboardPage';
import AdminDashboardPage from './pages/AdminDashboardPage';
import ForbiddenPage from './pages/ForbiddenPage';

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
      </AuthProvider>
    </BrowserRouter>
  );
}
