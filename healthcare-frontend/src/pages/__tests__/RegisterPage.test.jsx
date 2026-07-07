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

  it('shows error when passwords do not match', async () => {
    const { usernameInput, emailInput, passwordInput, confirmInput } = renderPage();

    await userEvent.type(usernameInput, 'newuser');
    await userEvent.type(emailInput, 'new@test.com');
    await userEvent.type(passwordInput, 'password123');
    await userEvent.type(confirmInput, 'differentpass');

    await userEvent.click(screen.getByRole('button', { name: /register/i }));

    const toast = (await import('react-hot-toast')).default;
    expect(toast.error).toHaveBeenCalledWith('Passwords do not match');
    expect(mockRegister).not.toHaveBeenCalled();
  });

  it('calls register with correct arguments on valid submission', async () => {
    const { usernameInput, emailInput, passwordInput, confirmInput } = renderPage();

    await userEvent.type(usernameInput, 'newuser');
    await userEvent.type(emailInput, 'new@test.com');
    await userEvent.type(passwordInput, 'password123');
    await userEvent.type(confirmInput, 'password123');

    await userEvent.click(screen.getByRole('button', { name: /register/i }));

    expect(mockRegister).toHaveBeenCalledWith('newuser', 'new@test.com', 'password123', 'Patient');
  });

  it('navigates to /login on successful registration', async () => {
    mockRegister.mockResolvedValueOnce({});

    const { usernameInput, emailInput, passwordInput, confirmInput } = renderPage();

    await userEvent.type(usernameInput, 'newuser');
    await userEvent.type(emailInput, 'new@test.com');
    await userEvent.type(passwordInput, 'password123');
    await userEvent.type(confirmInput, 'password123');

    await userEvent.click(screen.getByRole('button', { name: /register/i }));

    await vi.waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/login', { replace: true });
    });
  });
});
