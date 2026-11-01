import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterLink } from '@angular/router';
import { CompanyTimePipe } from '../../../../shared/pipes/company-time.pipe';
import { DurationPipe } from '../../../../shared/pipes/duration.pipe';
import { ReviewQueue } from '../../models/review-queue.model';
import { TimesheetService } from '../../services/timesheet.service';

/** What the signed-in supervisor or admin has to decide, oldest first. */
@Component({
  selector: 'app-review-queue',
  imports: [
    CompanyTimePipe,
    DurationPipe,
    MatButtonModule,
    MatIconModule,
    MatListModule,
    MatProgressSpinnerModule,
    RouterLink,
  ],
  templateUrl: './review-queue.component.html',
  styleUrl: './review-queue.component.scss',
})
export class ReviewQueueComponent {
  private readonly timesheets = inject(TimesheetService);

  protected readonly queue = signal<ReviewQueue | null>(null);
  protected readonly loadFailed = signal(false);
  protected readonly busy = signal(false);
  protected readonly timesheetPage = signal(1);
  protected readonly correctionPage = signal(1);

  constructor() {
    void this.reload();
  }

  protected async reload(
    timesheetPage = this.timesheetPage(),
    correctionPage = this.correctionPage(),
  ): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.loadFailed.set(false);
    try {
      this.queue.set(await this.timesheets.reviewQueue(timesheetPage, correctionPage));
      this.timesheetPage.set(timesheetPage);
      this.correctionPage.set(correctionPage);
    } catch {
      this.loadFailed.set(true);
    } finally {
      this.busy.set(false);
    }
  }
}
