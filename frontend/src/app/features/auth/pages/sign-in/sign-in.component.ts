import { Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { ApiError } from '../../../../core/models/api-error.model';
import { messageFor } from '../../../../core/interceptors/error.interceptor';
import { SessionService } from '../../../../core/services/session.service';
import { DemoAccountsComponent } from '../../components/demo-accounts/demo-accounts.component';
import { DemoAccount, DemoAccounts } from '../../models/demo-accounts.model';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-sign-in',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    DemoAccountsComponent,
  ],
  templateUrl: './sign-in.component.html',
  styleUrl: './sign-in.component.scss',
})
export class SignInComponent {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);

  /** Set by the set-password page after success (`?passwordSet=1`). */
  readonly passwordSet = input<string>();

  /** Only in a demo deployment (ADR 0005). */
  protected readonly demo = signal<DemoAccounts | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  constructor() {
    void inject(AuthService)
      .demoAccounts()
      .then((demo) => this.demo.set(demo));
  }

  protected fill(account: DemoAccount, password: string): void {
    this.form.setValue({ email: account.email, password });
    this.error.set(null);
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      const { email, password } = this.form.getRawValue();
      await this.session.signIn(email, password);
      await this.router.navigateByUrl('/');
    } catch (error) {
      const apiError = error as ApiError;
      this.error.set(
        apiError.code === 'InvalidCredentials'
          ? $localize`:@@invalidCredentials:The email or password is incorrect.`
          : messageFor(apiError),
      );
    } finally {
      this.busy.set(false);
    }
  }
}
