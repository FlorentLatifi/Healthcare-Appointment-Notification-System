import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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

import AdminAnalyticsPanel from '../admin/AdminAnalyticsPanel';

describe('AdminAnalyticsPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockApiClient.get.mockImplementation((url) => {
      if (url === '/Analytics/revenue') {
        return Promise.resolve({
          data: {
            success: true,
            data: {
              totalRevenue: 1250.5,
              currency: 'USD',
              byDoctor: [
                { doctorId: 1, doctorName: 'Dr. Ada Lovelace', revenue: 800 },
                { doctorId: 2, doctorName: 'Dr. Alan Turing', revenue: 450.5 },
              ],
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
              noShowRatePercent: 12.5,
              noShowCount: 5,
              completedCount: 30,
              confirmedCount: 5,
              totalCount: 40,
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
              items: [
                { period: '2026-07-01', created: 3, confirmed: 2, cancelled: 1 },
                { period: '2026-07-02', created: 1, confirmed: 4, cancelled: 0 },
              ],
            },
          },
        });
      }
      return Promise.resolve({ data: { success: false } });
    });
  });

  it('loads revenue, no-show-rate, and volume endpoints on mount', async () => {
    render(<AdminAnalyticsPanel />);

    await waitFor(() => {
      expect(mockApiClient.get).toHaveBeenCalledWith(
        '/Analytics/revenue',
        expect.objectContaining({
          params: expect.objectContaining({ groupBy: 'doctor' }),
        }),
      );
      expect(mockApiClient.get).toHaveBeenCalledWith(
        '/Analytics/no-show-rate',
        expect.objectContaining({ params: expect.any(Object) }),
      );
      expect(mockApiClient.get).toHaveBeenCalledWith(
        '/Analytics/volume',
        expect.objectContaining({
          params: expect.objectContaining({ groupBy: 'day' }),
        }),
      );
    });

    expect(await screen.findByText(/Dr\. Ada Lovelace/i)).toBeInTheDocument();
    expect(screen.getByText(/12\.5%/)).toBeInTheDocument();
    expect(screen.getByText('2026-07-01')).toBeInTheDocument();
  });

  it('refetches when Refresh is clicked', async () => {
    render(<AdminAnalyticsPanel />);
    await waitFor(() => expect(mockApiClient.get).toHaveBeenCalled());
    const callsBefore = mockApiClient.get.mock.calls.length;

    await userEvent.click(screen.getByRole('button', { name: /refresh/i }));

    await waitFor(() => {
      expect(mockApiClient.get.mock.calls.length).toBeGreaterThan(callsBefore);
    });
  });
});
