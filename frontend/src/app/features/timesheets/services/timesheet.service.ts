import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ReviewQueue } from '../models/review-queue.model';
import { Timesheet } from '../models/timesheet.model';

/** Weeks, their entries and their workflow. `ownerId` null means the signed-in user's own week. */
@Injectable({ providedIn: 'root' })
export class TimesheetService {
  private readonly http = inject(HttpClient);

  get(ownerId: string | null, weekStart: string): Promise<Timesheet> {
    return firstValueFrom(this.http.get<Timesheet>(`${weekPath(ownerId)}/${weekStart}`));
  }

  submit(ownerId: string | null, weekStart: string): Promise<void> {
    return this.post(`${weekPath(ownerId)}/${weekStart}/submit`);
  }

  addEntry(ownerId: string | null, start: string, end: string, note: string): Promise<void> {
    return this.post('/api/time-entries', { ownerId, start, end, note });
  }

  editEntry(
    id: string,
    start: string,
    end: string | null,
    note: string,
    version: number,
  ): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`/api/time-entries/${id}`, { start, end, note, version }),
    );
  }

  deleteEntry(id: string, note: string, version: number): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`/api/time-entries/${id}`, { body: { note, version } }),
    );
  }

  approve(timesheetId: string): Promise<void> {
    return this.post(`/api/timesheets/${timesheetId}/approve`);
  }

  return(timesheetId: string, comment: string): Promise<void> {
    return this.post(`/api/timesheets/${timesheetId}/return`, { comment });
  }

  reopen(timesheetId: string, reason: string): Promise<void> {
    return this.post(`/api/timesheets/${timesheetId}/reopen`, { reason });
  }

  requestCorrection(timesheetId: string, reason: string): Promise<void> {
    return this.post(`/api/timesheets/${timesheetId}/correction-requests`, { reason });
  }

  acceptCorrection(requestId: string, comment: string | null): Promise<void> {
    return this.post(`/api/correction-requests/${requestId}/accept`, { comment });
  }

  declineCorrection(requestId: string, comment: string): Promise<void> {
    return this.post(`/api/correction-requests/${requestId}/decline`, { comment });
  }

  reviewQueue(): Promise<ReviewQueue> {
    return firstValueFrom(this.http.get<ReviewQueue>('/api/reports/review-queue'));
  }

  private async post(url: string, body: unknown = null): Promise<void> {
    await firstValueFrom(this.http.post(url, body));
  }
}

function weekPath(ownerId: string | null): string {
  return ownerId ? `/api/timesheets/users/${ownerId}` : '/api/timesheets/mine';
}
