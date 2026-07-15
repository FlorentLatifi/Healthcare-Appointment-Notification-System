import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockApiClient, mockNavigate, mockLocation } = vi.hoisted(() => ({
  mockApiClient: { get: vi.fn(), post: vi.fn() },
  mockNavigate: vi.fn(),
  mockLocation: { pathname: '/admin' },
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
  useLocation: () => mockLocation,
}));

vi.mock('react-hot-toast', () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('../../services/apiClient', () => ({
  default: mockApiClient,
}));

import AdminDashboardPage from '../AdminDashboardPage';

describe('AdminDashboardPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLocation.pathname = '/admin';
    mockApiClient.get.mockImplementation((url) => {
      if (url === '/Doctors') {
        return Promise.resolve({
          data: {
            success: true,
            data: { items: [], pageNumber: 1, totalPages: 1 },
          },
        });
      }
      if (url === '/Patients' || url === '/Patients/search') {
        return Promise.resolve({ data: { success: true, data: { items: [] } } });
      }
      if (url === '/Analytics/revenue') {
        return Promise.resolve({
          data: { success: true, data: { totalRevenue: 1000, currency: 'USD' } },
        });
      }
      if (url === '/Analytics/no-show-rate') {
        return Promise.resolve({
          data: { success: true, data: { noShowRatePercent: 5, totalCount: 20 } },
        });
      }
      if (url === '/Analytics/volume') {
        return Promise.resolve({
          data: { success: true, data: { items: [{ created: 2, confirmed: 1, cancelled: 0 }] } },
        });
      }
      return Promise.resolve({ data: { success: true, data: {} } });
    });
  });

  it('shows Doctors and Patients catalog tabs', async () => {
    render(<AdminDashboardPage />);
    expect(screen.getByRole('heading', { name: /admin catalog/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /doctors/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /patients/i })).toBeInTheDocument();
    await waitFor(() => expect(mockApiClient.get).toHaveBeenCalledWith('/Doctors', expect.anything()));
  });

  it('shows KPI cards and quick links', async () => {
    render(<AdminDashboardPage />);
    await waitFor(() => {
      expect(screen.getByTestId('admin-kpi-cards')).toBeInTheDocument();
    });
    expect(screen.getByText(/open analytics/i)).toBeInTheDocument();
    expect(screen.getByText(/audit logs/i)).toBeInTheDocument();
    expect(screen.getByText(/5\.0%/)).toBeInTheDocument();
  });

  it('navigates to patients route when Patients tab is clicked', async () => {
    render(<AdminDashboardPage />);
    await userEvent.click(screen.getByRole('tab', { name: /patients/i }));
    expect(mockNavigate).toHaveBeenCalledWith('/admin/patients');
  });

  it('loads patients when path is /admin/patients', async () => {
    mockLocation.pathname = '/admin/patients';
    render(<AdminDashboardPage />);
    await waitFor(() => {
      expect(mockApiClient.get).toHaveBeenCalledWith('/Patients', expect.anything());
    });
  });
});
