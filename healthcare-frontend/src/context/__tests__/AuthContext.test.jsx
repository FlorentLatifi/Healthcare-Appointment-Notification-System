import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { AuthProvider, useAuth } from '../AuthContext';

const mockPost = vi.fn();
vi.mock('../../services/apiClient', () => ({
  default: { post: (...args) => mockPost(...args) },
  setTokenGetter: vi.fn(),
  setTokenSetter: vi.fn(),
  onAuthCleared: vi.fn(),
}));

function TestConsumer() {
  const auth = useAuth();
  return (
    <div>
      <span data-testid="isAuth">{String(auth.isAuthenticated)}</span>
      <span data-testid="user">{auth.user ? JSON.stringify(auth.user) : 'null'}</span>
      <span data-testid="token">{auth.token ?? 'null'}</span>
      <span data-testid="loading">{String(auth.loading)}</span>
      <button data-testid="loginBtn" onClick={() => { auth.login('testuser', 'pass123').catch(() => {}); }}>Login</button>
      <button data-testid="registerBtn" onClick={async () => { await auth.register('newuser', 'e@e.com', 'pass', 'Patient'); }}>Register</button>
      <button data-testid="logoutBtn" onClick={() => auth.logout()}>Logout</button>
    </div>
  );
}

function renderWithProvider() {
  return render(
    <AuthProvider>
      <TestConsumer />
    </AuthProvider>,
  );
}

/** Helper: make the session-restore effect return no session. */
function mockNoSession() {
  mockPost.mockRejectedValueOnce(new Error('No cookie'));
}

/** Helper: make the session-restore effect return a session. */
function mockHasSession() {
  mockPost.mockResolvedValueOnce({
    data: {
      success: true,
      data: { token: 'restored-token', username: 'restoreduser', role: 'Patient' },
    },
  });
}

describe('AuthContext', () => {
  beforeEach(() => {
    mockPost.mockReset();
  });

  it('starts unauthenticated', async () => {
    mockNoSession();
    renderWithProvider();
    await waitFor(() => {
      expect(screen.getByTestId('isAuth')).toHaveTextContent('false');
    });
    expect(screen.getByTestId('user')).toHaveTextContent('null');
    expect(screen.getByTestId('token')).toHaveTextContent('null');
  });

  it('login() updates state on success', async () => {
    mockNoSession();
    mockPost.mockResolvedValueOnce({
      data: {
        success: true,
        data: { token: 'abc123', username: 'testuser', role: 'Patient' },
      },
    });

    renderWithProvider();
    await userEvent.click(screen.getByTestId('loginBtn'));

    await waitFor(() => {
      expect(screen.getByTestId('isAuth')).toHaveTextContent('true');
      expect(screen.getByTestId('token')).toHaveTextContent('abc123');
      expect(screen.getByTestId('user')).toHaveTextContent('testuser');
      expect(screen.getByTestId('user')).toHaveTextContent('Patient');
    });
    expect(mockPost).toHaveBeenCalledWith('/Auth/login', { username: 'testuser', password: 'pass123' });
  });

  it('login() does not authenticate on failed response', async () => {
    mockNoSession();
    mockPost.mockResolvedValueOnce({
      data: { success: false, errors: ['Invalid credentials'] },
    });

    renderWithProvider();
    await userEvent.click(screen.getByTestId('loginBtn'));

    await waitFor(() => {
      expect(screen.getByTestId('isAuth')).toHaveTextContent('false');
    });
  });

  it('register() calls API but does not update auth state', async () => {
    mockNoSession();
    mockPost.mockResolvedValueOnce({
      data: { success: true, data: { token: 'xyz789', username: 'newuser', role: 'Patient' } },
    });

    renderWithProvider();
    await userEvent.click(screen.getByTestId('registerBtn'));

    await waitFor(() => {
      expect(mockPost).toHaveBeenCalledWith('/Auth/register', {
        username: 'newuser', email: 'e@e.com', password: 'pass', role: 'Patient',
      });
    });
    expect(screen.getByTestId('isAuth')).toHaveTextContent('false');
  });

  it('logout() clears all state', async () => {
    mockNoSession();
    mockPost.mockResolvedValueOnce({
      data: { success: true, data: { token: 'abc', username: 'u', role: 'Admin' } },
    });

    renderWithProvider();
    await userEvent.click(screen.getByTestId('loginBtn'));
    await waitFor(() => expect(screen.getByTestId('isAuth')).toHaveTextContent('true'));

    mockPost.mockResolvedValueOnce({ data: { success: true } });

    await userEvent.click(screen.getByTestId('logoutBtn'));

    await waitFor(() => {
      expect(screen.getByTestId('isAuth')).toHaveTextContent('false');
      expect(screen.getByTestId('user')).toHaveTextContent('null');
      expect(screen.getByTestId('token')).toHaveTextContent('null');
    });
  });

  it('sets loading during async operations', async () => {
    mockNoSession();
    let resolvePromise;
    mockPost.mockReturnValueOnce(new Promise((resolve) => { resolvePromise = resolve; }));

    renderWithProvider();

    const clickPromise = userEvent.click(screen.getByTestId('loginBtn'));

    await waitFor(() => {
      expect(screen.getByTestId('loading')).toHaveTextContent('true');
    });

    resolvePromise({
      data: { success: true, data: { token: 't', username: 'u', role: 'Patient' } },
    });
    await clickPromise;

    await waitFor(() => expect(screen.getByTestId('loading')).toHaveTextContent('false'));
  });

  it('restores session from refresh cookie on mount', async () => {
    mockHasSession();

    renderWithProvider();

    await waitFor(() => {
      expect(screen.getByTestId('isAuth')).toHaveTextContent('true');
      expect(screen.getByTestId('token')).toHaveTextContent('restored-token');
      expect(screen.getByTestId('user')).toHaveTextContent('restoreduser');
    });
  });

  it('logout() calls server-side revocation', async () => {
    mockNoSession();
    mockPost.mockResolvedValueOnce({
      data: { success: true, data: { token: 'abc', username: 'u', role: 'Admin' } },
    });

    renderWithProvider();
    await userEvent.click(screen.getByTestId('loginBtn'));
    await waitFor(() => expect(screen.getByTestId('isAuth')).toHaveTextContent('true'));

    mockPost.mockResolvedValueOnce({ data: { success: true } });

    await userEvent.click(screen.getByTestId('logoutBtn'));

    await waitFor(() => {
      expect(mockPost).toHaveBeenCalledWith('/Auth/logout');
    });
  });
});
