import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SILENT_ERRORS } from '../../../core/models/silent-errors.model';
import { ClockStatus } from '../models/clock-status.model';

/** The signed-in user's clock: whether they're clocked in, and today's and this week's time. */
@Injectable({ providedIn: 'root' })
export class ClockService {
  private readonly http = inject(HttpClient);
  private readonly current = signal<ClockStatus | null>(null);

  readonly status = this.current.asReadonly();

  async load(): Promise<void> {
    this.current.set(await firstValueFrom(this.http.get<ClockStatus>('/api/time-entries/clock')));
  }

  async clockIn(): Promise<void> {
    await firstValueFrom(this.http.post<void>('/api/time-entries/clock-in', null));
    await this.load();
  }

  /** Throws the ApiError; EntryTooLong is left to the page, which explains it. */
  async clockOut(): Promise<void> {
    const context = new HttpContext().set(SILENT_ERRORS, ['EntryTooLong']);
    await firstValueFrom(this.http.post<void>('/api/time-entries/clock-out', null, { context }));
    await this.load();
  }
}
