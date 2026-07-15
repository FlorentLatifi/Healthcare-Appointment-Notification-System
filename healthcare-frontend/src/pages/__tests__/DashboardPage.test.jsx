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
    sessionReady: true,
    refreshSession: vi.fn().mockResolvedValue({ patientId: 1 }),
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
  doctor: { fullName: 'Smith', specialties: ['Cardiology'] },
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
    mockAuth.sessionReady = true;
    mockAuth.refreshSession = vi.fn().mockResolvedValue({ patientId: 1 });
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

  it('shows personalized Welcome back greeting with first name after load', async () => {
    render(<DashboardPage />);

    expect(screen.getByTestId('dashboard-skeleton')).toBeInTheDocument();

    await vi.waitFor(() => {
      expect(screen.getAllByText(/Jane/).length).toBeGreaterThanOrEqual(1);
    });

    expect(screen.getByRole('heading', { name: /dashboard/i })).toBeInTheDocument();
    expect(screen.getByText(/Welcome back,/i)).toBeInTheDocument();
    expect(screen.queryByTestId('dashboard-skeleton')).not.toBeInTheDocument();
  });

  it('falls back to username when profile has no name fields', async () => {
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

  it('shows empty state with Book Appointment CTA when no upcoming', async () => {
    render(<DashboardPage />);

    await vi.waitFor(() => {
      expect(screen.getByText(/no upcoming appointments/i)).toBeInTheDocument();
    });

    expect(screen.getAllByRole('button', { name: /book appointment/i }).length).toBeGreaterThanOrEqual(1);
  });

  it('shows upcoming appointments and patient stats', async () => {
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

    expect(screen.getByTestId('dashboard-stats')).toBeInTheDocument();
    expect(screen.getByText('Upcoming')).toBeInTheDocument();
    expect(screen.getByText('Completed')).toBeInTheDocument();
    expect(screen.getByText('Next appointment')).toBeInTheDocument();
    expect(screen.getByText('Total')).toBeInTheDocument();
    expect(screen.getAllByText(/2099-07-15/).length).toBeGreaterThanOrEqual(1);

    expect(screen.getByText('Profile Summary')).toBeInTheDocument();
    expect(screen.getByText('jane@example.com')).toBeInTheDocument();
    expect(screen.getByText(/Book Appointment/i)).toBeInTheDocument();
    expect(screen.getByText(/View Appointments/i)).toBeInTheDocument();
    expect(screen.getAllByText(/Edit Profile/i).length).toBeGreaterThanOrEqual(1);
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
      expect(screen.getAllByText(/Jane/).length).toBeGreaterThanOrEqual(1);
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
    expect(mockAuth.refreshSession).toHaveBeenCalled();
    expect(screen.getAllByRole('button', { name: /create patient profile/i }).length).toBeGreaterThanOrEqual(1);
  });

  it('treats patientId 0 as unlinked', async () => {
    mockAuth.patientId = 0;
    render(<DashboardPage />);

    await vi.waitFor(() => {
      expect(screen.getByText(/create your patient profile/i)).toBeInTheDocument();
    });
    expect(mockApiClient.get).not.toHaveBeenCalled();
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
    expect(screen.getByText('Analytics')).toBeInTheDocument();
    expect(screen.getByText('Audit Logs')).toBeInTheDocument();
    expect(screen.getByText('Manage Catalog')).toBeInTheDocument();
  });
});
