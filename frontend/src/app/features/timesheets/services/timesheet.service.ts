import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ReviewQueue } from '../models/review-queue.model';
import { Timesheet } from '../models/timesheet.model';

/** Weeks, their entries and their workflow. `ownerId` null means the signed-in user's own week. */
@Injectable({ providedIn: 'root' })
export class TimesheetService {
  private readonly http = inject(HttpClient);

  async get(ownerId: string | null, weekStart: string): Promise<Timesheet> {
    return firstValueFrom(this.http.get<Timesheet>(`${weekPath(ownerId)}/${weekStart}`));
  }

  async submit(ownerId: string | null, weekStart: string): Promise<void> {
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

  reviewQueue(timesheetPage = 1, correctionPage = 1): Promise<ReviewQueue> {
    return firstValueFrom(
      this.http.get<ReviewQueue>('/api/reports/review-queue', {
        params: { timesheetPage, correctionPage },
      }),
    );
  }

  private async post(url: string, body: unknown = null): Promise<void> {
    await firstValueFrom(this.http.post(url, body));
  }
}

const userId = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function weekPath(ownerId: string | null): string {
  if (!ownerId) {
    return '/api/timesheets/mine';
  }

  // The id comes from the page URL: anything but a user id, such as "..", could point the request elsewhere.
  if (!userId.test(ownerId)) {
    throw new Error(`Not a user id: ${ownerId}`);
  }

  return `/api/timesheets/users/${ownerId}`;
}
