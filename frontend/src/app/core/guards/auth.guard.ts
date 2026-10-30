import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from '../services/session.service';

/** Convenience only: the API enforces authentication on every request. */
export const authGuard: CanActivateFn = () =>
  inject(SessionService).isSignedIn() || inject(Router).createUrlTree(['/sign-in']);
