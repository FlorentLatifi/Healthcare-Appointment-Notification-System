import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import toast from 'react-hot-toast';

const { mockDownload } = vi.hoisted(() => ({
  mockDownload: vi.fn(),
}));

vi.mock('react-hot-toast', () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('../../services/calendarApi', () => ({
  downloadAppointmentIcs: (...args) => mockDownload(...args),
}));

import AddToCalendarButton from '../AddToCalendarButton';

describe('AddToCalendarButton', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockDownload.mockResolvedValue(undefined);
  });

  it('downloads ICS for the appointment id', async () => {
    render(<AddToCalendarButton appointmentId={12} referenceCode="APT-1" />);

    await userEvent.click(screen.getByRole('button', { name: /add appointment to calendar/i }));

    await waitFor(() => {
      expect(mockDownload).toHaveBeenCalledWith(12, { filename: 'APT-1.ics' });
    });
    expect(toast.success).toHaveBeenCalled();
  });

  it('shows error toast on failure', async () => {
    mockDownload.mockRejectedValue(new Error('Network down'));
    render(<AddToCalendarButton appointmentId={3} />);

    await userEvent.click(screen.getByRole('button', { name: /add appointment to calendar/i }));

    await waitFor(() => {
      expect(toast.error).toHaveBeenCalledWith('Network down');
    });
  });
});
