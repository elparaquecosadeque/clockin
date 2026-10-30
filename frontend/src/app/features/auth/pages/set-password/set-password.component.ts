import { Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { messageFor } from '../../../../core/interceptors/error.interceptor';
import { ApiError } from '../../../../core/models/api-error.model';
import { AuthService } from '../../services/auth.service';

/** Activates an account or resets a password (docs/specs/accounts/set-password-with-link.feature). */
@Component({
  selector: 'app-set-password',
  imports: [ReactiveFormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatInputModule],
  templateUrl: './set-password.component.html',
  styleUrl: './set-password.component.scss',
})
export class SetPasswordComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** From the access link (`?token=...`). */
  readonly token = input<string>();

  protected readonly busy = signal(false);
  protected readonly linkInvalid = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    password: ['', [Validators.required, Validators.minLength(12), Validators.maxLength(128)]],
  });

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.setPassword(this.token() ?? '', this.form.getRawValue().password);
      await this.router.navigate(['/sign-in'], { queryParams: { passwordSet: 1 } });
    } catch (error) {
      this.show(error as ApiError);
    } finally {
      this.busy.set(false);
    }
  }

  private show(error: ApiError): void {
    if (error.code === 'InvalidOrExpiredLink') {
      this.linkInvalid.set(true);
    } else if (error.fieldErrors['password']) {
      this.form.controls.password.setErrors({ server: error.fieldErrors['password'][0] });
    } else {
      this.error.set(messageFor(error) ?? error.title);
    }
  }
}
