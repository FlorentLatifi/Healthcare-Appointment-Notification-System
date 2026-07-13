import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockApiClient } = vi.hoisted(() => ({
  mockApiClient: { post: vi.fn() },
}));

vi.mock('react-router-dom', () => ({
  Link: ({ children, to }) => <a href={to}>{children}</a>,
  useNavigate: () => vi.fn(),
}));

vi.mock('../../services/apiClient', () => ({
  default: mockApiClient,
}));

import ForgotPasswordPage from '../ForgotPasswordPage';

describe('ForgotPasswordPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the request form', () => {
    render(<ForgotPasswordPage />);
    expect(screen.getByRole('heading', { name: /forgot password/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /send reset link/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /back to login/i })).toHaveAttribute('href', '/login');
  });

  it('shows confirmation after successful request (no enumeration)', async () => {
    mockApiClient.post.mockResolvedValueOnce({
      data: { success: true, message: 'If the email address is registered...' },
    });

    render(<ForgotPasswordPage />);
    const emailInput = screen.getByLabelText(/email/i);
    await userEvent.type(emailInput, 'user@example.com');
    await userEvent.click(screen.getByRole('button', { name: /send reset link/i }));

    await vi.waitFor(() => {
      expect(screen.getByRole('heading', { name: /check your email/i })).toBeInTheDocument();
    });

    expect(mockApiClient.post).toHaveBeenCalledWith('/Auth/forgot-password', {
      email: 'user@example.com',
    });
    expect(screen.getByText(/user@example.com/)).toBeInTheDocument();
  });

  it('shows client-side error for invalid email', async () => {
    render(<ForgotPasswordPage />);
    await userEvent.type(screen.getByLabelText(/email/i), 'not-an-email');
    await userEvent.click(screen.getByRole('button', { name: /send reset link/i }));

    expect(await screen.findByText(/valid email/i)).toBeInTheDocument();
    expect(mockApiClient.post).not.toHaveBeenCalled();
  });
});
