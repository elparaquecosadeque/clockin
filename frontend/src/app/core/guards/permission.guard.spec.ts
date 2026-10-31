import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { SessionService } from '../services/session.service';
import { permissionGuard } from './permission.guard';

describe('permissionGuard', () => {
  const hasPermission = vi.fn<(permission: string) => boolean>();

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: SessionService, useValue: { hasPermission } }],
    });
  });

  function run(): unknown {
    return TestBed.runInInjectionContext(() =>
      permissionGuard('time-entries.punch')(
        {} as ActivatedRouteSnapshot,
        {} as RouterStateSnapshot,
      ),
    );
  }

  it('lets a user with the permission through', () => {
    hasPermission.mockReturnValue(true);

    expect(run()).toBe(true);
    expect(hasPermission).toHaveBeenCalledWith('time-entries.punch');
  });

  it('sends a user without the permission to the start page', () => {
    hasPermission.mockReturnValue(false);

    expect(TestBed.inject(Router).serializeUrl(run() as UrlTree)).toBe('/');
  });
});
