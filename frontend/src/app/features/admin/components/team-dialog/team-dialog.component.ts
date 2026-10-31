import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ApiError } from '../../../../core/models/api-error.model';
import { UserListItem } from '../../models/user-list-item.model';

export interface TeamDialogData {
  /** The current name when renaming; absent when creating. */
  name?: string;
  /** Supervisors free to lead a team; only asked for when creating. */
  supervisors?: UserListItem[];
  /** Saves the team. A rejection keeps the dialog open and lists any 400 field errors. */
  save: (name: string, supervisorId: string) => Promise<void>;
}

/** Creates a team, or renames one. Closes with true once saved. */
@Component({
  selector: 'app-team-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    ReactiveFormsModule,
  ],
  templateUrl: './team-dialog.component.html',
  styleUrl: './team-dialog.component.scss',
})
export class TeamDialogComponent {
  protected readonly data = inject<TeamDialogData>(MAT_DIALOG_DATA);
  private readonly dialog = inject(MatDialogRef);

  protected readonly renaming = this.data.name !== undefined;
  protected readonly saving = signal(false);
  protected readonly serverErrors = signal<string[]>([]);

  protected readonly form = new FormGroup({
    name: new FormControl(this.data.name ?? '', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(100)],
    }),
    supervisorId: new FormControl(
      { value: '', disabled: this.renaming },
      { nonNullable: true, validators: Validators.required },
    ),
  });

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, supervisorId } = this.form.getRawValue();
    this.saving.set(true);
    this.serverErrors.set([]);
    try {
      await this.data.save(name.trim(), supervisorId);
      this.dialog.close(true);
    } catch (error) {
      this.serverErrors.set(Object.values((error as ApiError).fieldErrors ?? {}).flat());
    } finally {
      this.saving.set(false);
    }
  }
}
