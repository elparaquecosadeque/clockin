import { LOCALE_ID, Pipe, PipeTransform, inject } from '@angular/core';
import { TIME_FORMAT } from '../models/time-format.model';

const times = {
  '24h': { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' },
  '12h': { hour: 'numeric', minute: '2-digit', hourCycle: 'h12' },
} satisfies Record<string, Intl.DateTimeFormatOptions>;

const dates = {
  day: { weekday: 'long', day: 'numeric', month: 'long' },
  date: { weekday: 'short', day: 'numeric', month: 'short' },
} satisfies Record<string, Intl.DateTimeFormatOptions>;

export type CompanyTimeFormat = 'time' | 'dateTime' | keyof typeof dates;

/**
 * An instant in the company's time zone, which the API sends with each response. Times follow the user's
 * 12h/24h preference; changing it reloads the app, so the pipe can stay pure.
 */
@Pipe({ name: 'companyTime' })
export class CompanyTimePipe implements PipeTransform {
  private readonly locale = inject(LOCALE_ID);
  private readonly timeFormat = inject(TIME_FORMAT);

  transform(
    instant: string | number,
    timeZone: string,
    format: CompanyTimeFormat = 'time',
  ): string {
    const time = times[this.timeFormat()];
    const options: Intl.DateTimeFormatOptions =
      format === 'time' ? time : format === 'dateTime' ? { ...dates.date, ...time } : dates[format];
    return new Intl.DateTimeFormat(this.locale, { ...options, timeZone }).format(new Date(instant));
  }
}
