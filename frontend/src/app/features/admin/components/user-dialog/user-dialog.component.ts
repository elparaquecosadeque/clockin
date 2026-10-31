import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ApiError } from '../../../../core/models/api-error.model';
import { AccessLink } from '../../models/access-link.model';
import { NewUser } from '../../models/new-user.model';
import { TeamListItem } from '../../models/team-list-item.model';
import { Role } from '../../models/user-list-item.model';

export interface UserDialogData {
  /** Active teams an employee can join. */
  teams: TeamListItem[];
  /** Creates the user. A rejection keeps the dialog open and lists any 400 field errors. */
  save: (user: NewUser) => Promise<AccessLink>;
}

/** Creates a user (create-user.feature). Closes with the activation link. */
@Component({
  selector: 'app-user-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    ReactiveFormsModule,
  ],
  templateUrl: './user-dialog.component.html',
  styleUrl: './user-dialog.component.scss',
})
export class UserDialogComponent {
  protected readonly data = inject<UserDialogData>(MAT_DIALOG_DATA);
  private readonly dialog = inject<MatDialogRef<UserDialogComponent, AccessLink>>(MatDialogRef);

  protected readonly saving = signal(false);
  protected readonly serverErrors = signal<string[]>([]);

  protected readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
    role: new FormControl<Role>('Employee', { nonNullable: true }),
    teamId: new FormControl('', { nonNullable: true, validators: Validators.required }),
    language: new FormControl<'en' | 'es'>('en', { nonNullable: true }),
    timeFormat: new FormControl<'24h' | '12h'>('24h', { nonNullable: true }),
  });

  constructor() {
    // Only employees belong to a team.
    this.form.controls.role.valueChanges.subscribe((role) => {
      const team = this.form.controls.teamId;
      if (role === 'Employee') {
        team.enable();
      } else {
        team.reset();
        team.disable();
      }
    });
  }

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    this.serverErrors.set([]);
    try {
      const link = await this.data.save({
        ...value,
        name: value.name.trim(),
        email: value.email.trim(),
        teamId: value.role === 'Employee' ? value.teamId : null,
      });
      this.dialog.close(link);
    } catch (error) {
      this.serverErrors.set(Object.values((error as ApiError).fieldErrors ?? {}).flat());
    } finally {
      this.saving.set(false);
    }
  }
}
