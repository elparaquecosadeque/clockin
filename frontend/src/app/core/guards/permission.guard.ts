import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from '../services/session.service';

/** Convenience only: the API enforces every permission. Without it, the user goes to the home page. */
export function permissionGuard(permission: string): CanActivateFn {
  return () =>
    inject(SessionService).hasPermission(permission) || inject(Router).createUrlTree(['/home']);
}
