import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  formatRateLimitMessage,
  notifySessionExpired,
  setSessionExpiredHandler,
} from '../sessionExpiry';

describe('formatRateLimitMessage', () => {
  it('includes seconds when retry-after is under a minute', () => {
    expect(formatRateLimitMessage(12)).toMatch(/12 seconds/i);
  });

  it('uses minutes for longer waits', () => {
    expect(formatRateLimitMessage(90)).toMatch(/2 minutes/i);
  });

  it('prefers server message that already includes countdown', () => {
    const msg = 'Rate limit exceeded. Please try again in 30 seconds.';
    expect(formatRateLimitMessage(30, msg)).toBe(msg);
  });
});

describe('notifySessionExpired', () => {
  beforeEach(() => {
    setSessionExpiredHandler(null);
    sessionStorage.clear();
  });

  afterEach(() => {
    setSessionExpiredHandler(null);
  });

  it('invokes registered SPA handler', () => {
    const handler = vi.fn();
    setSessionExpiredHandler(handler);
    notifySessionExpired();
    expect(handler).toHaveBeenCalledTimes(1);
    expect(sessionStorage.getItem('session_expired')).toBe('1');
  });
});
