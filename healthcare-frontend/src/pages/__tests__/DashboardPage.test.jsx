import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockNavigate, mockApiClient, mockAuth } = vi.hoisted(() => ({
  mockNavigate: vi.fn(),
  mockApiClient: { get: vi.fn(), put: vi.fn(), post: vi.fn() },
  mockAuth: {
    user: { username: 'jdoe', role: 'Patient' },
    logout: vi.fn(),
    patientId: 1,
    doctorId: null,
  },
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
  useAuth: () => mockAuth,
}));

import DashboardPage from '../DashboardPage';

const patientProfile = {
  id: 1,
  firstName: 'Jane',
  lastName: 'Doe',
  fullName: 'Jane Doe',
  email: 'jane@example.com',
  phoneNumber: '555-0100',
  dateOfBirth: '1990-01-01',
  age: 36,
  gender: 'Female',
  address: '123 Main St',
};

const makeAppt = (id, status, overrides = {}) => ({
  id,
  status,
  referenceCode: `APT-20260715-00${id}`,
  scheduledDate: '2099-07-15',
  scheduledTimeFormatted: '10:00 AM',
  doctor: { fullName: 'Smith' },
  patient: { fullName: 'Jane Doe' },
  reason: 'Checkup',
  ...overrides,
});

describe('DashboardPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockAuth.user = { username: 'jdoe', role: 'Patient' };
    mockAuth.patientId = 1;
    mockAuth.doctorId = null;
    mockApiClient.get.mockImplementation((url) => {
      if (url.startsWith('/Patients/')) {
        return Promise.resolve({ data: { success: true, data: patientProfile } });
      }
      if (url.startsWith('/Appointments/patient/')) {
        return Promise.resolve({ data: { success: true, data: { items: [] } } });
      }
      if (url.startsWith('/Doctors/')) {
        return Promise.resolve({
          data: {
            success: true,
            data: {
              id: 7,
              fullName: 'Ada Lovelace',
              email: 'ada@example.com',
              phoneNumber: '555-0200',
              specialties: ['Cardiology'],
              yearsOfExperience: 10,
              isAcceptingPatients: true,
            },
          },
        });
      }
      if (url.startsWith('/Appointments/doctor/')) {
        return Promise.resolve({ data: { success: true, data: { items: [] } } });
      }
      return Promise.resolve({ data: { success: true, data: null } });
    });
  });

  it('shows personalized greeting with profile name after load', async () => {
    render(<DashboardPage />);

    expect(screen.getByTestId('dashboard-skeleton')).toBeInTheDocument();

    await vi.waitFor(() => {
      expect(screen.getAllByText(/Jane Doe/).length).toBeGreaterThanOrEqual(1);
    });

    expect(screen.getByRole('heading', { name: /dashboard/i })).toBeInTheDocument();
    expect(screen.getByText(/Welcome,/i)).toBeInTheDocument();
    expect(screen.queryByTestId('dashboard-skeleton')).not.toBeInTheDocument();
  });

  it('falls back to username when profile has no fullName', async () => {
    mockApiClient.get.mockImplementation((url) => {
      if (url.startsWith('/Patients/')) {
        return Promise.resolve({
          data: { success: true, data: { id: 1, email: 'x@y.com' } },
        });
      }
      if (url.startsWith('/Appointments/patient/')) {
        return Promise.resolve({ data: { success: true, data: { items: [] } } });
      }
      return Promise.resolve({ data: { success: true, data: null } });
    });

    render(<DashboardPage />);

    await vi.waitFor(() => {
      expect(screen.getByText('jdoe')).toBeInTheDocument();
    });
  });

  it('shows loading skeleton while fetching', () => {
    mockApiClient.get.mockReturnValue(new Promise(() => {}));
    render(<DashboardPage />);
    expect(screen.getByTestId('dashboard-skeleton')).toBeInTheDocument();
  });

  it('shows empty state when there are no upcoming appointments', async () => {
    render(<DashboardPage />);

    await vi.waitFor(() => {
      expect(screen.getByText(/no upcoming appointments/i)).toBeInTheDocument();
    });

    expect(screen.getByRole('button', { name: /browse doctors/i })).toBeInTheDocument();
  });

  it('shows upcoming appointments and stats', async () => {
    mockApiClient.get.mockImplementation((url) => {
      if (url.startsWith('/Patients/')) {
        return Promise.resolve({ data: { success: true, data: patientProfile } });
      }
      if (url.startsWith('/Appointments/patient/')) {
        return Promise.resolve({
          data: {
            success: true,
            data: {
              items: [
                makeAppt(1, 'Pending'),
                makeAppt(2, 'Confirmed'),
                makeAppt(3, 'Completed', { scheduledDate: '2020-01-01' }),
                makeAppt(4, 'Cancelled', { scheduledDate: '2020-02-01' }),
              ],
            },
          },
        });
      }
      return Promise.resolve({ data: { success: true, data: null } });
    });

    render(<DashboardPage />);

    await vi.waitFor(() => {
      expect(screen.getByText('APT-20260715-001')).toBeInTheDocument();
    });

    expect(screen.getByText('APT-20260715-002')).toBeInTheDocument();
    expect(screen.queryByText('APT-20260715-003')).not.toBeInTheDocument();

    // Stat labels (Pending/Confirmed also appear on badges)
    expect(screen.getByText('Upcoming')).toBeInTheDocument();
    expect(screen.getAllByText('Pending').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Completed')).toBeInTheDocument();
    expect(screen.getByText('Cancelled')).toBeInTheDocument();

    // Profile summary
    expect(screen.getByText('Profile Summary')).toBeInTheDocument();
    expect(screen.getByText('jane@example.com')).toBeInTheDocument();
  });

  it('shows error state with working Retry button', async () => {
    mockApiClient.get.mockRejectedValueOnce({
      response: { data: { message: 'Server unavailable' } },
    });

    render(<DashboardPage />);

    await vi.waitFor(() => {
      expect(screen.getByText(/server unavailable/i)).toBeInTheDocument();
    });

    const retry = screen.getByRole('button', { name: /retry/i });
    expect(retry).toBeInTheDocument();

    mockApiClient.get.mockImplementation((url) => {
      if (url.startsWith('/Patients/')) {
        return Promise.resolve({ data: { success: true, data: patientProfile } });
      }
      if (url.startsWith('/Appointments/patient/')) {
        return Promise.resolve({ data: { success: true, data: { items: [] } } });
      }
      return Promise.resolve({ data: { success: true, data: null } });
    });

    await userEvent.click(retry);

    await vi.waitFor(() => {
      expect(screen.getAllByText(/Jane Doe/).length).toBeGreaterThanOrEqual(1);
    });
    expect(screen.queryByText(/server unavailable/i)).not.toBeInTheDocument();
  });

  it('prompts patient to create profile when patientId is missing', async () => {
    mockAuth.patientId = null;
    render(<DashboardPage />);

    await vi.waitFor(() => {
      expect(screen.getByText(/create your patient profile/i)).toBeInTheDocument();
    });

    expect(mockApiClient.get).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: /create patient profile/i })).toBeInTheDocument();
  });

  it('loads doctor dashboard data when role is Doctor', async () => {
    mockAuth.user = { username: 'drada', role: 'Doctor' };
    mockAuth.patientId = null;
    mockAuth.doctorId = 7;

    mockApiClient.get.mockImplementation((url) => {
      if (url.startsWith('/Doctors/')) {
        return Promise.resolve({
          data: {
            success: true,
            data: {
              id: 7,
              fullName: 'Ada Lovelace',
              email: 'ada@example.com',
              phoneNumber: '555-0200',
              specialties: ['Cardiology'],
              yearsOfExperience: 10,
              isAcceptingPatients: true,
            },
          },
        });
      }
      if (url.startsWith('/Appointments/doctor/')) {
        return Promise.resolve({
          data: {
            success: true,
            data: { items: [makeAppt(9, 'Pending')] },
          },
        });
      }
      return Promise.resolve({ data: { success: true, data: null } });
    });

    render(<DashboardPage />);

    await vi.waitFor(() => {
      expect(screen.getAllByText(/Dr\. Ada Lovelace/).length).toBeGreaterThanOrEqual(1);
    });
    expect(screen.getByText('APT-20260715-009')).toBeInTheDocument();
    expect(screen.getByText(/Cardiology/)).toBeInTheDocument();
  });

  it('shows admin quick actions without fetching profile data', async () => {
    mockAuth.user = { username: 'admin1', role: 'Admin' };
    mockAuth.patientId = null;
    mockAuth.doctorId = null;

    render(<DashboardPage />);

    await vi.waitFor(() => {
      expect(screen.getByText('admin1')).toBeInTheDocument();
    });

    expect(mockApiClient.get).not.toHaveBeenCalled();
    expect(screen.getByText('Admin Dashboard')).toBeInTheDocument();
  });
});
