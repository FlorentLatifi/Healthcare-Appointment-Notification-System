/* Design tokens are now defined in index.css via @theme.
   This file re-exports the CSS custom properties for JS usage. */

export const STATUS_COLORS = {
  Scheduled: { bg: '#E8E4DB', color: '#5C5546' },
  Confirmed: { bg: '#DCE8E2', color: '#3D6656' },
  InProgress: { bg: '#EAE6D4', color: '#7A6E3A' },
  Completed: { bg: '#E2E0F0', color: '#47456C' },
  Cancelled: { bg: '#ECDEDE', color: '#8A4747' },
  NoShow: { bg: '#E8E2E8', color: '#6A476A' },
};
