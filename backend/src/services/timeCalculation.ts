import { BookingType, TimeEntry } from '@prisma/client';

type EntryLike = Pick<TimeEntry, 'timestampUtc' | 'type'>;

export interface DailySummary {
  date: string;
  workedMinutes: number;
  readable: string;
}

/**
 * Calculates worked minutes per day from ordered time entries.
 */
export const calculateDailySummary = (entries: EntryLike[], expectedMinutes = 480): DailySummary[] => {
  const sorted = [...entries].sort((a, b) => new Date(a.timestampUtc).getTime() - new Date(b.timestampUtc).getTime());
  const perDay: Record<string, number> = {};
  let lastIn: Date | null = null;
  let pauseStarted: Date | null = null;

  for (const entry of sorted) {
    const dateKey = new Date(entry.timestampUtc).toISOString().split('T')[0];
    perDay[dateKey] = perDay[dateKey] || 0;

    if (entry.type === BookingType.KOMMEN) {
      lastIn = new Date(entry.timestampUtc);
    }
    if (entry.type === BookingType.PAUSE_START) {
      pauseStarted = new Date(entry.timestampUtc);
    }
    if (entry.type === BookingType.PAUSE_ENDE && pauseStarted) {
      const diff = (new Date(entry.timestampUtc).getTime() - pauseStarted.getTime()) / 60000;
      perDay[dateKey] -= diff; // subtract break from worked time
      pauseStarted = null;
    }
    if (entry.type === BookingType.GEHEN && lastIn) {
      const diff = (new Date(entry.timestampUtc).getTime() - lastIn.getTime()) / 60000;
      perDay[dateKey] += diff;
      lastIn = null;
    }
  }

  return Object.entries(perDay).map(([date, minutes]) => ({
    date,
    workedMinutes: Math.max(0, Math.round(minutes)),
    readable: `${Math.floor(Math.max(0, minutes) / 60)}h ${Math.round(Math.max(0, minutes) % 60)}m (${Math.round(
      (Math.max(0, minutes) - expectedMinutes) / 60
    )}h Δ)`
  }));
};
