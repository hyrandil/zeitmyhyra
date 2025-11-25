import { describe, expect, it } from 'vitest';
import { BookingType } from '@prisma/client';
import { calculateDailySummary } from './timeCalculation';

describe('calculateDailySummary', () => {
  it('calculates worked minutes and delta', () => {
    const entries = [
      { timestampUtc: new Date('2024-05-01T08:00:00Z'), type: BookingType.KOMMEN },
      { timestampUtc: new Date('2024-05-01T12:00:00Z'), type: BookingType.PAUSE_START },
      { timestampUtc: new Date('2024-05-01T12:30:00Z'), type: BookingType.PAUSE_ENDE },
      { timestampUtc: new Date('2024-05-01T17:00:00Z'), type: BookingType.GEHEN }
    ];

    const result = calculateDailySummary(entries, 480);
    expect(result[0].workedMinutes).toBe(450);
    expect(result[0].readable).toContain('7h');
  });
});
