import { LOCALE_ID, Pipe, PipeTransform, inject } from '@angular/core';

const formats = {
  time: { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' },
  day: { weekday: 'long', day: 'numeric', month: 'long' },
} satisfies Record<string, Intl.DateTimeFormatOptions>;

/**
 * An instant in the company's time zone, which the API sends with each response.
 * ponytail: 24h only; the user's 12h/24h preference comes with update-preferences.feature.
 */
@Pipe({ name: 'companyTime' })
export class CompanyTimePipe implements PipeTransform {
  private readonly locale = inject(LOCALE_ID);

  transform(
    instant: string | number,
    timeZone: string,
    format: keyof typeof formats = 'time',
  ): string {
    return new Intl.DateTimeFormat(this.locale, { ...formats[format], timeZone }).format(
      new Date(instant),
    );
  }
}
