import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/ui';
import { Clock, LogIn } from 'lucide-react';

/**
 * Shown when the access token cannot be refreshed (cookie expired / revoked).
 * Route is public so users are never stuck on a broken authenticated page.
 */
export default function SessionExpiredPage() {
  const navigate = useNavigate();

  useEffect(() => {
    try {
      sessionStorage.removeItem('session_expired');
    } catch {
      // ignore storage errors
    }
  }, []);

  return (
    <div className="min-h-screen flex items-center justify-center bg-bg px-4 sm:px-6 py-8">
      <div className="text-center max-w-md w-full">
        <div className="w-16 h-16 mx-auto mb-6 rounded-full bg-status-pending-bg flex items-center justify-center">
          <Clock size={32} className="text-status-pending-text" aria-hidden="true" />
        </div>
        <h1 className="text-2xl sm:text-3xl font-semibold text-text tracking-tight mb-2">
          Session expired
        </h1>
        <p className="text-sm text-text-muted mb-8 leading-relaxed">
          Your session has expired. Please sign in again to continue.
        </p>
        <Button
          className="w-full sm:w-auto"
          leftIcon={<LogIn size={14} />}
          onClick={() => navigate('/login', { replace: true })}
        >
          Sign in again
        </Button>
      </div>
    </div>
  );
}
