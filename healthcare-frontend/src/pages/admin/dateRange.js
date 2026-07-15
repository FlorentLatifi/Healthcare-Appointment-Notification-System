/** Format a Date as yyyy-MM-dd for <input type="date"> and API query params. */
export function toDateInputValue(date) {
  if (!(date instanceof Date) || Number.isNaN(date.getTime())) return '';
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/** Default analytics window: last 30 days (inclusive of today). */
export function defaultDateRange(days = 30) {
  const to = new Date();
  const from = new Date();
  from.setDate(from.getDate() - (days - 1));
  return { from: toDateInputValue(from), to: toDateInputValue(to) };
}

/**
 * Build API-friendly ISO bounds for a calendar date range.
 * Uses start-of-day local for `from` and end-of-day local for `to`.
 */
export function toApiDateBounds(fromStr, toStr) {
  const dateFrom = fromStr ? new Date(`${fromStr}T00:00:00`) : null;
  const dateTo = toStr ? new Date(`${toStr}T23:59:59.999`) : null;
  return {
    dateFrom: dateFrom && !Number.isNaN(dateFrom.getTime()) ? dateFrom.toISOString() : undefined,
    dateTo: dateTo && !Number.isNaN(dateTo.getTime()) ? dateTo.toISOString() : undefined,
  };
}

export function formatMoney(amount, currency = 'USD') {
  const n = Number(amount);
  if (!Number.isFinite(n)) return '—';
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency: currency || 'USD',
      maximumFractionDigits: 2,
    }).format(n);
  } catch {
    return `${currency || 'USD'} ${n.toFixed(2)}`;
  }
}

export function formatDateTime(value) {
  if (!value) return '—';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return String(value);
  return d.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function outcomeBadgeStatus(outcome) {
  const o = String(outcome || '').toLowerCase();
  if (o === 'success' || o === 'succeeded') return 'Confirmed';
  if (o === 'failure' || o === 'failed' || o === 'error') return 'Cancelled';
  if (o === 'denied' || o === 'forbidden') return 'NoShow';
  return 'Pending';
}
