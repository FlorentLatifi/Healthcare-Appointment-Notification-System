import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockNavigate, mockApiClient } = vi.hoisted(() => ({
  mockNavigate: vi.fn(),
  mockApiClient: { get: vi.fn(), post: vi.fn() },
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
  useParams: () => ({ doctorId: '5' }),
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

import BookAppointmentPage from '../BookAppointmentPage';

describe('BookAppointmentPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockApiClient.get.mockResolvedValue({
      data: { success: true, data: { fullName: 'Test Doctor', specialties: ['Cardiology'] } },
    });
  });

  async function fillAndSubmit(datetime, reason) {
    if (datetime) {
      const dtInput = document.querySelector('input[type="datetime-local"]');
      fireEvent.input(dtInput, { target: { value: datetime } });
    }
    if (reason) {
      await userEvent.type(document.querySelector('textarea'), reason);
    }
    fireEvent.submit(document.querySelector('form'));
  }

  it('renders the booking form', () => {
    render(<BookAppointmentPage />);
    expect(screen.getByRole('heading', { name: /book appointment/i })).toBeInTheDocument();
    expect(document.querySelector('input[type="datetime-local"]')).toBeInTheDocument();
    expect(document.querySelector('textarea')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /book appointment/i })).toBeInTheDocument();
  });

  it('rejects past datetime', async () => {
    render(<BookAppointmentPage />);
    await fillAndSubmit('2020-01-01T10:00', 'Past date test reason text here.');
    expect(screen.getByText('Cannot be in the past')).toBeInTheDocument();
    expect(mockApiClient.post).not.toHaveBeenCalled();
  });

  it('rejects non-30-minute intervals', async () => {
    render(<BookAppointmentPage />);
    await fillAndSubmit('2099-06-15T10:07', 'Interval test reason text right here.');
    expect(screen.getByText(/time must be in 30-minute intervals/i)).toBeInTheDocument();
    expect(mockApiClient.post).not.toHaveBeenCalled();
  });

  it('rejects reason shorter than 10 characters', async () => {
    render(<BookAppointmentPage />);
    await fillAndSubmit('2099-06-15T10:00', 'Short');
    expect(screen.getByText('Reason must be at least 10 characters')).toBeInTheDocument();
    expect(mockApiClient.post).not.toHaveBeenCalled();
  });

  it('sends correct payload on valid submission', async () => {
    mockApiClient.post.mockResolvedValue({ data: { success: true } });
    render(<BookAppointmentPage />);
    await fillAndSubmit('2099-06-15T10:00', 'Regular checkup and consultation.');

    expect(mockApiClient.post).toHaveBeenCalledWith('/Appointments', {
      patientId: 1,
      doctorId: 5,
      scheduledTime: expect.any(String),
      reason: 'Regular checkup and consultation.',
      appointmentType: 'Standard',
    });
  });

  it('navigates to my-appointments on success', async () => {
    mockApiClient.post.mockResolvedValue({ data: { success: true } });
    render(<BookAppointmentPage />);
    await fillAndSubmit('2099-06-15T10:00', 'Regular checkup and consultation.');

    await vi.waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/my-appointments');
    });
  });
});
