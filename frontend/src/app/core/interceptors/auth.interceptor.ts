import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { SessionService } from '../services/session.service';

/**
 * Attaches the access token. On a 401 it refreshes once and retries; if the refresh fails, the user is
 * sent to sign in (CODE_STYLE.md, HTTP, session and errors). The auth endpoints are left alone.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (request.url.startsWith('/api/auth/')) {
    return next(request);
  }

  const session = inject(SessionService);
  const router = inject(Router);
  return next(withToken(request, session.accessToken())).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || !session.accessToken()) {
        return throwError(() => error);
      }

      return from(session.refresh()).pipe(
        switchMap((refreshed) => {
          if (!refreshed) {
            void router.navigate(['/sign-in']);
            return throwError(() => error);
          }

          return next(withToken(request, session.accessToken()));
        }),
      );
    }),
  );
};

function withToken(request: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;
}
