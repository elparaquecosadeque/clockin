import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SILENT_ERRORS } from '../../../core/models/silent-errors.model';
import { DemoAccounts } from '../models/demo-accounts.model';

/** Account actions that don't need a session. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  /** Throws the ApiError; the page shows it inline. */
  async setPassword(token: string, password: string): Promise<void> {
    const context = new HttpContext().set(SILENT_ERRORS, ['*']);
    await firstValueFrom(
      this.http.post<void>('/api/auth/set-password', { token, password }, { context }),
    );
  }

  /** The demo accounts, or null outside a demo (the API answers 404). */
  async demoAccounts(): Promise<DemoAccounts | null> {
    const context = new HttpContext().set(SILENT_ERRORS, ['*']);
    try {
      return await firstValueFrom(this.http.get<DemoAccounts>('/api/demo/accounts', { context }));
    } catch {
      return null;
    }
  }
}
