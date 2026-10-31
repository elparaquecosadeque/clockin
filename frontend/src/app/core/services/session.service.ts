import { HttpClient, HttpContext } from '@angular/common/http';
import { DOCUMENT, Injectable, LOCALE_ID, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AccessToken } from '../models/access-token.model';
import { SessionUser } from '../models/session-user.model';
import { SILENT_ERRORS } from '../models/silent-errors.model';
import { TimeFormat } from '../../shared/models/time-format.model';

/**
 * The signed-in user and their access token, in memory only (CODE_STYLE.md, HTTP, session and errors).
 * The refresh token is an HttpOnly cookie this code never sees.
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  private readonly locale = inject(LOCALE_ID);
  private readonly location = inject(DOCUMENT).location;
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

  /** Saves the preferences and reloads, into the other language's build if it changed (ADR 0001). */
  async updatePreferences(
    language: SessionUser['language'],
    timeFormat: TimeFormat,
  ): Promise<void> {
    await firstValueFrom(this.http.put<void>('/api/me/preferences', { language, timeFormat }));
    this.location.assign(
      localizedPath(this.location.pathname, this.locale, language) ?? this.location.href,
    );
  }

  /** Every other session ends; this one continues with the new token. Throws the ApiError. */
  async changePassword(currentPassword: string, newPassword: string): Promise<void> {
    const context = new HttpContext().set(SILENT_ERRORS, ['InvalidCurrentPassword']);
    const response = await firstValueFrom(
      this.http.post<AccessToken>(
        '/api/me/password',
        { currentPassword, newPassword },
        { context },
      ),
    );
    this.token.set(response.accessToken);
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
    const user = await firstValueFrom(this.http.get<SessionUser>('/api/me'));
    const other = localizedPath(this.location.pathname, this.locale, user.language);
    if (other) {
      // The refresh cookie survives the reload, so the other build restores this session.
      this.location.assign(other);
    }

    this.currentUser.set(user);
  }

  private clear(): void {
    this.token.set(null);
    this.currentUser.set(null);
  }
}

/**
 * The same page in the build for `language`, or null when this build already matches. Only builds served under
 * a locale path (`/en/`, `/es/`) can switch; a dev server serves one locale at the root.
 */
export function localizedPath(pathname: string, locale: string, language: string): string | null {
  const current = locale.slice(0, 2);
  const prefix = `/${current}/`;
  return current === language || !pathname.startsWith(prefix)
    ? null
    : `/${language}/${pathname.slice(prefix.length)}`;
}

function silent(): HttpContext {
  return new HttpContext().set(SILENT_ERRORS, ['*']);
}
