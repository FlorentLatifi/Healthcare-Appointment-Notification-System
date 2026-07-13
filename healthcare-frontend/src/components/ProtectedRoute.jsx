import { Navigate } from 'react-router-dom';
import { Loader2 } from 'lucide-react';
import { useAuth } from '../context/AuthContext';

export default function ProtectedRoute({ children, allowedRoles }) {
  const { isAuthenticated, user, sessionReady } = useAuth();

  // Wait for cookie-based session restore so we don't flash login or empty dashboard.
  if (!sessionReady) {
    return (
      <div
        className="flex items-center justify-center min-h-[60vh] text-text-muted"
        role="status"
        aria-label="Restoring session"
      >
        <Loader2 size={24} className="animate-spin" />
      </div>
    );
  }

  if (!isAuthenticated) return <Navigate to="/login" replace />;
  if (allowedRoles && !allowedRoles.includes(user?.role)) {
    return <Navigate to="/403" replace />;
  }
  return children;
}
