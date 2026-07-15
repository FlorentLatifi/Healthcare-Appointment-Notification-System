import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockLogin = vi.fn();
const mockNavigate = vi.fn();

vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
  Link: ({ children, to }) => <a href={to}>{children}</a>,
}));

vi.mock('react-hot-toast', () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('../../context/AuthContext', () => ({
  useAuth: () => ({ login: mockLogin, loading: false }),
}));

import LoginPage from '../LoginPage';

describe('LoginPage', () => {
  beforeEach(() => {
    mockLogin.mockReset();
    mockNavigate.mockReset();
  });

  function renderPage() {
    const view = render(<LoginPage />);
    return {
      ...view,
      usernameInput: view.container.querySelector('input[name="username"]'),
      passwordInput: view.container.querySelector('input[name="password"]'),
    };
  }

  it('renders the login form', () => {
    const { usernameInput, passwordInput } = renderPage();
    expect(screen.getByRole('heading', { name: /login/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /login/i })).toBeInTheDocument();
    expect(usernameInput).toBeInTheDocument();
    expect(passwordInput).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /register/i })).toHaveAttribute('href', '/register');
    expect(screen.getByRole('link', { name: /forgot password/i })).toHaveAttribute('href', '/forgot-password');
  });

  it('calls login() with entered credentials on submit', async () => {
    mockLogin.mockResolvedValueOnce({});

    const { usernameInput, passwordInput } = renderPage();
    await userEvent.type(usernameInput, 'myuser');
    await userEvent.type(passwordInput, 'mypass');

    await userEvent.click(screen.getByRole('button', { name: /login/i }));

    expect(mockLogin).toHaveBeenCalledWith('myuser', 'mypass');
  });

  it('navigates to dashboard on successful login for linked patient', async () => {
    mockLogin.mockResolvedValueOnce({
      user: { username: 'u', role: 'Patient' },
      patientId: 1,
      doctorId: null,
    });

    const { usernameInput, passwordInput } = renderPage();
    await userEvent.type(usernameInput, 'u');
    await userEvent.type(passwordInput, 'p');
    await userEvent.click(screen.getByRole('button', { name: /login/i }));

    await vi.waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/dashboard', { replace: true });
    });
  });

  it('navigates to create-patient when patient has no profile', async () => {
    mockLogin.mockResolvedValueOnce({
      user: { username: 'u', role: 'Patient' },
      patientId: null,
      doctorId: null,
    });

    const { usernameInput, passwordInput } = renderPage();
    await userEvent.type(usernameInput, 'u');
    await userEvent.type(passwordInput, 'p');
    await userEvent.click(screen.getByRole('button', { name: /login/i }));

    await vi.waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/create-patient', { replace: true });
    });
  });

  it('navigates to admin for Admin role', async () => {
    mockLogin.mockResolvedValueOnce({
      user: { username: 'admin', role: 'Admin' },
      patientId: null,
      doctorId: null,
    });

    const { usernameInput, passwordInput } = renderPage();
    await userEvent.type(usernameInput, 'admin');
    await userEvent.type(passwordInput, 'p');
    await userEvent.click(screen.getByRole('button', { name: /login/i }));

    await vi.waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/admin', { replace: true });
    });
  });
});
