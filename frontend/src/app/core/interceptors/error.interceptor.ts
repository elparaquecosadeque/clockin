import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, throwError } from 'rxjs';
import { ApiError } from '../models/api-error.model';
import { SILENT_ERRORS } from '../models/silent-errors.model';

const announced = new Set([403, 404, 409, 422]);

/**
 * Turns ProblemDetails into a typed ApiError. 403/404/409/422 show a snackbar with the server's message;
 * 429, 500 and network failures show a generic one. 400 and 401 are left to forms and the auth interceptor.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const snackBar = inject(MatSnackBar);
  return next(request).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }

      const apiError = toApiError(error);
      const silenced = request.context.get(SILENT_ERRORS);
      const message = messageFor(apiError);
      if (message && !silenced.includes('*') && !silenced.includes(apiError.code ?? '')) {
        snackBar.open(message, $localize`:@@dismiss:Dismiss`, { duration: 6000 });
      }

      return throwError(() => apiError);
    }),
  );
};

function toApiError(error: HttpErrorResponse): ApiError {
  const body: Record<string, unknown> =
    typeof error.error === 'object' && error.error !== null ? error.error : {};
  return {
    status: error.status,
    title: (body['title'] as string | undefined) ?? error.statusText,
    detail: body['detail'] as string | undefined,
    code: body['code'] as string | undefined,
    fieldErrors: (body['errors'] as Record<string, string[]> | undefined) ?? {},
  };
}

/** Exported for pages that show the same wording inline. */
export function messageFor(error: ApiError): string | null {
  if (error.status === 0) {
    return $localize`:@@networkError:Can't reach ClockIn. Check your connection and try again.`;
  }

  if (error.status >= 500) {
    return $localize`:@@unexpectedError:Something went wrong on our side. Try again in a moment.`;
  }

  if (error.status === 429) {
    return $localize`:@@tooManyRequests:Too many attempts. Wait a minute and try again.`;
  }

  return announced.has(error.status) ? (error.detail ?? error.title) : null;
}
