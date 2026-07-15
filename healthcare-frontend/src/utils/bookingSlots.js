/**
 * Build free 30-minute appointment slots from doctor schedule + day availability.
 * Aligns with domain rules: working hours (local), :00/:30 intervals, ≥1h advance.
 */

const SLOT_MINUTES = 30;
const MIN_ADVANCE_MS = 60 * 60 * 1000;

/** @param {string} hhmm "HH:mm" */
export function parseHmToMinutes(hhmm) {
  if (!hhmm || typeof hhmm !== 'string') return null;
  const m = /^(\d{1,2}):(\d{2})$/.exec(hhmm.trim());
  if (!m) return null;
  const h = Number(m[1]);
  const min = Number(m[2]);
  if (!Number.isFinite(h) || !Number.isFinite(min) || h > 23 || min > 59) return null;
  return h * 60 + min;
}

export function minutesToHm(total) {
  const h = Math.floor(total / 60);
  const m = total % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
}

/** Local calendar date → YYYY-MM-DD */
export function toDateInputValue(date = new Date()) {
  const y = date.getFullYear();
  const mo = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${mo}-${d}`;
}

/**
 * .NET DayOfWeek and JS getDay() both use Sunday = 0.
 * @param {string} dateStr YYYY-MM-DD
 */
export function dayOfWeekFromDateStr(dateStr) {
  const d = new Date(`${dateStr}T12:00:00`);
  if (Number.isNaN(d.getTime())) return null;
  return d.getDay();
}

/**
 * @param {Array<{ dayOfWeek: number|string, isWorkingDay: boolean, startTime?: string, endTime?: string }>} weeklySchedule
 * @param {number} dayOfWeek
 */
export function findDaySchedule(weeklySchedule, dayOfWeek) {
  if (!Array.isArray(weeklySchedule)) return null;
  return weeklySchedule.find((row) => Number(row.dayOfWeek) === Number(dayOfWeek)) || null;
}

/**
 * Local date+time for a slot (browser local timezone).
 * @param {string} dateStr YYYY-MM-DD
 * @param {string} timeStr HH:mm
 */
export function slotLocalDateTime(dateStr, timeStr) {
  const d = new Date(`${dateStr}T${timeStr}:00`);
  return Number.isNaN(d.getTime()) ? null : d;
}

/**
 * Whether a booked slot (UTC start) lands on this local date+time.
 * @param {string} dateStr
 * @param {string} timeStr
 * @param {{ startUtc?: string, StartUtc?: string }[]} bookedSlots
 */
export function isSlotBooked(dateStr, timeStr, bookedSlots) {
  if (!Array.isArray(bookedSlots) || !bookedSlots.length) return false;
  const target = slotLocalDateTime(dateStr, timeStr);
  if (!target) return false;
  const tY = target.getFullYear();
  const tM = target.getMonth();
  const tD = target.getDate();
  const tH = target.getHours();
  const tMin = target.getMinutes();

  return bookedSlots.some((slot) => {
    const raw = slot.startUtc ?? slot.StartUtc;
    if (!raw) return false;
    const d = new Date(raw);
    if (Number.isNaN(d.getTime())) return false;
    return (
      d.getFullYear() === tY
      && d.getMonth() === tM
      && d.getDate() === tD
      && d.getHours() === tH
      && d.getMinutes() === tMin
    );
  });
}

/**
 * Generate free slots for a calendar day.
 * @returns {{ time: string, label: string, iso: string }[]}
 */
export function buildFreeSlots({
  dateStr,
  weeklySchedule,
  bookedSlots = [],
  now = new Date(),
  minAdvanceMs = MIN_ADVANCE_MS,
  slotMinutes = SLOT_MINUTES,
}) {
  if (!dateStr) return [];

  const dow = dayOfWeekFromDateStr(dateStr);
  if (dow == null) return [];

  const day = findDaySchedule(weeklySchedule, dow);
  if (!day || !day.isWorkingDay) return [];

  const startMin = parseHmToMinutes(day.startTime);
  const endMin = parseHmToMinutes(day.endTime);
  if (startMin == null || endMin == null || startMin >= endMin) return [];

  const free = [];
  const earliest = now.getTime() + minAdvanceMs;

  for (let m = startMin; m < endMin; m += slotMinutes) {
    // Align to :00 / :30
    if (m % slotMinutes !== 0) continue;
    const timeStr = minutesToHm(m);
    const local = slotLocalDateTime(dateStr, timeStr);
    if (!local) continue;
    if (local.getTime() <= earliest) continue;
    if (isSlotBooked(dateStr, timeStr, bookedSlots)) continue;

    free.push({
      time: timeStr,
      label: timeStr,
      /** ISO UTC for POST /Appointments scheduledTime */
      iso: local.toISOString(),
    });
  }

  return free;
}

/** Human summary of weekly hours for the doctor card. */
export function formatWeeklyHoursSummary(weeklySchedule) {
  if (!Array.isArray(weeklySchedule) || !weeklySchedule.length) return null;
  const names = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  const working = weeklySchedule
    .filter((d) => d.isWorkingDay && d.startTime && d.endTime)
    .sort((a, b) => Number(a.dayOfWeek) - Number(b.dayOfWeek));
  if (!working.length) return 'No working hours on file';
  // Collapse Mon–Fri same hours
  const first = working[0];
  const same = working.every(
    (d) => d.startTime === first.startTime && d.endTime === first.endTime,
  );
  if (same && working.length >= 5) {
    const days = working.map((d) => names[Number(d.dayOfWeek)]).join(', ');
    return `${days}: ${first.startTime}–${first.endTime}`;
  }
  return working
    .map((d) => `${names[Number(d.dayOfWeek)]} ${d.startTime}–${d.endTime}`)
    .join(' · ');
}
