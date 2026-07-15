import { render, screen, fireEvent, waitFor } from '@testing-library/react';
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

const weeklySchedule = [
  { dayOfWeek: 0, isWorkingDay: false, startTime: null, endTime: null },
  { dayOfWeek: 1, isWorkingDay: true, startTime: '08:00', endTime: '18:00' },
  { dayOfWeek: 2, isWorkingDay: true, startTime: '08:00', endTime: '18:00' },
  { dayOfWeek: 3, isWorkingDay: true, startTime: '08:00', endTime: '18:00' },
  { dayOfWeek: 4, isWorkingDay: true, startTime: '08:00', endTime: '18:00' },
  { dayOfWeek: 5, isWorkingDay: true, startTime: '08:00', endTime: '18:00' },
  { dayOfWeek: 6, isWorkingDay: false, startTime: null, endTime: null },
];

// Monday far in the future
const FUTURE_MONDAY = '2099-06-15';

function mockDoctorApis({ bookedSlots = [] } = {}) {
  mockApiClient.get.mockImplementation((url) => {
    if (url === '/Doctors/5') {
      return Promise.resolve({
        data: { success: true, data: { fullName: 'Test Doctor', specialties: ['Cardiology'] } },
      });
    }
    if (url === '/Doctors/5/schedule') {
      return Promise.resolve({
        data: {
          success: true,
          data: {
            doctorId: 5,
            isActive: true,
            isAcceptingPatients: true,
            weeklySchedule,
          },
        },
      });
    }
    if (url === '/Doctors/5/availability') {
      return Promise.resolve({
        data: {
          success: true,
          data: {
            doctorId: 5,
            date: FUTURE_MONDAY,
            bookedSlots,
          },
        },
      });
    }
    return Promise.resolve({ data: { success: true, data: {} } });
  });
}

async function waitForDoctor() {
  expect(await screen.findByText(/Dr\. Test Doctor/i)).toBeInTheDocument();
}

function dateInput() {
  return document.querySelector('input[type="date"]#appointmentDate')
    || document.querySelector('input[type="date"]');
}

async function pickDateAndSlot(timeLabel = '10:00') {
  fireEvent.change(dateInput(), { target: { value: FUTURE_MONDAY } });

  await waitFor(() => {
    expect(mockApiClient.get).toHaveBeenCalledWith(
      '/Doctors/5/availability',
      expect.objectContaining({ params: { date: FUTURE_MONDAY } }),
    );
  });

  const slotBtn = await screen.findByRole('option', { name: timeLabel });
  await userEvent.click(slotBtn);
  return slotBtn;
}

describe('BookAppointmentPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockDoctorApis();
  });

  it('renders booking form without free-text datetime input', async () => {
    render(<BookAppointmentPage />);
    await waitForDoctor();

    expect(screen.getByRole('heading', { name: /book appointment/i })).toBeInTheDocument();
    expect(document.querySelector('input[type="datetime-local"]')).not.toBeInTheDocument();
    expect(dateInput()).toBeInTheDocument();
    expect(screen.getByText(/select a date to see free slots/i)).toBeInTheDocument();
    expect(document.querySelector('textarea')).toBeInTheDocument();
  });

  it('loads schedule and shows free slots for the selected day', async () => {
    render(<BookAppointmentPage />);
    await waitForDoctor();

    await waitFor(() => {
      expect(mockApiClient.get).toHaveBeenCalledWith('/Doctors/5/schedule');
    });

    fireEvent.change(dateInput(), { target: { value: FUTURE_MONDAY } });

    expect(await screen.findByRole('option', { name: '08:00' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: '10:00' })).toBeInTheDocument();
  });

  it('hides booked slots from the picker', async () => {
    const bookedLocal = new Date(`${FUTURE_MONDAY}T10:00:00`);
    mockDoctorApis({ bookedSlots: [{ startUtc: bookedLocal.toISOString(), status: 'Pending' }] });

    render(<BookAppointmentPage />);
    await waitForDoctor();

    fireEvent.change(dateInput(), { target: { value: FUTURE_MONDAY } });

    await screen.findByRole('option', { name: '09:30' });
    expect(screen.queryByRole('option', { name: '10:00' })).not.toBeInTheDocument();
    expect(screen.getByRole('option', { name: '10:30' })).toBeInTheDocument();
  });

  it('shows empty state when no free slots', async () => {
    // Saturday
    render(<BookAppointmentPage />);
    await waitForDoctor();

    fireEvent.change(dateInput(), { target: { value: '2099-06-20' } });

    await waitFor(() => {
      expect(mockApiClient.get).toHaveBeenCalledWith(
        '/Doctors/5/availability',
        expect.objectContaining({ params: { date: '2099-06-20' } }),
      );
    });

    expect(await screen.findByText(/no available slots on this day/i)).toBeInTheDocument();
  });

  it('requires a selected slot before submit', async () => {
    render(<BookAppointmentPage />);
    await waitForDoctor();

    await userEvent.type(
      document.querySelector('textarea'),
      'Regular checkup and consultation.',
    );
    fireEvent.submit(document.querySelector('form'));

    expect(await screen.findByText(/please select an available time slot/i)).toBeInTheDocument();
    expect(mockApiClient.post).not.toHaveBeenCalled();
  });

  it('rejects reason shorter than 10 characters', async () => {
    render(<BookAppointmentPage />);
    await waitForDoctor();
    await pickDateAndSlot('10:00');

    await userEvent.type(document.querySelector('textarea'), 'Short');
    fireEvent.submit(document.querySelector('form'));

    expect(await screen.findByText('Reason must be at least 10 characters')).toBeInTheDocument();
    expect(mockApiClient.post).not.toHaveBeenCalled();
  });

  it('sends selected slot ISO on valid submission and navigates to payment', async () => {
    mockApiClient.post.mockResolvedValue({ data: { success: true, data: { id: 99 } } });
    render(<BookAppointmentPage />);
    await waitForDoctor();
    await pickDateAndSlot('10:00');

    await userEvent.type(
      document.querySelector('textarea'),
      'Regular checkup and consultation.',
    );
    fireEvent.submit(document.querySelector('form'));

    await waitFor(() => {
      expect(mockApiClient.post).toHaveBeenCalledWith('/Appointments', {
        patientId: 1,
        doctorId: 5,
        scheduledTime: expect.any(String),
        reason: 'Regular checkup and consultation.',
        appointmentType: 'Standard',
      });
    });

    const payload = mockApiClient.post.mock.calls[0][1];
    const sent = new Date(payload.scheduledTime);
    expect(sent.getHours()).toBe(10);
    expect(sent.getMinutes()).toBe(0);

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/pay/99');
    });
  });
});
