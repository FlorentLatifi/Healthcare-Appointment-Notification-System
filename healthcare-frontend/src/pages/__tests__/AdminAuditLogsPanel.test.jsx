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

import AdminAuditLogsPanel from '../admin/AdminAuditLogsPanel';

describe('AdminAuditLogsPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockApiClient.get.mockResolvedValue({
      data: {
        success: true,
        data: {
          items: [
            {
              id: 9,
              action: 'LoginSucceeded',
              resourceType: 'User',
              resourceId: 1,
              outcome: 'Success',
              actorUserId: 1,
              actorRole: 'Admin',
              occurredOn: '2026-07-15T10:00:00Z',
              details: 'Admin signed in from console',
              correlationId: 'corr-abc',
              clientIp: '127.0.0.1',
            },
          ],
          pageNumber: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        },
      },
    });
  });

  it('loads /AuditLogs on mount and renders rows', async () => {
    render(<AdminAuditLogsPanel />);

    await waitFor(() => {
      expect(mockApiClient.get).toHaveBeenCalledWith(
        '/AuditLogs',
        expect.objectContaining({
          params: expect.objectContaining({ pageNumber: 1, pageSize: 20 }),
        }),
      );
    });

    // Prefer table cells over the Action <select> options (same labels).
    const table = await screen.findByRole('table');
    expect(table).toHaveTextContent('LoginSucceeded');
    expect(table).toHaveTextContent('User #1');
    expect(table).toHaveTextContent('Success');
    expect(screen.getByText(/1 log/i)).toBeInTheDocument();
  });

  it('applies filters and opens detail modal', async () => {
    render(<AdminAuditLogsPanel />);
    await screen.findByRole('table');

    await userEvent.selectOptions(screen.getByLabelText(/^action$/i), 'LoginSucceeded');
    await userEvent.click(screen.getByRole('button', { name: /apply filters/i }));

    await waitFor(() => {
      const lastCall = mockApiClient.get.mock.calls.at(-1);
      expect(lastCall[0]).toBe('/AuditLogs');
      expect(lastCall[1].params.action).toBe('LoginSucceeded');
    });

    await userEvent.click(screen.getByRole('button', { name: /admin signed in/i }));
    expect(await screen.findByRole('heading', { name: /audit log detail/i })).toBeInTheDocument();
    expect(screen.getByText('corr-abc')).toBeInTheDocument();
    expect(screen.getByText('127.0.0.1')).toBeInTheDocument();
  });
});
