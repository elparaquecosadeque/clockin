import { LOCALE_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TIME_FORMAT, TimeFormat } from '../models/time-format.model';
import { CompanyTimePipe } from './company-time.pipe';

describe('CompanyTimePipe', () => {
  function pipe(locale: string, timeFormat: TimeFormat = '24h'): CompanyTimePipe {
    TestBed.configureTestingModule({
      providers: [
        { provide: LOCALE_ID, useValue: locale },
        { provide: TIME_FORMAT, useValue: () => timeFormat },
      ],
    });
    return TestBed.runInInjectionContext(() => new CompanyTimePipe());
  }

  it('shows the time in the company time zone, not the browser one', () => {
    expect(pipe('en-US').transform('2026-10-30T13:00:00Z', 'America/Bogota')).toBe('08:00');
  });

  it('follows the 12-hour preference in the build language', () => {
    expect(pipe('en-US', '12h').transform('2026-10-30T13:00:00Z', 'America/Bogota')).toBe(
      '8:00 AM',
    );
    TestBed.resetTestingModule();
    expect(
      pipe('es', '12h').transform('2026-10-30T13:00:00Z', 'America/Bogota').replace(/\s/g, ' '),
    ).toBe('8:00 a. m.');
  });

  it('shows the day in the build language', () => {
    expect(pipe('es').transform('2026-10-30T13:00:00Z', 'America/Bogota', 'day')).toBe(
      'viernes, 30 de octubre',
    );
  });

  it('shows a short date with the time', () => {
    expect(pipe('en-US').transform('2026-10-30T13:00:00Z', 'America/Bogota', 'dateTime')).toBe(
      'Fri, Oct 30, 08:00',
    );
  });
});
