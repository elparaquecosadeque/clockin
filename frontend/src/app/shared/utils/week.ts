/** Weeks run Monday to Sunday and are named by their Monday, as ISO dates (`2026-10-26`). */

/** The Monday of the week holding `date`, an ISO date. */
export function mondayOf(date: string): string {
  const day = new Date(`${date}T00:00:00Z`);
  day.setUTCDate(day.getUTCDate() - ((day.getUTCDay() + 6) % 7));
  return day.toISOString().slice(0, 10);
}

export function addDays(date: string, days: number): string {
  const day = new Date(`${date}T00:00:00Z`);
  day.setUTCDate(day.getUTCDate() + days);
  return day.toISOString().slice(0, 10);
}

/**
 * Today's ISO date in `timeZone`.
 * ponytail: callers without the company zone yet pass the browser's; off by a day only around midnight.
 */
export function today(timeZone?: string): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone }).format(new Date());
}

/** The wall-clock date (`yyyy-mm-dd`) and time (`HH:mm`) of an instant in `timeZone`. */
export function wallClock(instant: string, timeZone: string): { date: string; time: string } {
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat('en-CA', {
      timeZone,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      hourCycle: 'h23',
    })
      .formatToParts(new Date(instant))
      .map((part) => [part.type, part.value]),
  );
  return {
    date: `${parts['year']}-${parts['month']}-${parts['day']}`,
    time: `${parts['hour']}:${parts['minute']}`,
  };
}

/** The instant at a wall-clock date and time in `timeZone`, as an ISO string with `Z`. */
export function instantAt(date: string, time: string, timeZone: string): string {
  const asUtc = Date.parse(`${date}T${time}:00Z`);
  // The zone's offset at that moment: how far its wall clock is from UTC. A second pass settles DST edges.
  let instant = asUtc;
  for (let pass = 0; pass < 2; pass++) {
    const wall = wallClock(new Date(instant).toISOString(), timeZone);
    instant -= Date.parse(`${wall.date}T${wall.time}:00Z`) - asUtc;
  }

  return new Date(instant).toISOString();
}
