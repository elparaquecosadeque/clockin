import { HttpInterceptorFn } from '@angular/common/http';

export const correlationIdHeader = 'X-Correlation-Id';

/** Sends a correlation ID on every request, so a user's report can be matched to the server logs. */
export const correlationIdInterceptor: HttpInterceptorFn = (request, next) =>
  next(request.clone({ setHeaders: { [correlationIdHeader]: crypto.randomUUID() } }));
