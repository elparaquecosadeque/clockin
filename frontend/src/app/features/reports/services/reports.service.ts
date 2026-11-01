import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ClockedIn } from '../models/clocked-in.model';
import { WeekException } from '../models/week-exception.model';
import { TeamWeek } from '../models/team-week.model';
import { ReportPage } from '../../../shared/models/report-page.model';

/** Reports for supervisors (their team) and admins (everyone); the server scopes them. */
@Injectable({ providedIn: 'root' })
export class ReportsService {
  private readonly http = inject(HttpClient);

  teamWeek(weekStart: string): Promise<TeamWeek> {
    return firstValueFrom(this.http.get<TeamWeek>(`/api/reports/team-week/${weekStart}`));
  }

  clockedIn(): Promise<ClockedIn[]> {
    return firstValueFrom(this.http.get<ClockedIn[]>('/api/reports/clocked-in'));
  }

  exceptions(page = 1): Promise<ReportPage<WeekException>> {
    return firstValueFrom(
      this.http.get<ReportPage<WeekException>>('/api/reports/exceptions', { params: { page } }),
    );
  }

  /** The approved timesheets of a week as a CSV file, with the server's file name. */
  async approvedCsv(
    weekStart: string,
    teamId: string | null,
  ): Promise<{ name: string; content: Blob }> {
    const params = teamId ? new HttpParams().set('teamId', teamId) : undefined;
    const response: HttpResponse<Blob> = await firstValueFrom(
      this.http.get(`/api/reports/approved-csv/${weekStart}`, {
        params,
        observe: 'response',
        responseType: 'blob',
      }),
    );
    const disposition = response.headers.get('Content-Disposition') ?? '';
    const name = /filename="?([^";]+)"?/.exec(disposition)?.[1] ?? `approved-${weekStart}.csv`;
    return { name, content: response.body! };
  }
}
