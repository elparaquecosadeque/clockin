import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { SessionService } from '../services/session.service';
import { authGuard } from './auth.guard';

describe('authGuard', () => {
  const isSignedIn = vi.fn<() => boolean>();

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: SessionService, useValue: { isSignedIn } }],
    });
  });

  function run(): unknown {
    return TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );
  }

  it('lets a signed-in user through', () => {
    isSignedIn.mockReturnValue(true);

    expect(run()).toBe(true);
  });

  it('sends everyone else to sign in', () => {
    isSignedIn.mockReturnValue(false);

    expect(TestBed.inject(Router).serializeUrl(run() as UrlTree)).toBe('/sign-in');
  });
});
