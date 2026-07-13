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
      <span data-testid="patientId">{auth.patientId ?? 'null'}</span>
      <span data-testid="doctorId">{auth.doctorId ?? 'null'}</span>
      <span data-testid="loading">{String(auth.loading)}</span>
      <span data-testid="hasSetPatientId">{String(typeof auth.setPatientId === 'function')}</span>
      <span data-testid="hasSetDoctorId">{String(typeof auth.setDoctorId === 'function')}</span>
      <button data-testid="loginBtn" onClick={() => { auth.login('testuser', 'pass123').catch(() => {}); }}>Login</button>
      <button data-testid="registerBtn" onClick={async () => { await auth.register('newuser', 'e@e.com', 'pass', 'Patient'); }}>Register</button>
      <button data-testid="logoutBtn" onClick={() => auth.logout()}>Logout</button>
      <button data-testid="refreshBtn" onClick={() => { auth.refreshSession().catch(() => {}); }}>Refresh</button>
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
      data: { token: 'restored-token', username: 'restoreduser', role: 'Patient', patientId: 42, doctorId: null },
    },
  });
}

/** Helper: make the session-restore effect return a doctor session. */
function mockHasDoctorSession() {
  mockPost.mockResolvedValueOnce({
    data: {
      success: true,
      data: { token: 'restored-token', username: 'drwho', role: 'Doctor', patientId: null, doctorId: 7 },
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
        data: { token: 'abc123', username: 'testuser', role: 'Patient', patientId: 10, doctorId: null },
      },
    });

    renderWithProvider();
    await userEvent.click(screen.getByTestId('loginBtn'));

    await waitFor(() => {
      expect(screen.getByTestId('isAuth')).toHaveTextContent('true');
      expect(screen.getByTestId('token')).toHaveTextContent('abc123');
      expect(screen.getByTestId('user')).toHaveTextContent('testuser');
      expect(screen.getByTestId('user')).toHaveTextContent('Patient');
      expect(screen.getByTestId('patientId')).toHaveTextContent('10');
      expect(screen.getByTestId('doctorId')).toHaveTextContent('null');
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
      data: { success: true, data: { token: 'abc', username: 'u', role: 'Admin', patientId: null, doctorId: null } },
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
      expect(screen.getByTestId('patientId')).toHaveTextContent('null');
      expect(screen.getByTestId('doctorId')).toHaveTextContent('null');
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
      data: { success: true, data: { token: 't', username: 'u', role: 'Patient', patientId: null, doctorId: null } },
    });
    await clickPromise;

    await waitFor(() => expect(screen.getByTestId('loading')).toHaveTextContent('false'));
  });

  it('restores session with patientId from refresh cookie on mount', async () => {
    mockHasSession();

    renderWithProvider();

    await waitFor(() => {
      expect(screen.getByTestId('isAuth')).toHaveTextContent('true');
      expect(screen.getByTestId('token')).toHaveTextContent('restored-token');
      expect(screen.getByTestId('user')).toHaveTextContent('restoreduser');
      expect(screen.getByTestId('patientId')).toHaveTextContent('42');
      expect(screen.getByTestId('doctorId')).toHaveTextContent('null');
    });
  });

  it('restores session with doctorId from refresh cookie on mount', async () => {
    mockHasDoctorSession();

    renderWithProvider();

    await waitFor(() => {
      expect(screen.getByTestId('isAuth')).toHaveTextContent('true');
      expect(screen.getByTestId('token')).toHaveTextContent('restored-token');
      expect(screen.getByTestId('user')).toHaveTextContent('drwho');
      expect(screen.getByTestId('patientId')).toHaveTextContent('null');
      expect(screen.getByTestId('doctorId')).toHaveTextContent('7');
    });
  });

  it('login() sets patientId and doctorId from response', async () => {
    mockNoSession();
    mockPost.mockResolvedValueOnce({
      data: {
        success: true,
        data: { token: 'doc-token', username: 'drwho', role: 'Doctor', patientId: null, doctorId: 99 },
      },
    });

    renderWithProvider();
    await userEvent.click(screen.getByTestId('loginBtn'));

    await waitFor(() => {
      expect(screen.getByTestId('patientId')).toHaveTextContent('null');
      expect(screen.getByTestId('doctorId')).toHaveTextContent('99');
    });
  });

  it('logout() calls server-side revocation', async () => {
    mockNoSession();
    mockPost.mockResolvedValueOnce({
      data: { success: true, data: { token: 'abc', username: 'u', role: 'Admin', patientId: null, doctorId: null } },
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

  it('refreshSession() updates token and profile claim ids from /Auth/refresh', async () => {
    mockNoSession();
    mockPost.mockResolvedValueOnce({
      data: {
        success: true,
        data: { token: 'old-token', username: 'testuser', role: 'Patient', patientId: null, doctorId: null },
      },
    });

    renderWithProvider();
    await userEvent.click(screen.getByTestId('loginBtn'));
    await waitFor(() => {
      expect(screen.getByTestId('token')).toHaveTextContent('old-token');
      expect(screen.getByTestId('patientId')).toHaveTextContent('null');
    });

    // Simulate post-CreatePatient: refresh returns JWT with patient_id claim
    mockPost.mockResolvedValueOnce({
      data: {
        success: true,
        data: { token: 'new-token-with-claim', username: 'testuser', role: 'Patient', patientId: 77, doctorId: null },
      },
    });

    await userEvent.click(screen.getByTestId('refreshBtn'));

    await waitFor(() => {
      expect(screen.getByTestId('token')).toHaveTextContent('new-token-with-claim');
      expect(screen.getByTestId('patientId')).toHaveTextContent('77');
    });
    expect(mockPost).toHaveBeenCalledWith('/Auth/refresh');
  });

  it('does not expose client-only setPatientId / setDoctorId claim mutators', async () => {
    mockNoSession();
    renderWithProvider();
    await waitFor(() => {
      expect(screen.getByTestId('hasSetPatientId')).toHaveTextContent('false');
      expect(screen.getByTestId('hasSetDoctorId')).toHaveTextContent('false');
    });
  });
});
