import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockNavigate, mockRefreshSession, mockAuthContext, mockApiClient } = vi.hoisted(() => ({
  mockNavigate: vi.fn(),
  mockRefreshSession: vi.fn(),
  mockAuthContext: { useAuth: vi.fn() },
  mockApiClient: { get: vi.fn(), put: vi.fn(), post: vi.fn() },
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
}));

vi.mock('react-hot-toast', () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('../../services/apiClient', () => ({
  default: mockApiClient,
}));

vi.mock('../../theme', () => ({
  STATUS_COLORS: {
    Pending: { bg: '#E8E4DB', color: '#5C5546' },
    Scheduled: { bg: '#E8E4DB', color: '#5C5546' },
    Confirmed: { bg: '#d1fae5', color: '#065f46' },
    Completed: { bg: '#f3f4f6', color: '#374151' },
    Cancelled: { bg: '#fee2e2', color: '#991b1b' },
    NoShow: { bg: '#fef3c7', color: '#92400e' },
  },
}));

vi.mock('../../context/AuthContext', () => ({
  useAuth: () => mockAuthContext.useAuth(),
}));

import DoctorDashboardPage from '../DoctorDashboardPage';

const makeAppt = (id, status, overrides = {}) => ({
  id,
  status,
  referenceCode: `REF-${id}`,
  scheduledDate: '2026-07-15',
  scheduledTimeFormatted: '10:00 AM',
  patient: { fullName: 'Test Patient', email: 'patient@test.com' },
  reason: 'Regular checkup',
  doctorNotes: null,
  cancellationReason: null,
  ...overrides,
});

describe('DoctorDashboardPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockRefreshSession.mockResolvedValue({ doctorId: 9 });
    mockAuthContext.useAuth.mockReturnValue({ doctorId: null, refreshSession: mockRefreshSession });
  });

  describe('create-profile mode (no doctorId in JWT)', () => {
    it('shows create doctor profile form instead of email lookup spoofing', () => {
      render(<DoctorDashboardPage />);
      expect(screen.getByRole('heading', { name: /create doctor profile/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /create profile/i })).toBeInTheDocument();
      expect(screen.queryByPlaceholderText(/email address/i)).not.toBeInTheDocument();
    });

    it('creates profile then refreshes session for doctor_id claim', async () => {
      mockApiClient.post.mockResolvedValue({ data: { success: true, data: 9 } });

      render(<DoctorDashboardPage />);

      const form = screen.getByLabelText(/create doctor profile form/i);
      const inputs = form.querySelectorAll('input');
      // firstName, lastName, email, phone, license, fee, currency, years
      await userEvent.type(inputs[0], 'Jane');
      await userEvent.type(inputs[1], 'Smith');
      await userEvent.type(inputs[2], 'jane@clinic.com');
      await userEvent.type(inputs[3], '+38349987654');
      await userEvent.type(inputs[4], 'MED-99999');

      await userEvent.click(screen.getByRole('button', { name: /create profile/i }));

      await vi.waitFor(() => {
        expect(mockApiClient.post).toHaveBeenCalledWith('/Doctors', expect.objectContaining({
          firstName: 'Jane',
          lastName: 'Smith',
          email: 'jane@clinic.com',
          licenseNumber: 'MED-99999',
        }));
      });
      expect(mockRefreshSession).toHaveBeenCalled();
    });
  });

  describe('dashboard mode (with doctorId from JWT)', () => {
    beforeEach(() => {
      mockAuthContext.useAuth.mockReturnValue({ doctorId: 1, refreshSession: mockRefreshSession });
      mockApiClient.get.mockResolvedValue({ data: { success: true, data: { items: [] } } });
    });

    it('shows Confirm, Complete and No-Show for Pending appointments', async () => {
      mockApiClient.get.mockResolvedValue({
        data: { success: true, data: { items: [makeAppt(1, 'Pending')] } },
      });
      render(<DoctorDashboardPage />);

      await vi.waitFor(() => expect(screen.getByText('REF-1')).toBeInTheDocument());

      expect(screen.getByRole('button', { name: 'Confirm' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Complete' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'No-Show' })).toBeInTheDocument();
    });

    it('shows only Complete for Confirmed appointments', async () => {
      mockApiClient.get.mockResolvedValue({
        data: { success: true, data: { items: [makeAppt(2, 'Confirmed')] } },
      });
      render(<DoctorDashboardPage />);

      await vi.waitFor(() => expect(screen.getByText('REF-2')).toBeInTheDocument());

      expect(screen.getByRole('button', { name: 'Complete' })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Confirm' })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'No-Show' })).not.toBeInTheDocument();
    });

    it('shows no action buttons for Completed or Cancelled', async () => {
      mockApiClient.get.mockResolvedValue({
        data: { success: true, data: { items: [makeAppt(3, 'Completed'), makeAppt(4, 'Cancelled')] } },
      });
      render(<DoctorDashboardPage />);

      await vi.waitFor(() => expect(screen.getByText('REF-3')).toBeInTheDocument());

      expect(screen.queryByRole('button', { name: 'Confirm' })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Complete' })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'No-Show' })).not.toBeInTheDocument();
    });

    describe('complete modal', () => {
      it('rejects short doctor notes', async () => {
        mockApiClient.get.mockResolvedValue({
          data: { success: true, data: { items: [makeAppt(5, 'Pending')] } },
        });
        render(<DoctorDashboardPage />);

        await vi.waitFor(() => expect(screen.getByText('REF-5')).toBeInTheDocument());
        await userEvent.click(screen.getByRole('button', { name: 'Confirm' }));

        await userEvent.click(screen.getByRole('button', { name: 'Complete' }));
        await userEvent.type(screen.getByPlaceholderText(/clinical notes/i), 'Short');
        await userEvent.click(screen.getAllByRole('button', { name: /^complete$/i }).pop());

        await vi.waitFor(() => {
          expect(screen.getByText('Doctor notes must be at least 20 characters')).toBeInTheDocument();
        });
        expect(mockApiClient.put).not.toHaveBeenCalled();
      });
    });

    describe('confirm modal', () => {
      it('shows override reason field when override payment is checked', async () => {
        mockApiClient.get.mockResolvedValue({
          data: { success: true, data: { items: [makeAppt(6, 'Pending')] } },
        });
        render(<DoctorDashboardPage />);

        await vi.waitFor(() => expect(screen.getByText('REF-6')).toBeInTheDocument());
        await userEvent.click(screen.getByRole('button', { name: 'Confirm' }));

        await userEvent.click(screen.getByLabelText(/override payment/i));

        expect(document.querySelector('input:not([type="checkbox"])')).toBeInTheDocument();
      });

      it('sends overrideReason with confirm request when checked', async () => {
        mockApiClient.get.mockResolvedValue({
          data: { success: true, data: { items: [makeAppt(7, 'Pending')] } },
        });
        mockApiClient.put.mockResolvedValue({ data: { success: true } });
        render(<DoctorDashboardPage />);

        await vi.waitFor(() => expect(screen.getByText('REF-7')).toBeInTheDocument());
        await userEvent.click(screen.getByRole('button', { name: 'Confirm' }));

        await userEvent.click(screen.getByLabelText(/override payment/i));
        await userEvent.type(document.querySelector('input:not([type="checkbox"])'), 'Admin waiver');

        await userEvent.click(screen.getAllByRole('button', { name: /^confirm$/i }).pop());

        await vi.waitFor(() => {
          expect(mockApiClient.put).toHaveBeenCalledWith('/Appointments/7/confirm', {
            appointmentId: 7,
            overridePaymentRequirement: true,
            overrideReason: 'Admin waiver',
          });
        });
      });
    });
  });
});
