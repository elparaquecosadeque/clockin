import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { CompanyTimePipe } from '../../../../shared/pipes/company-time.pipe';
import { DurationPipe } from '../../../../shared/pipes/duration.pipe';
import { StatusLabelPipe } from '../../../../shared/pipes/status-label.pipe';
import { addDays, mondayOf, today } from '../../../../shared/utils/week';
import { ClockedIn } from '../../models/clocked-in.model';
import { TeamWeek } from '../../models/team-week.model';
import { WeekException } from '../../models/week-exception.model';
import { ReportsService } from '../../services/reports.service';

/**
 * The team week grid (team-week-grid.feature) with who is clocked in now, the exceptions to look at,
 * the approved CSV and a print view. Admins see every team and can narrow to one.
 */
@Component({
  selector: 'app-team-week',
  imports: [
    CompanyTimePipe,
    DurationPipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    RouterLink,
    StatusLabelPipe,
  ],
  templateUrl: './team-week.component.html',
  styleUrl: './team-week.component.scss',
})
export class TeamWeekComponent {
  private readonly reports = inject(ReportsService);
  private readonly router = inject(Router);

  readonly weekStart = input<string>();

  protected readonly week = computed(() => mondayOf(this.weekStart() ?? today()));
  protected readonly grid = signal<TeamWeek | null>(null);
  protected readonly clockedIn = signal<ClockedIn[]>([]);
  protected readonly exceptions = signal<WeekException[]>([]);
  protected readonly loadFailed = signal(false);
  protected readonly exporting = signal(false);
  /** The team id to show, or '' for all. Filtering happens here; the CSV export asks the server for the team. */
  protected readonly teamFilter = signal('');

  protected readonly dayDates = computed(() =>
    [0, 1, 2, 3, 4, 5, 6].map((d) => addDays(this.week(), d)),
  );
  protected readonly filterable = computed(() => (this.grid()?.groups.length ?? 0) > 1);
  protected readonly groups = computed(() => {
    const filter = this.teamFilter();
    return (this.grid()?.groups ?? []).filter((g) => !filter || g.teamId === filter);
  });

  constructor() {
    effect(() => void this.loadGrid(this.week()));
    void this.loadMonitoring();
  }

  protected goToWeek(offset: number): void {
    void this.router.navigate(['/team', addDays(this.week(), offset * 7)]);
  }

  protected async reload(): Promise<void> {
    await Promise.all([this.loadGrid(this.week()), this.loadMonitoring()]);
  }

  protected async exportCsv(): Promise<void> {
    this.exporting.set(true);
    try {
      const file = await this.reports.approvedCsv(this.week(), this.teamFilter() || null);
      const link = document.createElement('a');
      link.href = URL.createObjectURL(file.content);
      link.download = file.name;
      link.click();
      URL.revokeObjectURL(link.href);
    } catch {
      // The error interceptor already told the user.
    } finally {
      this.exporting.set(false);
    }
  }

  protected print(): void {
    window.print();
  }

  private async loadGrid(week: string): Promise<void> {
    this.loadFailed.set(false);
    try {
      this.grid.set(await this.reports.teamWeek(week));
    } catch {
      this.grid.set(null);
      this.loadFailed.set(true);
    }
  }

  private async loadMonitoring(): Promise<void> {
    try {
      const [clockedIn, exceptions] = await Promise.all([
        this.reports.clockedIn(),
        this.reports.exceptions(),
      ]);
      this.clockedIn.set(clockedIn);
      this.exceptions.set(exceptions);
    } catch {
      // The grid is the page; these panels stay empty if they fail.
    }
  }
}
