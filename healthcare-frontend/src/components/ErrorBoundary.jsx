import { Component } from 'react';

const s = {
  wrapper: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: '100vh',
    padding: 24,
    fontFamily: 'system-ui, -apple-system, sans-serif',
  },
  card: {
    background: '#fff',
    border: '1px solid #ddd',
    borderRadius: 8,
    padding: 32,
    maxWidth: 420,
    width: '100%',
    textAlign: 'center',
  },
  heading: { margin: '0 0 8px', fontSize: 20, color: '#dc2626' },
  message: { margin: '0 0 24px', fontSize: 14, color: '#666', lineHeight: 1.5 },
  actions: { display: 'flex', justifyContent: 'center', gap: 12 },
  btn: {
    padding: '8px 20px',
    borderRadius: 6,
    fontSize: 14,
    border: 'none',
    cursor: 'pointer',
    textDecoration: 'none',
    display: 'inline-flex',
    alignItems: 'center',
  },
  primaryBtn: { background: '#2563eb', color: '#fff' },
  secondaryBtn: { background: '#f9f9f9', color: '#333', border: '1px solid #ccc' },
};

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
        <div style={s.wrapper}>
          <div style={s.card}>
            <h2 style={s.heading}>Something went wrong</h2>
            <p style={s.message}>
              An unexpected error occurred. Please try reloading the page or
              return to the dashboard.
            </p>
            <div style={s.actions}>
              <button
                style={{ ...s.btn, ...s.primaryBtn }}
                onClick={() => window.location.reload()}
              >
                Reload Page
              </button>
              <a
                href="/dashboard"
                style={{ ...s.btn, ...s.secondaryBtn }}
              >
                Go to Dashboard
              </a>
            </div>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
