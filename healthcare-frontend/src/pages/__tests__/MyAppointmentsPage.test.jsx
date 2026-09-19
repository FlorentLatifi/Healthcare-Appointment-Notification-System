import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import toast from 'react-hot-toast';

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

vi.mock('../../context/AuthContext', () => ({
  useAuth: () => ({ patientId: 1 }),
}));

import MyAppointmentsPage from '../MyAppointmentsPage';

const makeAppt = (id, status, overrides = {}) => ({
  id,
  status,
  referenceCode: `REF-${id}`,
  doctorId: 7,
  scheduledDate: '2026-07-15',
  scheduledTimeFormatted: '10:00 AM',
  doctor: { id: 7, fullName: 'Test Doctor' },
  reason: 'Regular checkup',
  cancellationReason: null,
  ...overrides,
});

describe('MyAppointmentsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockApiClient.get.mockResolvedValue({ data: { success: true, data: { items: [] } } });
  });

  it('shows cancel + book-again helper for Pending appointments', async () => {
    mockApiClient.get.mockResolvedValue({
      data: { success: true, data: { items: [makeAppt(1, 'Pending')] } },
    });
    render(<MyAppointmentsPage />);

    await vi.waitFor(() => expect(screen.getByText('REF-1')).toBeInTheDocument());

    expect(screen.getByRole('button', { name: /cancel appointment/i })).toBeInTheDocument();
    expect(screen.getAllByText(/need a different time/i).length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText(/book a new one|no separate reschedule|do not offer in-place reschedule/i).length)
      .toBeGreaterThanOrEqual(1);
  });

  it('allows cancel for Confirmed appointments', async () => {
    mockApiClient.get.mockResolvedValue({
      data: { success: true, data: { items: [makeAppt(2, 'Confirmed')] } },
    });
    render(<MyAppointmentsPage />);

    await vi.waitFor(() => expect(screen.getByText('REF-2')).toBeInTheDocument());

    expect(screen.getByRole('button', { name: /cancel appointment/i })).toBeInTheDocument();
  });

  it('does not show cancel for Completed appointments', async () => {
    mockApiClient.get.mockResolvedValue({
      data: { success: true, data: { items: [makeAppt(3, 'Completed')] } },
    });
    render(<MyAppointmentsPage />);

    await vi.waitFor(() => expect(screen.getByText('REF-3')).toBeInTheDocument());

    expect(screen.queryByRole('button', { name: /cancel appointment/i })).not.toBeInTheDocument();
  });

  it('shows book again on cancelled cards', async () => {
    mockApiClient.get.mockResolvedValue({
      data: {
        success: true,
        data: {
          items: [
            makeAppt(8, 'Cancelled', { cancellationReason: 'Schedule conflict, need another time.' }),
          ],
        },
      },
    });
    render(<MyAppointmentsPage />);

    await vi.waitFor(() => expect(screen.getByText('REF-8')).toBeInTheDocument());
    expect(screen.getByRole('button', { name: /book again with same doctor/i })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /book again with same doctor/i }));
    expect(mockNavigate).toHaveBeenCalledWith('/book-appointment/7');
  });

  describe('cancel modal', () => {
    it('opens modal when clicking Cancel appointment', async () => {
      mockApiClient.get.mockResolvedValue({
        data: { success: true, data: { items: [makeAppt(4, 'Pending')] } },
      });
      render(<MyAppointmentsPage />);

      await vi.waitFor(() => expect(screen.getByText('REF-4')).toBeInTheDocument());
      await userEvent.click(screen.getByRole('button', { name: /cancel appointment/i }));

      expect(screen.getByRole('heading', { name: /cancel appointment/i })).toBeInTheDocument();
      expect(screen.getByText(/there is no separate reschedule action/i)).toBeInTheDocument();
    });

    it('rejects reason shorter than 10 characters', async () => {
      mockApiClient.get.mockResolvedValue({
        data: { success: true, data: { items: [makeAppt(5, 'Pending')] } },
      });
      render(<MyAppointmentsPage />);

      await vi.waitFor(() => expect(screen.getByText('REF-5')).toBeInTheDocument());
      await userEvent.click(screen.getByRole('button', { name: /cancel appointment/i }));

      await userEvent.type(screen.getByPlaceholderText(/reason for cancellation/i), 'Short');
      await userEvent.click(screen.getByRole('button', { name: /confirm cancel/i }));

      expect(screen.getByText('Cancellation reason must be at least 10 characters')).toBeInTheDocument();
      expect(mockApiClient.put).not.toHaveBeenCalled();
    });

    it('cancels then shows rebook prompt and can book same doctor', async () => {
      mockApiClient.get
        .mockResolvedValueOnce({
          data: { success: true, data: { items: [makeAppt(6, 'Pending')] } },
        })
        .mockResolvedValue({
          data: {
            success: true,
            data: {
              items: [
                makeAppt(6, 'Cancelled', {
                  cancellationReason: 'Schedule conflict, need to book another time.',
                }),
              ],
            },
          },
        });
      mockApiClient.put.mockResolvedValue({ data: { success: true } });
      render(<MyAppointmentsPage />);

      await vi.waitFor(() => expect(screen.getByText('REF-6')).toBeInTheDocument());
      await userEvent.click(screen.getByRole('button', { name: /cancel appointment/i }));

      await userEvent.type(
        screen.getByPlaceholderText(/reason for cancellation/i),
        'Schedule conflict, need to book another time.',
      );
      await userEvent.click(screen.getByRole('button', { name: /confirm cancel/i }));

      await vi.waitFor(() => {
        expect(mockApiClient.put).toHaveBeenCalledWith('/Appointments/6/cancel', {
          appointmentId: 6,
          cancellationReason: 'Schedule conflict, need to book another time.',
        });
      });

      expect(toast.success).toHaveBeenCalled();
      expect(await screen.findByTestId('rebook-prompt')).toBeInTheDocument();
      expect(screen.getByText(/appointment cancelled/i)).toBeInTheDocument();

      await userEvent.click(screen.getByRole('button', { name: /book a new one with same doctor/i }));
      expect(mockNavigate).toHaveBeenCalledWith('/book-appointment/7');
    });
  });
});
