import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('react-router-dom', () => ({
  Navigate: ({ to }) => <div data-testid="navigate-to">{to}</div>,
}));

vi.mock('../../context/AuthContext', () => ({
  useAuth: vi.fn(),
}));

import { useAuth } from '../../context/AuthContext';
import ProtectedRoute from '../ProtectedRoute';

describe('ProtectedRoute', () => {
  it('shows session restore spinner while sessionReady is false', () => {
    vi.mocked(useAuth).mockReturnValue({
      isAuthenticated: false,
      user: null,
      sessionReady: false,
    });

    render(
      <ProtectedRoute>
        <div data-testid="content">Protected Content</div>
      </ProtectedRoute>,
    );

    expect(screen.getByRole('status', { name: /restoring session/i })).toBeInTheDocument();
    expect(screen.queryByTestId('content')).not.toBeInTheDocument();
    expect(screen.queryByTestId('navigate-to')).not.toBeInTheDocument();
  });

  it('redirects to /login when not authenticated', () => {
    vi.mocked(useAuth).mockReturnValue({
      isAuthenticated: false,
      user: null,
      sessionReady: true,
    });

    render(
      <ProtectedRoute allowedRoles={['Admin']}>
        <div data-testid="content">Protected Content</div>
      </ProtectedRoute>,
    );

    expect(screen.getByTestId('navigate-to')).toHaveTextContent('/login');
    expect(screen.queryByTestId('content')).not.toBeInTheDocument();
  });

  it('redirects to /403 when role is not allowed', () => {
    vi.mocked(useAuth).mockReturnValue({
      isAuthenticated: true,
      user: { role: 'Patient' },
      sessionReady: true,
    });

    render(
      <ProtectedRoute allowedRoles={['Admin', 'Doctor']}>
        <div data-testid="content">Protected Content</div>
      </ProtectedRoute>,
    );

    expect(screen.getByTestId('navigate-to')).toHaveTextContent('/403');
    expect(screen.queryByTestId('content')).not.toBeInTheDocument();
  });

  it('renders children when authenticated and role is allowed', () => {
    vi.mocked(useAuth).mockReturnValue({
      isAuthenticated: true,
      user: { role: 'Admin' },
      sessionReady: true,
    });

    render(
      <ProtectedRoute allowedRoles={['Admin', 'Doctor']}>
        <div data-testid="content">Protected Content</div>
      </ProtectedRoute>,
    );

    expect(screen.getByTestId('content')).toHaveTextContent('Protected Content');
    expect(screen.queryByTestId('navigate-to')).not.toBeInTheDocument();
  });

  it('renders children when authenticated and no allowedRoles specified', () => {
    vi.mocked(useAuth).mockReturnValue({
      isAuthenticated: true,
      user: { role: 'Patient' },
      sessionReady: true,
    });

    render(
      <ProtectedRoute>
        <div data-testid="content">Protected Content</div>
      </ProtectedRoute>,
    );

    expect(screen.getByTestId('content')).toBeInTheDocument();
  });
});
