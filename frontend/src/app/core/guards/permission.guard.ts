import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from '../services/session.service';

/**
 * Lets through users with any of the permissions. Convenience only: the API enforces every permission.
 * Without one, the user goes to their start page.
 */
export function permissionGuard(...permissions: string[]): CanActivateFn {
  return () => {
    const session = inject(SessionService);
    return permissions.some((p) => session.hasPermission(p)) || inject(Router).createUrlTree(['/']);
  };
}
