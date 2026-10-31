import { InjectionToken } from '@angular/core';

export type TimeFormat = '24h' | '12h';

/** The signed-in user's time format (ADR 0001). The app provides it from the session; 24h until then. */
export const TIME_FORMAT = new InjectionToken<() => TimeFormat>('TIME_FORMAT', {
  factory: () => () => '24h',
});
