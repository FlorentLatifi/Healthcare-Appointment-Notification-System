import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockApiClient } = vi.hoisted(() => ({
  mockApiClient: { get: vi.fn() },
}));

vi.mock('react-hot-toast', () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('../../services/apiClient', () => ({
  default: mockApiClient,
}));

import AdminAnalyticsPage from '../AdminAnalyticsPage';

describe('AdminAnalyticsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockApiClient.get.mockImplementation((url) => {
      if (url === '/Analytics/revenue') {
        return Promise.resolve({
          data: {
            success: true,
            data: {
              totalRevenue: 500,
              currency: 'USD',
              byDoctor: [{ doctorId: 1, doctorName: 'Dr. Ada', revenue: 500 }],
              bySpecialty: [],
            },
          },
        });
      }
      if (url === '/Analytics/no-show-rate') {
        return Promise.resolve({
          data: {
            success: true,
            data: {
              noShowRatePercent: 8,
              noShowCount: 2,
              completedCount: 20,
              confirmedCount: 3,
              totalCount: 25,
            },
          },
        });
      }
      if (url === '/Analytics/volume') {
        return Promise.resolve({
          data: {
            success: true,
            data: {
              groupBy: 'day',
              items: [{ period: '2026-07-10', created: 2, confirmed: 1, cancelled: 0 }],
            },
          },
        });
      }
      return Promise.resolve({ data: { success: false } });
    });
  });

  it('renders page header and KPI data from analytics endpoints', async () => {
    render(<AdminAnalyticsPage />);
    expect(screen.getByRole('heading', { name: /^analytics$/i })).toBeInTheDocument();

    await waitFor(() => {
      expect(mockApiClient.get).toHaveBeenCalledWith('/Analytics/revenue', expect.anything());
      expect(mockApiClient.get).toHaveBeenCalledWith('/Analytics/no-show-rate', expect.anything());
      expect(mockApiClient.get).toHaveBeenCalledWith('/Analytics/volume', expect.anything());
    });

    expect(await screen.findByText(/Dr\. Ada/i)).toBeInTheDocument();
    expect(screen.getByText(/8\.0%/)).toBeInTheDocument();
  });
});
