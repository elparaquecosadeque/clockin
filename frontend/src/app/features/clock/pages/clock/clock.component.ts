import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiError } from '../../../../core/models/api-error.model';
import { CompanyTimePipe } from '../../../../shared/pipes/company-time.pipe';
import { DurationPipe } from '../../../../shared/pipes/duration.pipe';
import { ClockService } from '../../services/clock.service';

/** The main page for employees and supervisors (docs/specs/reporting/clock-screen.feature). */
@Component({
  selector: 'app-clock',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    CompanyTimePipe,
    DurationPipe,
  ],
  templateUrl: './clock.component.html',
  styleUrl: './clock.component.scss',
})
export class ClockComponent {
  private readonly clock = inject(ClockService);

  protected readonly status = this.clock.status;
  protected readonly now = signal(Date.now());
  protected readonly busy = signal(false);
  protected readonly loadFailed = signal(false);
  protected readonly entryTooLong = signal(false);

  protected readonly elapsedSeconds = computed(() => {
    const open = this.status()?.openEntry;
    return open ? (this.now() - Date.parse(open.start)) / 1000 : 0;
  });

  protected readonly todaySeconds = computed(() => {
    const status = this.status();
    if (!status) return 0;
    return status.closedTodaySeconds + (status.openEntryStartedToday ? this.elapsedSeconds() : 0);
  });

  protected readonly weekSeconds = computed(() => {
    const status = this.status();
    if (!status) return 0;
    return status.closedWeekSeconds + (status.openEntryStartedThisWeek ? this.elapsedSeconds() : 0);
  });

  constructor() {
    const timer = setInterval(() => this.now.set(Date.now()), 1000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.loadFailed.set(false);
    try {
      await this.clock.load();
    } catch {
      this.loadFailed.set(true);
    }
  }

  protected async punch(): Promise<void> {
    this.busy.set(true);
    this.entryTooLong.set(false);
    try {
      await (this.status()?.openEntry ? this.clock.clockOut() : this.clock.clockIn());
    } catch (error) {
      // Other errors were already shown by the error interceptor.
      this.entryTooLong.set((error as ApiError).code === 'EntryTooLong');
    } finally {
      this.busy.set(false);
    }
  }
}
