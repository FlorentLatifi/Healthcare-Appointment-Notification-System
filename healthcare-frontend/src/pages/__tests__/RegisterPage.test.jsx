import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockNavigate, mockRegister } = vi.hoisted(() => ({
  mockNavigate: vi.fn(),
  mockRegister: vi.fn(),
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
  Link: ({ children, to }) => <a href={to}>{children}</a>,
}));

vi.mock('react-hot-toast', () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('../../context/AuthContext', () => ({
  useAuth: () => ({ register: mockRegister, loading: false }),
}));

import RegisterPage from '../RegisterPage';
import toast from 'react-hot-toast';

describe('RegisterPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  function renderPage() {
    const view = render(<RegisterPage />);
    return {
      ...view,
      usernameInput: view.container.querySelector('input[name="username"]'),
      emailInput: view.container.querySelector('input[name="email"]'),
      passwordInput: view.container.querySelector('input[name="password"]'),
      confirmInput: view.container.querySelector('input[name="confirmPassword"]'),
    };
  }

  it('renders the registration form', () => {
    const { usernameInput, emailInput, passwordInput, confirmInput } = renderPage();
    expect(screen.getByRole('heading', { name: /register/i })).toBeInTheDocument();
    expect(usernameInput).toBeInTheDocument();
    expect(emailInput).toBeInTheDocument();
    expect(passwordInput).toBeInTheDocument();
    expect(confirmInput).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /register/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /login/i })).toHaveAttribute('href', '/login');
  });

  it('shows inline error when passwords do not match', async () => {
    const { usernameInput, emailInput, passwordInput, confirmInput } = renderPage();

    await userEvent.type(usernameInput, 'newuser');
    await userEvent.type(emailInput, 'new@test.com');
    await userEvent.type(passwordInput, 'MyClinic!2026x');
    await userEvent.type(confirmInput, 'MyClinic!2026y');

    await userEvent.click(screen.getByRole('button', { name: /register/i }));

    expect((await screen.findAllByText('Passwords do not match')).length).toBeGreaterThan(0);
    expect(mockRegister).not.toHaveBeenCalled();
  });

  it('shows client-side password length error for weak password', async () => {
    const { usernameInput, emailInput, passwordInput, confirmInput } = renderPage();

    await userEvent.type(usernameInput, 'newuser');
    await userEvent.type(emailInput, 'new@test.com');
    await userEvent.type(passwordInput, '123456');
    await userEvent.type(confirmInput, '123456');

    await userEvent.click(screen.getByRole('button', { name: /register/i }));

    expect((await screen.findAllByText(/at least 12 characters/i)).length).toBeGreaterThan(0);
    expect(mockRegister).not.toHaveBeenCalled();
  });

  it('calls register with correct arguments on valid submission', async () => {
    mockRegister.mockResolvedValueOnce({});
    const { usernameInput, emailInput, passwordInput, confirmInput } = renderPage();

    await userEvent.type(usernameInput, 'newuser');
    await userEvent.type(emailInput, 'new@test.com');
    await userEvent.type(passwordInput, 'MyClinic!2026x');
    await userEvent.type(confirmInput, 'MyClinic!2026x');

    await userEvent.click(screen.getByRole('button', { name: /register/i }));

    await vi.waitFor(() => {
      expect(mockRegister).toHaveBeenCalledWith('newuser', 'new@test.com', 'MyClinic!2026x', 'Patient');
    });
  });

  it('shows field-level API validation errors without toast for field-only failures', async () => {
    const apiErr = new Error('Password must be at least 12 characters');
    apiErr.apiError = {
      fieldErrors: { password: ['Password must be at least 12 characters'] },
      generalError: null,
      hasFieldErrors: true,
    };
    mockRegister.mockRejectedValueOnce(apiErr);

    const { usernameInput, emailInput, passwordInput, confirmInput } = renderPage();
    await userEvent.type(usernameInput, 'newuser');
    await userEvent.type(emailInput, 'new@test.com');
    await userEvent.type(passwordInput, 'MyClinic!2026x');
    await userEvent.type(confirmInput, 'MyClinic!2026x');
    await userEvent.click(screen.getByRole('button', { name: /register/i }));

    await vi.waitFor(() => {
      expect(screen.getAllByText('Password must be at least 12 characters').length).toBeGreaterThan(0);
    });
    expect(toast.error).not.toHaveBeenCalled();
  });

  it('navigates to /login on successful registration', async () => {
    mockRegister.mockResolvedValueOnce({});

    const { usernameInput, emailInput, passwordInput, confirmInput } = renderPage();

    await userEvent.type(usernameInput, 'newuser');
    await userEvent.type(emailInput, 'new@test.com');
    await userEvent.type(passwordInput, 'MyClinic!2026x');
    await userEvent.type(confirmInput, 'MyClinic!2026x');

    await userEvent.click(screen.getByRole('button', { name: /register/i }));

    await vi.waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/login', { replace: true });
    });
    expect(toast.success).toHaveBeenCalled();
  });
});
