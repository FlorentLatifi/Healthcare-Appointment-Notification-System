import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockApiClient, mockNavigate, searchParams } = vi.hoisted(() => ({
  mockApiClient: { post: vi.fn() },
  mockNavigate: vi.fn(),
  searchParams: new URLSearchParams('email=user%40example.com&token=abc123token'),
}));

vi.mock('react-router-dom', () => ({
  Link: ({ children, to }) => <a href={to}>{children}</a>,
  useNavigate: () => mockNavigate,
  useSearchParams: () => [searchParams],
}));

vi.mock('react-hot-toast', () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('../../services/apiClient', () => ({
  default: mockApiClient,
}));

import ResetPasswordPage from '../ResetPasswordPage';

const strongPassword = 'Str0ng!Passw0rd';

describe('ResetPasswordPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // restore default params
    searchParams.set('email', 'user@example.com');
    searchParams.set('token', 'abc123token');
  });

  it('renders the reset form when email and token are present', () => {
    render(<ResetPasswordPage />);
    expect(screen.getByRole('heading', { name: /set new password/i })).toBeInTheDocument();
    expect(screen.getByText(/user@example.com/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /reset password/i })).toBeInTheDocument();
  });

  it('shows invalid link state when token is missing', () => {
    searchParams.delete('token');
    render(<ResetPasswordPage />);
    expect(screen.getByRole('heading', { name: /invalid reset link/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /request new link/i })).toBeInTheDocument();
  });

  it('rejects mismatched confirm password client-side', async () => {
    render(<ResetPasswordPage />);
    await userEvent.type(screen.getByLabelText(/^new password$/i), strongPassword);
    await userEvent.type(screen.getByLabelText(/confirm new password/i), 'Different1!');
    await userEvent.click(screen.getByRole('button', { name: /reset password/i }));

    expect(await screen.findByText(/passwords do not match/i)).toBeInTheDocument();
    expect(mockApiClient.post).not.toHaveBeenCalled();
  });

  it('submits new password and shows success', async () => {
    mockApiClient.post.mockResolvedValueOnce({
      data: { success: true, message: 'Password has been reset successfully.' },
    });

    render(<ResetPasswordPage />);
    await userEvent.type(screen.getByLabelText(/^new password$/i), strongPassword);
    await userEvent.type(screen.getByLabelText(/confirm new password/i), strongPassword);
    await userEvent.click(screen.getByRole('button', { name: /reset password/i }));

    await vi.waitFor(() => {
      expect(mockApiClient.post).toHaveBeenCalledWith('/Auth/reset-password', {
        email: 'user@example.com',
        token: 'abc123token',
        newPassword: strongPassword,
      });
    });

    expect(await screen.findByRole('heading', { name: /password updated/i })).toBeInTheDocument();
  });
});
