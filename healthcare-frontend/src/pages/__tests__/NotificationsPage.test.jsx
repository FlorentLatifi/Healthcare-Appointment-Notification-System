import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockApiClient } = vi.hoisted(() => ({
  mockApiClient: { get: vi.fn(), put: vi.fn() },
}));

vi.mock('react-hot-toast', () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('../../services/apiClient', () => ({
  default: mockApiClient,
}));

import NotificationsPage from '../NotificationsPage';

describe('NotificationsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockApiClient.get.mockImplementation((url) => {
      if (url === '/Notifications/unread-count') {
        return Promise.resolve({ data: { success: true, data: { count: 1 } } });
      }
      if (url === '/Notifications') {
        return Promise.resolve({
          data: {
            success: true,
            data: {
              items: [
                {
                  id: 1,
                  title: 'Appointment confirmed',
                  message: 'Your appointment APT-1 is confirmed.',
                  isRead: false,
                  createdAt: '2026-07-15T10:00:00Z',
                },
                {
                  id: 2,
                  title: 'Welcome',
                  message: 'Thanks for joining.',
                  isRead: true,
                  createdAt: '2026-07-14T10:00:00Z',
                },
              ],
              unreadCount: 1,
              totalCount: 2,
              pageNumber: 1,
              totalPages: 1,
            },
          },
        });
      }
      return Promise.resolve({ data: { success: true, data: {} } });
    });
    mockApiClient.put.mockResolvedValue({ data: { success: true } });
  });

  it('lists notifications and marks one as read', async () => {
    render(<NotificationsPage />);

    expect(await screen.findByText('Appointment confirmed')).toBeInTheDocument();
    expect(screen.getByText('Welcome')).toBeInTheDocument();
    expect(screen.getByText(/1 unread/i)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /mark as read/i }));

    await waitFor(() => {
      expect(mockApiClient.put).toHaveBeenCalledWith('/Notifications/1/read');
    });
  });

  it('marks all as read', async () => {
    render(<NotificationsPage />);
    await screen.findByText('Appointment confirmed');

    await userEvent.click(screen.getByRole('button', { name: /mark all as read/i }));

    await waitFor(() => {
      expect(mockApiClient.put).toHaveBeenCalledWith('/Notifications/read-all');
    });
  });
});
