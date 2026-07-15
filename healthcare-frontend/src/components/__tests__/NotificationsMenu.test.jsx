import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockApiClient, mockNavigate } = vi.hoisted(() => ({
  mockApiClient: { get: vi.fn(), put: vi.fn() },
  mockNavigate: vi.fn(),
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

import NotificationsMenu from '../NotificationsMenu';

describe('NotificationsMenu', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Force desktop dropdown path (jsdom may not define matchMedia)
    window.matchMedia = vi.fn().mockImplementation((query) => ({
      matches: false,
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }));

    mockApiClient.get.mockImplementation((url) => {
      if (url === '/Notifications/unread-count') {
        return Promise.resolve({ data: { success: true, data: { count: 2 } } });
      }
      if (url === '/Notifications') {
        return Promise.resolve({
          data: {
            success: true,
            data: {
              items: [
                {
                  id: 9,
                  title: 'Test note',
                  message: 'Hello',
                  isRead: false,
                  createdAt: '2026-07-15T12:00:00Z',
                },
              ],
              unreadCount: 2,
            },
          },
        });
      }
      return Promise.resolve({ data: { success: true, data: {} } });
    });
  });

  it('shows unread badge and opens dropdown list', async () => {
    render(<NotificationsMenu enabled />);

    await waitFor(() => {
      expect(screen.getByTestId('notifications-badge')).toHaveTextContent('2');
    });

    await userEvent.click(screen.getByTestId('notifications-bell'));

    expect(await screen.findByTestId('notifications-dropdown')).toBeInTheDocument();
    expect(screen.getByText('Test note')).toBeInTheDocument();
  });
});
