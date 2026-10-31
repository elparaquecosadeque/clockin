import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ApiError } from '../../../../core/models/api-error.model';
import { addDays, instantAt, wallClock } from '../../../../shared/utils/week';
import { TimesheetEntry } from '../../models/timesheet-entry.model';

export const maxNoteLength = 500;

export interface EntryDialogResult {
  start: string;
  end: string | null;
  note: string;
}

export interface EntryDialogData {
  weekStart: string;
  timeZone: string;
  /** The entry to edit; absent when adding. */
  entry?: TimesheetEntry;
  /** Saves the entry. A rejection keeps the dialog open and lists any 400 field errors. */
  save: (result: EntryDialogResult) => Promise<void>;
}

/**
 * Adds or edits an entry in the company's time zone. An end before the start means the next day, so overnight
 * entries need no second date. Leaving the end empty keeps an open entry open.
 */
@Component({
  selector: 'app-entry-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    ReactiveFormsModule,
  ],
  templateUrl: './entry-dialog.component.html',
  styleUrl: './entry-dialog.component.scss',
})
export class EntryDialogComponent {
  protected readonly data = inject<EntryDialogData>(MAT_DIALOG_DATA);
  private readonly dialog = inject(MatDialogRef);

  protected readonly isOpenEntry = !!this.data.entry && this.data.entry.end === null;
  protected readonly firstDay = this.data.weekStart;
  protected readonly lastDay = addDays(this.data.weekStart, 6);
  protected readonly saving = signal(false);

  private readonly start = this.data.entry
    ? wallClock(this.data.entry.start, this.data.timeZone)
    : null;

  protected readonly form = new FormGroup({
    date: new FormControl(this.start?.date ?? '', {
      nonNullable: true,
      validators: Validators.required,
    }),
    start: new FormControl(this.start?.time ?? '', {
      nonNullable: true,
      validators: Validators.required,
    }),
    end: new FormControl(
      this.data.entry?.end ? wallClock(this.data.entry.end, this.data.timeZone).time : '',
      { nonNullable: true, validators: this.isOpenEntry ? [] : Validators.required },
    ),
    note: new FormControl('', {
      nonNullable: true,
      validators: [
        Validators.required,
        Validators.pattern(/\S/),
        Validators.maxLength(maxNoteLength),
      ],
    }),
  });

  protected readonly serverErrors = signal<string[]>([]);

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { date, start, end, note } = this.form.getRawValue();
    const startAt = instantAt(date, start, this.data.timeZone);
    const endAt = end
      ? instantAt(end > start ? date : addDays(date, 1), end, this.data.timeZone)
      : null;
    this.saving.set(true);
    this.serverErrors.set([]);
    try {
      await this.data.save({ start: startAt, end: endAt, note: note.trim() });
      this.dialog.close(true);
    } catch (error) {
      // Rule breaks and conflicts were shown by the error interceptor; validation errors are listed here.
      this.serverErrors.set(Object.values((error as ApiError).fieldErrors ?? {}).flat());
    } finally {
      this.saving.set(false);
    }
  }
}
