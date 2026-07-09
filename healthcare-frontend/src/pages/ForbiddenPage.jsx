import { useNavigate } from 'react-router-dom';
import { Button } from '../components/ui';
import { ShieldAlert, ArrowRight } from 'lucide-react';

export default function ForbiddenPage() {
  const navigate = useNavigate();
  return (
    <div className="min-h-screen flex items-center justify-center bg-bg px-4">
      <div className="text-center max-w-sm">
        <div className="w-16 h-16 mx-auto mb-6 rounded-full bg-status-cancelled-bg flex items-center justify-center">
          <ShieldAlert size={32} className="text-status-cancelled-text" />
        </div>
        <h1 className="text-5xl font-bold text-text tracking-tight mb-2">403</h1>
        <p className="text-sm text-text-muted mb-8">
          You do not have permission to access this page.
        </p>
        <Button rightIcon={<ArrowRight size={14} />} onClick={() => navigate('/dashboard')}>
          Go to Dashboard
        </Button>
      </div>
    </div>
  );
}
