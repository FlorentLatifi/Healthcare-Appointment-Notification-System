/**
 * Navigate to the session-expired screen after silent refresh fails.
 * Prefer SPA navigation when a handler is registered; fall back to location assign.
 */

let _handler = null;

export function setSessionExpiredHandler(fn) {
  _handler = typeof fn === 'function' ? fn : null;
}

export function notifySessionExpired() {
  try {
    sessionStorage.setItem('session_expired', '1');
  } catch {
    // ignore
  }

  if (typeof _handler === 'function') {
    try {
      _handler();
      return;
    } catch {
      // fall through to hard redirect
    }
  }

  if (typeof window !== 'undefined') {
    const path = window.location?.pathname || '';
    if (
      path.startsWith('/login')
      || path.startsWith('/register')
      || path.startsWith('/session-expired')
      || path.startsWith('/forgot-password')
      || path.startsWith('/reset-password')
    ) {
      return;
    }
    window.location.assign('/session-expired');
  }
}

/** Build a user-facing rate-limit message including Retry-After when known. */
export function formatRateLimitMessage(retryAfterSeconds, serverMessage) {
  if (serverMessage && /try again in \d+/i.test(serverMessage)) {
    return serverMessage;
  }
  if (retryAfterSeconds != null && Number.isFinite(Number(retryAfterSeconds))) {
    const sec = Math.max(1, Math.ceil(Number(retryAfterSeconds)));
    if (sec >= 60) {
      const mins = Math.ceil(sec / 60);
      return `Too many requests. Please try again in about ${mins} minute${mins === 1 ? '' : 's'}.`;
    }
    return `Too many requests. Please try again in ${sec} second${sec === 1 ? '' : 's'}.`;
  }
  if (serverMessage && !/rate limit/i.test(serverMessage)) {
    return serverMessage;
  }
  return 'Too many requests. Please wait a moment and try again.';
}
