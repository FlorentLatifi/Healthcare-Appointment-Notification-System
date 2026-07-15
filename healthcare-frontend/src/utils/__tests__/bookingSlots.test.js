import { describe, it, expect } from 'vitest';
import {
  buildFreeSlots,
  dayOfWeekFromDateStr,
  isSlotBooked,
  parseHmToMinutes,
  minutesToHm,
  findDaySchedule,
} from '../bookingSlots';

const monFri = [1, 2, 3, 4, 5].map((dayOfWeek) => ({
  dayOfWeek,
  isWorkingDay: true,
  startTime: '08:00',
  endTime: '18:00',
}));
const weekend = [
  { dayOfWeek: 0, isWorkingDay: false, startTime: null, endTime: null },
  { dayOfWeek: 6, isWorkingDay: false, startTime: null, endTime: null },
];
const weekly = [...weekend, ...monFri];

describe('bookingSlots helpers', () => {
  it('parses and formats HH:mm', () => {
    expect(parseHmToMinutes('09:30')).toBe(9 * 60 + 30);
    expect(minutesToHm(9 * 60 + 30)).toBe('09:30');
  });

  it('resolves day of week from date string', () => {
    // 2099-06-15 is a Monday
    expect(dayOfWeekFromDateStr('2099-06-15')).toBe(1);
  });

  it('finds day schedule by dayOfWeek number', () => {
    const day = findDaySchedule(weekly, 1);
    expect(day?.startTime).toBe('08:00');
    expect(findDaySchedule(weekly, 0)?.isWorkingDay).toBe(false);
  });

  it('detects booked slots from UTC starts', () => {
    // Construct a local 10:00 on 2099-06-15 and express as ISO for bookedSlots
    const local = new Date('2099-06-15T10:00:00');
    const booked = [{ startUtc: local.toISOString() }];
    expect(isSlotBooked('2099-06-15', '10:00', booked)).toBe(true);
    expect(isSlotBooked('2099-06-15', '10:30', booked)).toBe(false);
  });

  it('builds free slots excluding weekends, past, and booked', () => {
    const now = new Date('2099-06-15T07:00:00'); // Monday morning, before hours
    const localBooked = new Date('2099-06-15T10:00:00');
    const free = buildFreeSlots({
      dateStr: '2099-06-15',
      weeklySchedule: weekly,
      bookedSlots: [{ startUtc: localBooked.toISOString() }],
      now,
    });

    expect(free.length).toBeGreaterThan(0);
    // 08:00 is exactly +1h from 07:00 → excluded by ≥1h advance rule
    expect(free[0].time).toBe('08:30');
    expect(free.some((s) => s.time === '10:00')).toBe(false);
    expect(free.some((s) => s.time === '10:30')).toBe(true);
    expect(free.every((s) => s.iso && s.time)).toBe(true);
  });

  it('returns empty list for non-working day', () => {
    // 2099-06-20 is Saturday
    const free = buildFreeSlots({
      dateStr: '2099-06-20',
      weeklySchedule: weekly,
      bookedSlots: [],
      now: new Date('2099-06-15T07:00:00'),
    });
    expect(free).toEqual([]);
  });

  it('filters slots that violate 1-hour advance notice', () => {
    // Mid-day Monday, only later slots remain
    const now = new Date('2099-06-15T12:00:00');
    const free = buildFreeSlots({
      dateStr: '2099-06-15',
      weeklySchedule: weekly,
      bookedSlots: [],
      now,
    });
    // 12:00 is not free (needs +1h → after 13:00)
    expect(free.some((s) => s.time === '12:00')).toBe(false);
    expect(free.some((s) => s.time === '12:30')).toBe(false);
    expect(free.some((s) => s.time === '13:00' || s.time === '13:30')).toBe(true);
  });
});
