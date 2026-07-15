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

import AdminAuditLogsPage from '../AdminAuditLogsPage';

describe('AdminAuditLogsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockApiClient.get.mockResolvedValue({
      data: {
        success: true,
        data: {
          items: [
            {
              id: 3,
              action: 'BookAppointment',
              resourceType: 'Appointment',
              resourceId: 12,
              outcome: 'Success',
              actorUserId: 7,
              actorRole: 'Patient',
              occurredOn: '2026-07-15T12:00:00Z',
              details: 'Booked appointment #12',
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

  it('renders page header and paginated audit table', async () => {
    render(<AdminAuditLogsPage />);
    expect(screen.getByRole('heading', { name: /audit logs/i })).toBeInTheDocument();

    await waitFor(() => {
      expect(mockApiClient.get).toHaveBeenCalledWith(
        '/AuditLogs',
        expect.objectContaining({
          params: expect.objectContaining({ pageNumber: 1, pageSize: 20 }),
        }),
      );
    });

    const table = await screen.findByRole('table');
    expect(table).toHaveTextContent('BookAppointment');
    expect(table).toHaveTextContent('Appointment #12');
    expect(table).toHaveTextContent('Success');
  });
});
