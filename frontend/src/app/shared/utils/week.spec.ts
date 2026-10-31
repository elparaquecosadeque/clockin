import { addDays, instantAt, mondayOf, wallClock } from './week';

describe('week', () => {
  it('finds the Monday of any day of the week', () => {
    expect(mondayOf('2026-10-26')).toBe('2026-10-26');
    expect(mondayOf('2026-11-01')).toBe('2026-10-26');
    expect(mondayOf('2026-10-30')).toBe('2026-10-26');
  });

  it('adds days across months', () => {
    expect(addDays('2026-10-26', 7)).toBe('2026-11-02');
    expect(addDays('2026-10-26', -7)).toBe('2026-10-19');
  });

  it('turns an instant into the company wall clock and back', () => {
    expect(wallClock('2026-10-30T13:00:00Z', 'America/Bogota')).toEqual({
      date: '2026-10-30',
      time: '08:00',
    });
    expect(instantAt('2026-10-30', '08:00', 'America/Bogota')).toBe('2026-10-30T13:00:00.000Z');
  });

  it('handles zones with daylight saving', () => {
    expect(instantAt('2026-07-01', '08:00', 'Europe/Madrid')).toBe('2026-07-01T06:00:00.000Z');
    expect(instantAt('2026-12-01', '08:00', 'Europe/Madrid')).toBe('2026-12-01T07:00:00.000Z');
  });
});
