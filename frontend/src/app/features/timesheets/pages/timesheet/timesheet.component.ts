import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ToDosService } from '../../../../core/services/to-dos.service';
import {
  TextDialogComponent,
  TextDialogData,
} from '../../../../shared/components/text-dialog/text-dialog.component';
import { CompanyTimePipe } from '../../../../shared/pipes/company-time.pipe';
import { DurationPipe } from '../../../../shared/pipes/duration.pipe';
import { addDays, mondayOf, today } from '../../../../shared/utils/week';
import { HistoryComponent } from '../../components/history/history.component';
import {
  EntryDialogComponent,
  EntryDialogData,
  EntryDialogResult,
  maxNoteLength,
} from '../../components/entry-dialog/entry-dialog.component';
import { StatusLabelPipe } from '../../../../shared/pipes/status-label.pipe';
import { Timesheet } from '../../models/timesheet.model';
import { TimesheetEntry } from '../../models/timesheet-entry.model';
import { TimesheetService } from '../../services/timesheet.service';

const maxCommentLength = 1000;

/**
 * One person's week (view-timesheet.feature) with every action the server says the viewer can take now.
 * Without `ownerId` it is the signed-in user's own week; without `weekStart`, the current week.
 */
@Component({
  selector: 'app-timesheet',
  imports: [
    CompanyTimePipe,
    DurationPipe,
    HistoryComponent,
    MatButtonModule,
    MatExpansionModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    RouterLink,
    StatusLabelPipe,
  ],
  templateUrl: './timesheet.component.html',
  styleUrl: './timesheet.component.scss',
})
export class TimesheetComponent {
  private readonly timesheets = inject(TimesheetService);
  private readonly toDos = inject(ToDosService);
  private readonly dialogs = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);

  readonly ownerId = input<string>();
  readonly weekStart = input<string>();

  protected readonly week = computed(() => mondayOf(this.weekStart() ?? today()));
  protected readonly timesheet = signal<Timesheet | null>(null);
  protected readonly loadFailed = signal(false);
  protected readonly busy = signal(false);

  /** Entries grouped under the seven days, Monday first. */
  protected readonly days = computed(() => {
    const timesheet = this.timesheet();
    return (timesheet?.days ?? []).map((day) => ({
      ...day,
      entries: timesheet!.entries.filter((entry) => entry.date === day.date),
    }));
  });

  protected readonly base = computed(() => {
    const owner = this.ownerId();
    return owner ? ['/review', owner] : ['/timesheets'];
  });

  constructor() {
    effect(() => void this.load(this.ownerId() ?? null, this.week()));
  }

  protected goToWeek(offset: number): void {
    void this.router.navigate([...this.base(), addDays(this.week(), offset * 7)]);
  }

  protected async reload(): Promise<void> {
    await this.load(this.ownerId() ?? null, this.week());
  }

  protected addEntry(): void {
    const timesheet = this.timesheet()!;
    this.openEntryDialog(timesheet, undefined, (result) =>
      this.timesheets.addEntry(this.ownerId() ?? null, result.start, result.end!, result.note),
    );
  }

  protected editEntry(entry: TimesheetEntry): void {
    const timesheet = this.timesheet()!;
    this.openEntryDialog(timesheet, entry, (result) =>
      this.timesheets.editEntry(entry.id, result.start, result.end, result.note, timesheet.version),
    );
  }

  protected async deleteEntry(entry: TimesheetEntry): Promise<void> {
    const note = await this.ask({
      title: $localize`:@@deleteEntryTitle:Delete this entry?`,
      message: $localize`:@@deleteEntryMessage:It stays in the history with its last times.`,
      label: $localize`:@@deleteEntryNote:Why is it deleted?`,
      confirm: $localize`:@@delete:Delete`,
      required: true,
      maxLength: maxNoteLength,
    });
    if (note !== undefined) {
      await this.act(
        () => this.timesheets.deleteEntry(entry.id, note, this.timesheet()!.version),
        $localize`:@@entryDeleted:Entry deleted.`,
      );
    }
  }

  protected async submit(): Promise<void> {
    await this.act(
      () => this.timesheets.submit(this.ownerId() ?? null, this.week()),
      $localize`:@@timesheetSubmitted:Timesheet submitted for approval.`,
    );
  }

  protected async approve(): Promise<void> {
    await this.act(
      () => this.timesheets.approve(this.timesheet()!.id!),
      $localize`:@@timesheetApproved:Timesheet approved.`,
    );
  }

  protected async returnTimesheet(): Promise<void> {
    const comment = await this.ask({
      title: $localize`:@@returnTitle:Return for changes`,
      message: $localize`:@@returnMessage:The timesheet goes back to Draft so its owner can fix it.`,
      label: $localize`:@@returnComment:What needs to change?`,
      confirm: $localize`:@@return:Return`,
      required: true,
      maxLength: maxCommentLength,
    });
    if (comment !== undefined) {
      await this.act(
        () => this.timesheets.return(this.timesheet()!.id!, comment),
        $localize`:@@timesheetReturned:Timesheet returned.`,
      );
    }
  }

  protected async reopen(): Promise<void> {
    const reason = await this.ask({
      title: $localize`:@@reopenTitle:Reopen this approved timesheet?`,
      message: $localize`:@@reopenMessage:It goes back to Draft and needs approval again.`,
      label: $localize`:@@reopenReason:Reason`,
      confirm: $localize`:@@reopen:Reopen`,
      required: true,
      maxLength: maxCommentLength,
    });
    if (reason !== undefined) {
      await this.act(
        () => this.timesheets.reopen(this.timesheet()!.id!, reason),
        $localize`:@@timesheetReopened:Timesheet reopened.`,
      );
    }
  }

  protected async requestCorrection(): Promise<void> {
    const reason = await this.ask({
      title: $localize`:@@requestCorrectionTitle:Request a correction`,
      message: $localize`:@@requestCorrectionMessage:Your approver decides. If they accept, the week goes back to Draft for you to fix.`,
      label: $localize`:@@requestCorrectionReason:What is wrong?`,
      confirm: $localize`:@@send:Send`,
      required: true,
      maxLength: maxCommentLength,
    });
    if (reason !== undefined) {
      await this.act(
        () => this.timesheets.requestCorrection(this.timesheet()!.id!, reason),
        $localize`:@@correctionRequested:Correction requested.`,
      );
    }
  }

  protected async acceptCorrection(): Promise<void> {
    const comment = await this.ask({
      title: $localize`:@@acceptCorrectionTitle:Accept the correction?`,
      message: $localize`:@@acceptCorrectionMessage:The timesheet goes back to Draft so its owner can fix it.`,
      label: $localize`:@@optionalComment:Comment (optional)`,
      confirm: $localize`:@@accept:Accept`,
      required: false,
      maxLength: maxCommentLength,
    });
    if (comment !== undefined) {
      await this.act(
        () =>
          this.timesheets.acceptCorrection(this.timesheet()!.latestCorrection!.id, comment || null),
        $localize`:@@correctionAccepted:Correction accepted.`,
      );
    }
  }

  protected async declineCorrection(): Promise<void> {
    const comment = await this.ask({
      title: $localize`:@@declineCorrectionTitle:Decline the correction?`,
      label: $localize`:@@declineCorrectionComment:Why is it declined?`,
      confirm: $localize`:@@decline:Decline`,
      required: true,
      maxLength: maxCommentLength,
    });
    if (comment !== undefined) {
      await this.act(
        () => this.timesheets.declineCorrection(this.timesheet()!.latestCorrection!.id, comment),
        $localize`:@@correctionDeclined:Correction declined.`,
      );
    }
  }

  protected print(): void {
    window.print();
  }

  private async load(ownerId: string | null, week: string): Promise<void> {
    this.loadFailed.set(false);
    try {
      this.timesheet.set(await this.timesheets.get(ownerId, week));
    } catch {
      this.timesheet.set(null);
      this.loadFailed.set(true);
    }
  }

  private openEntryDialog(
    timesheet: Timesheet,
    entry: TimesheetEntry | undefined,
    save: (result: EntryDialogResult) => Promise<void>,
  ): void {
    const data: EntryDialogData = {
      weekStart: timesheet.weekStart,
      timeZone: timesheet.timeZone,
      entry,
      save,
    };
    this.dialogs
      .open(EntryDialogComponent, { data, width: '480px' })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) void this.afterChange($localize`:@@entrySaved:Entry saved.`);
      });
  }

  private async ask(data: TextDialogData): Promise<string | undefined> {
    const dialog = this.dialogs.open<TextDialogComponent, TextDialogData, string>(
      TextDialogComponent,
      {
        data,
        width: '480px',
      },
    );
    return firstValueFrom(dialog.afterClosed());
  }

  /** Runs an action; the error interceptor shows failures, and the page reloads either way. */
  private async act(action: () => Promise<void>, done: string): Promise<void> {
    this.busy.set(true);
    try {
      await action();
      await this.afterChange(done);
    } catch {
      await this.reload();
    } finally {
      this.busy.set(false);
    }
  }

  private async afterChange(done: string): Promise<void> {
    this.snackBar.open(done, undefined, { duration: 4000 });
    await this.reload();
    void this.toDos.load();
  }
}
