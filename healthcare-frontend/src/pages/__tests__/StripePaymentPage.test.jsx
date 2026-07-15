import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockNavigate, mockApiClient } = vi.hoisted(() => ({
  mockNavigate: vi.fn(),
  mockApiClient: { get: vi.fn(), post: vi.fn() },
}));

let mockConfirmPayment;

vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
  useParams: () => ({ appointmentId: '42' }),
}));

vi.mock('react-hot-toast', () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('../../services/apiClient', () => ({
  default: mockApiClient,
}));

vi.mock('../../context/AuthContext', () => ({
  useAuth: () => ({ patientId: 1 }),
}));

vi.mock('@stripe/stripe-js', () => ({
  loadStripe: () => Promise.resolve({}),
}));

vi.mock('@stripe/react-stripe-js', () => ({
  Elements: ({ children }) => <div data-testid="stripe-elements">{children}</div>,
  PaymentElement: () => <div data-testid="payment-element" />,
  useStripe: () => ({
    confirmPayment: mockConfirmPayment,
  }),
  useElements: () => ({}),
}));

import StripePaymentPage from '../StripePaymentPage';

const makeAppointment = (overrides = {}) => ({
  id: 42,
  status: 'Pending',
  referenceCode: 'REF-42',
  scheduledDate: '2026-07-15',
  scheduledTimeFormatted: '10:00 AM',
  doctor: { fullName: 'Test Doctor' },
  reason: 'Regular checkup',
  consultationFeeAmount: 75.00,
  consultationFeeCurrency: 'USD',
  ...overrides,
});

describe('StripePaymentPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockConfirmPayment = vi.fn();

    mockApiClient.get.mockResolvedValue({
      data: { success: true, data: makeAppointment() },
    });

    mockApiClient.post.mockResolvedValue({
      data: {
        success: true,
        data: {
          clientSecret: 'pi_42_secret_test',
          paymentIntentId: 'pi_42',
          amount: 75.00,
          currency: 'USD',
          appointmentId: 42,
        },
      },
    });
  });

  it('renders appointment summary and Stripe payment form', async () => {
    render(<StripePaymentPage />);

    await vi.waitFor(() => {
      expect(screen.getByText('REF-42')).toBeInTheDocument();
    });

    expect(screen.getByText('Complete Payment')).toBeInTheDocument();
    expect(screen.getByTestId('stripe-elements')).toBeInTheDocument();
    expect(screen.getByTestId('payment-element')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /pay/i })).toBeInTheDocument();
  });

  it('handles successful payment: confirms, reconciles, navigates to appointments', async () => {
    mockConfirmPayment.mockResolvedValue({
      paymentIntent: { id: 'pi_42', status: 'succeeded' },
      error: null,
    });

    mockApiClient.post.mockImplementation(async (url) => {
      if (url === '/Payments/create-intent') {
        return {
          data: {
            success: true,
            data: { clientSecret: 'pi_42_secret_test', paymentIntentId: 'pi_42', amount: 75.00, currency: 'USD', appointmentId: 42 },
          },
        };
      }
      if (url === '/Payments/process') {
        return { data: { success: true, data: 1 } };
      }
      return { data: { success: true } };
    });

    render(<StripePaymentPage />);

    await vi.waitFor(() => {
      expect(screen.getByTestId('payment-element')).toBeInTheDocument();
    });

    await userEvent.click(screen.getByRole('button', { name: /pay/i }));

    await vi.waitFor(() => {
      expect(mockConfirmPayment).toHaveBeenCalled();
      expect(mockApiClient.post).toHaveBeenCalledWith('/Payments/process', {
        appointmentId: 42,
        paymentIntentId: 'pi_42',
      });
      expect(mockNavigate).toHaveBeenCalledWith('/my-appointments');
    });
  });

  it('shows error and keeps appointment Pending when payment fails', async () => {
    mockConfirmPayment.mockResolvedValue({
      paymentIntent: null,
      error: { message: 'Your card was declined. Please try a different payment method.' },
    });

    render(<StripePaymentPage />);

    await vi.waitFor(() => {
      expect(screen.getByTestId('payment-element')).toBeInTheDocument();
    });

    await userEvent.click(screen.getByRole('button', { name: /pay/i }));

    await vi.waitFor(() => {
      expect(
        screen.getByText(/card was declined/i),
      ).toBeInTheDocument();
    });

    expect(mockApiClient.post).not.toHaveBeenCalledWith('/Payments/process', expect.anything());
    expect(mockNavigate).not.toHaveBeenCalled();
  });

  it('redirects to my-appointments when appointment already paid', async () => {
    mockApiClient.post.mockResolvedValue({
      data: {
        success: false,
        message: 'Payment has already been processed for this appointment',
      },
    });

    render(<StripePaymentPage />);

    await vi.waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/my-appointments');
    });
  });

  it('shows error when appointment fetch fails', async () => {
    mockApiClient.get.mockResolvedValue({
      data: { success: false, message: 'Appointment not found' },
    });

    render(<StripePaymentPage />);

    await vi.waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/my-appointments');
    });
  });
});
