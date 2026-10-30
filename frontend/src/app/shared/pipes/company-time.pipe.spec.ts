import { LOCALE_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CompanyTimePipe } from './company-time.pipe';

describe('CompanyTimePipe', () => {
  function pipe(locale: string): CompanyTimePipe {
    TestBed.configureTestingModule({ providers: [{ provide: LOCALE_ID, useValue: locale }] });
    return TestBed.runInInjectionContext(() => new CompanyTimePipe());
  }

  it('shows the time in the company time zone, not the browser one', () => {
    expect(pipe('en-US').transform('2026-10-30T13:00:00Z', 'America/Bogota')).toBe('08:00');
  });

  it('shows the day in the build language', () => {
    expect(pipe('es').transform('2026-10-30T13:00:00Z', 'America/Bogota', 'day')).toBe(
      'viernes, 30 de octubre',
    );
  });
});
