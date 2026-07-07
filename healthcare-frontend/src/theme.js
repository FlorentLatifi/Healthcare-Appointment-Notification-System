/*
 * Shared theme / design tokens module.
 *
 * Chosen approach (Option B, shared module) over (Option A, Tailwind commit)
 * because the codebase uses 100 % inline style objects with no existing
 * Tailwind integration.  Extracting constants into a JS module carries
 * zero build risk and can be adopted incrementally file by file without
 * touching the Vite / PostCSS configuration.
 *
 * Expected future additions:
 *   - styles.card, styles.input, styles.modal (common patterns)
 *   - button variant factories (primary, danger, success, ghost)
 */

// ── Color palette ─────────────────────────────────────────────────
export const colors = {
  primary: '#2563eb',
  primaryHover: '#1d4ed8',
  success: '#059669',
  danger: '#dc2626',

  textDark: '#333',
  textMuted: '#666',
  textLight: '#888',

  border: '#ccc',
  borderLight: '#ddd',

  cardBg: '#fff',
  bodyBg: '#f9f9f9',

  cancelBg: '#fee2e2',
  cancelBorder: '#fecaca',
  cancelText: '#991b1b',

  overlay: 'rgba(0,0,0,0.4)',
};

// ── Appointment-status badge colours ──────────────────────────────
export const STATUS_COLORS = {
  Scheduled: { bg: '#dbeafe', color: '#1e40af' },
  Confirmed: { bg: '#d1fae5', color: '#065f46' },
  InProgress: { bg: '#fef3c7', color: '#92400e' },
  Completed: { bg: '#e0e7ff', color: '#3730a3' },
  Cancelled: { bg: '#fee2e2', color: '#991b1b' },
  NoShow: { bg: '#f3e8ff', color: '#6b21a8' },
};

// ── Common style fragments ────────────────────────────────────────
export const badge = (status) => ({
  display: 'inline-block',
  fontSize: 12,
  padding: '3px 10px',
  borderRadius: 12,
  fontWeight: 600,
  background: (STATUS_COLORS[status] || { bg: '#f3f4f6' }).bg,
  color: (STATUS_COLORS[status] || { color: '#333' }).color,
});

export const card = {
  border: '1px solid #ddd',
  borderRadius: 8,
  padding: 16,
  marginBottom: 12,
  background: '#fff',
};
