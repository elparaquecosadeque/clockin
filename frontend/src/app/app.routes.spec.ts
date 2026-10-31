import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { SessionService } from './core/services/session.service';

describe('routes', () => {
  function setUp(permissions: string[] | null): void {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: SessionService,
          useValue: {
            isSignedIn: () => permissions !== null,
            hasPermission: (p: string) => permissions?.includes(p) ?? false,
            user: () => null,
            accessToken: () => null,
          },
        },
      ],
    });
  }

  async function open(url: string): Promise<string> {
    await RouterTestingHarness.create(url);
    return TestBed.inject(Router).url;
  }

  it('sends a signed-out user to sign in', async () => {
    setUp(null);

    expect(await open('/')).toBe('/sign-in');
  });

  it('starts a user who punches time on the clock', async () => {
    setUp(['time-entries.punch']);

    expect(await open('/')).toBe('/clock');
  });

  it('starts an admin on the review queue, and sends them there from pages they lack', async () => {
    setUp(['users.manage', 'timesheets.review-all']);

    expect(await open('/clock')).toBe('/review');
  });
});
