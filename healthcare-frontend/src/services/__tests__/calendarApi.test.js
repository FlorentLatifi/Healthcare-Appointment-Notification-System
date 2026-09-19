import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

const { mockGet } = vi.hoisted(() => ({
  mockGet: vi.fn(),
}));

vi.mock('../apiClient', () => ({
  default: { get: (...args) => mockGet(...args) },
}));

import { downloadAppointmentIcs } from '../calendarApi';

describe('downloadAppointmentIcs', () => {
  let createObjectURL;
  let revokeObjectURL;
  let clickSpy;

  beforeEach(() => {
    vi.clearAllMocks();
    createObjectURL = vi.fn(() => 'blob:mock-url');
    revokeObjectURL = vi.fn();
    globalThis.URL.createObjectURL = createObjectURL;
    globalThis.URL.revokeObjectURL = revokeObjectURL;

    clickSpy = vi.fn();
    vi.spyOn(document, 'createElement').mockImplementation((tag) => {
      if (tag === 'a') {
        return {
          href: '',
          download: '',
          rel: '',
          click: clickSpy,
          remove: vi.fn(),
        };
      }
      return document.createElement.bind(document)(tag);
    });
    vi.spyOn(document.body, 'appendChild').mockImplementation((el) => el);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('requests ICS blob and triggers download as appointment.ics by default naming', async () => {
    const blob = new Blob(['BEGIN:VCALENDAR'], { type: 'text/calendar' });
    mockGet.mockResolvedValue({
      data: blob,
      headers: { 'content-type': 'text/calendar; charset=utf-8' },
    });

    await downloadAppointmentIcs(42, { filename: 'appointment.ics' });

    expect(mockGet).toHaveBeenCalledWith('/Appointments/42/calendar.ics', {
      responseType: 'blob',
      headers: { Accept: 'text/calendar, text/plain, */*' },
    });
    expect(createObjectURL).toHaveBeenCalled();
    expect(clickSpy).toHaveBeenCalled();
  });

  it('throws when server returns JSON error blob', async () => {
    const errBlob = new Blob(
      [JSON.stringify({ message: 'Forbidden' })],
      { type: 'application/json' },
    );
    // jsdom Blob has text()
    mockGet.mockResolvedValue({
      data: errBlob,
      headers: { 'content-type': 'application/json' },
    });

    await expect(downloadAppointmentIcs(1)).rejects.toThrow(/Forbidden|Could not download/i);
  });
});
