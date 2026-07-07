import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockNavigate, mockApiClient } = vi.hoisted(() => ({
  mockNavigate: vi.fn(),
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
    Scheduled: { bg: '#dbeafe', color: '#1e40af' },
    Confirmed: { bg: '#d1fae5', color: '#065f46' },
    Completed: { bg: '#f3f4f6', color: '#374151' },
    Cancelled: { bg: '#fee2e2', color: '#991b1b' },
    NoShow: { bg: '#fef3c7', color: '#92400e' },
  },
}));

vi.mock('../../context/AuthContext', () => ({
  useAuth: () => ({ patientId: 1 }),
}));

import MyAppointmentsPage from '../MyAppointmentsPage';

const makeAppt = (id, status, overrides = {}) => ({
  id,
  status,
  referenceCode: `REF-${id}`,
  scheduledDate: '2026-07-15',
  scheduledTimeFormatted: '10:00 AM',
  doctor: { fullName: 'Test Doctor' },
  reason: 'Regular checkup',
  cancellationReason: null,
  ...overrides,
});

describe('MyAppointmentsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockApiClient.get.mockResolvedValue({ data: { success: true, data: { items: [] } } });
  });

  it('shows cancel button for Scheduled appointments', async () => {
    mockApiClient.get.mockResolvedValue({
      data: { success: true, data: { items: [makeAppt(1, 'Scheduled')] } },
    });
    render(<MyAppointmentsPage />);

    await vi.waitFor(() => expect(screen.getByText('REF-1')).toBeInTheDocument());

    expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
  });

  it('does not show cancel button for Confirmed appointments', async () => {
    mockApiClient.get.mockResolvedValue({
      data: { success: true, data: { items: [makeAppt(2, 'Confirmed')] } },
    });
    render(<MyAppointmentsPage />);

    await vi.waitFor(() => expect(screen.getByText('REF-2')).toBeInTheDocument());

    expect(screen.queryByRole('button', { name: /cancel/i })).not.toBeInTheDocument();
  });

  it('does not show cancel button for Completed appointments', async () => {
    mockApiClient.get.mockResolvedValue({
      data: { success: true, data: { items: [makeAppt(3, 'Completed')] } },
    });
    render(<MyAppointmentsPage />);

    await vi.waitFor(() => expect(screen.getByText('REF-3')).toBeInTheDocument());

    expect(screen.queryByRole('button', { name: /cancel/i })).not.toBeInTheDocument();
  });

  describe('cancel modal', () => {
    it('opens modal when clicking Cancel', async () => {
      mockApiClient.get.mockResolvedValue({
        data: { success: true, data: { items: [makeAppt(4, 'Scheduled')] } },
      });
      render(<MyAppointmentsPage />);

      await vi.waitFor(() => expect(screen.getByText('REF-4')).toBeInTheDocument());
      await userEvent.click(screen.getByRole('button', { name: /cancel/i }));

      expect(screen.getByText('Cancel Appointment')).toBeInTheDocument();
    });

    it('rejects reason shorter than 10 characters', async () => {
      mockApiClient.get.mockResolvedValue({
        data: { success: true, data: { items: [makeAppt(5, 'Scheduled')] } },
      });
      render(<MyAppointmentsPage />);

      await vi.waitFor(() => expect(screen.getByText('REF-5')).toBeInTheDocument());
      await userEvent.click(screen.getByRole('button', { name: /cancel/i }));

      await userEvent.type(screen.getByPlaceholderText(/reason for cancellation/i), 'Short');
      await userEvent.click(screen.getByRole('button', { name: /confirm cancel/i }));

      expect(screen.getByText('Cancellation reason must be at least 10 characters')).toBeInTheDocument();
      expect(mockApiClient.put).not.toHaveBeenCalled();
    });

    it('sends cancel request when reason is long enough', async () => {
      mockApiClient.get.mockResolvedValue({
        data: { success: true, data: { items: [makeAppt(6, 'Scheduled')] } },
      });
      mockApiClient.put.mockResolvedValue({ data: { success: true } });
      render(<MyAppointmentsPage />);

      await vi.waitFor(() => expect(screen.getByText('REF-6')).toBeInTheDocument());
      await userEvent.click(screen.getByRole('button', { name: /cancel/i }));

      await userEvent.type(
        screen.getByPlaceholderText(/reason for cancellation/i),
        'Schedule conflict, need to reschedule.',
      );
      await userEvent.click(screen.getByRole('button', { name: /confirm cancel/i }));

      await vi.waitFor(() => {
        expect(mockApiClient.put).toHaveBeenCalledWith('/Appointments/6/cancel', {
          appointmentId: 6,
          cancellationReason: 'Schedule conflict, need to reschedule.',
        });
      });
    });
  });
});
