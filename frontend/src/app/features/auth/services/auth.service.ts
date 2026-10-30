import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SILENT_ERRORS } from '../../../core/models/silent-errors.model';

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
}
