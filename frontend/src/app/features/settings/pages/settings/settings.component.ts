import { Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiError } from '../../../../core/models/api-error.model';
import { SessionUser } from '../../../../core/models/session-user.model';
import { SessionService } from '../../../../core/services/session.service';
import { TimeFormat } from '../../../../shared/models/time-format.model';

/** As set-password-with-link.feature: length only, no composition rules. */
const minPassword = 12;
const maxPassword = 128;

function matchesNewPassword(group: AbstractControl): ValidationErrors | null {
  const { newPassword, confirmation } = group.value as {
    newPassword: string;
    confirmation: string;
  };
  return confirmation && newPassword !== confirmation ? { mismatch: true } : null;
}

/** The signed-in user's preferences (update-preferences.feature) and password (change-own-password.feature). */
@Component({
  selector: 'app-settings',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    ReactiveFormsModule,
  ],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss',
})
export class SettingsComponent {
  private readonly session = inject(SessionService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly user = this.session.user()!;

  protected readonly savingPreferences = signal(false);
  protected readonly savingPassword = signal(false);

  protected readonly preferences = new FormGroup({
    language: new FormControl<SessionUser['language']>(this.user.language, { nonNullable: true }),
    timeFormat: new FormControl<TimeFormat>(this.user.timeFormat, { nonNullable: true }),
  });

  protected readonly password = new FormGroup(
    {
      currentPassword: new FormControl('', { nonNullable: true, validators: Validators.required }),
      newPassword: new FormControl('', {
        nonNullable: true,
        validators: [
          Validators.required,
          Validators.minLength(minPassword),
          Validators.maxLength(maxPassword),
        ],
      }),
      confirmation: new FormControl('', { nonNullable: true, validators: Validators.required }),
    },
    { validators: matchesNewPassword },
  );

  /** Saving reloads the app, in the other language's build if it changed. */
  protected async savePreferences(event: Event): Promise<void> {
    event.preventDefault();
    const { language, timeFormat } = this.preferences.getRawValue();
    this.savingPreferences.set(true);
    try {
      await this.session.updatePreferences(language, timeFormat);
    } catch {
      this.savingPreferences.set(false);
    }
  }

  protected async changePassword(event: Event): Promise<void> {
    event.preventDefault();
    if (this.password.invalid) {
      this.password.markAllAsTouched();
      return;
    }

    const { currentPassword, newPassword } = this.password.getRawValue();
    this.savingPassword.set(true);
    try {
      await this.session.changePassword(currentPassword, newPassword);
      this.password.reset();
      this.snackBar.open(
        $localize`:@@passwordChanged:Password changed. Your other devices are signed out.`,
        undefined,
        { duration: 5000 },
      );
    } catch (error) {
      if ((error as ApiError).code === 'InvalidCurrentPassword') {
        this.password.controls.currentPassword.setErrors({ wrong: true });
      }
    } finally {
      this.savingPassword.set(false);
    }
  }
}
