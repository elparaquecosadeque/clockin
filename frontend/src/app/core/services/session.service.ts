import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AccessToken } from '../models/access-token.model';
import { SessionUser } from '../models/session-user.model';
import { SILENT_ERRORS } from '../models/silent-errors.model';

/**
 * The signed-in user and their access token, in memory only (CODE_STYLE.md, HTTP, session and errors).
 * The refresh token is an HttpOnly cookie this code never sees.
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  private readonly token = signal<string | null>(null);
  private readonly currentUser = signal<SessionUser | null>(null);
  private pendingRefresh: Promise<boolean> | null = null;

  readonly accessToken = this.token.asReadonly();
  readonly user = this.currentUser.asReadonly();
  readonly isSignedIn = computed(() => this.currentUser() !== null);

  hasPermission(permission: string): boolean {
    return this.currentUser()?.permissions.includes(permission) ?? false;
  }

  /** Throws the ApiError on failure; the caller shows it. */
  async signIn(email: string, password: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<AccessToken>('/api/auth/login', { email, password }, { context: silent() }),
    );
    this.token.set(response.accessToken);
    await this.loadUser();
  }

  /** Swaps the refresh cookie for a new access token. Concurrent callers share one request. */
  refresh(): Promise<boolean> {
    this.pendingRefresh ??= this.requestRefresh().finally(() => (this.pendingRefresh = null));
    return this.pendingRefresh;
  }

  /** On app start: a reload keeps the user signed in while the refresh cookie is valid. */
  async restore(): Promise<void> {
    if (await this.refresh()) {
      await this.loadUser().catch(() => this.clear());
    }
  }

  async signOut(): Promise<void> {
    try {
      await firstValueFrom(this.http.post<void>('/api/auth/logout', null, { context: silent() }));
    } catch {
      // Signing out locally matters more than telling the server.
    } finally {
      this.clear();
    }
  }

  private async requestRefresh(): Promise<boolean> {
    try {
      const response = await firstValueFrom(
        this.http.post<AccessToken>('/api/auth/refresh', null, { context: silent() }),
      );
      this.token.set(response.accessToken);
      return true;
    } catch {
      this.clear();
      return false;
    }
  }

  private async loadUser(): Promise<void> {
    this.currentUser.set(await firstValueFrom(this.http.get<SessionUser>('/api/me')));
  }

  private clear(): void {
    this.token.set(null);
    this.currentUser.set(null);
  }
}

function silent(): HttpContext {
  return new HttpContext().set(SILENT_ERRORS, ['*']);
}
