import { Component } from 'react';
import { Button } from './ui';
import { AlertTriangle, RefreshCw, ArrowRight } from 'lucide-react';

export default class ErrorBoundary extends Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError() {
    return { hasError: true };
  }

  componentDidCatch(error, info) {
    console.error('ErrorBoundary caught:', error, info);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="flex items-center justify-center min-h-screen p-6 bg-bg">
          <div className="bg-white rounded-xl shadow-card p-8 max-w-md w-full text-center">
            <div className="w-12 h-12 mx-auto mb-4 rounded-full bg-status-cancelled-bg flex items-center justify-center">
              <AlertTriangle size={24} className="text-status-cancelled-text" />
            </div>
            <h2 className="text-lg font-semibold text-text mb-2">Something went wrong</h2>
            <p className="text-sm text-text-muted mb-6 leading-relaxed">
              An unexpected error occurred. Please try reloading the page or return to the dashboard.
            </p>
            <div className="flex justify-center gap-3">
              <Button
                leftIcon={<RefreshCw size={14} />}
                onClick={() => window.location.reload()}
              >
                Reload Page
              </Button>
              <a
                href="/dashboard"
                className="inline-flex items-center gap-1.5 px-4 py-2 text-sm font-medium text-text border border-border rounded-md hover:bg-surface transition-colors duration-150"
              >
                Go to Dashboard
                <ArrowRight size={14} />
              </a>
            </div>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
