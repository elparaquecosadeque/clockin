import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SessionUser } from '../models/session-user.model';
import { SessionService, localizedPath } from './session.service';

const eve: SessionUser = {
  id: '0193a1b2-c3d4-7e8f-9a0b-1c2d3e4f5a6b',
  name: 'Eve',
  email: 'eve@clockin.test',
  role: 'Employee',
  permissions: ['time-entries.punch'],
  language: 'en',
  timeFormat: '24h',
  teamName: 'Warehouse',
  supervisorName: 'Sam',
};

describe('SessionService', () => {
  let session: SessionService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    session = TestBed.inject(SessionService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function flushMicrotasks(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
  }

  it('keeps the access token in memory and loads the user when signing in', async () => {
    const signedIn = session.signIn('eve@clockin.test', 'correct horse battery');
    http
      .expectOne({ method: 'POST', url: '/api/auth/login' })
      .flush({ accessToken: 'token-1', expiresAt: '2026-10-30T15:15:00Z' });
    await flushMicrotasks();
    http.expectOne('/api/me').flush(eve);
    await signedIn;

    expect(session.accessToken()).toBe('token-1');
    expect(session.user()).toEqual(eve);
    expect(session.hasPermission('time-entries.punch')).toBe(true);
    expect(session.hasPermission('users.manage')).toBe(false);
  });

  it('shares one refresh between concurrent callers, so a rotated token is never reused', async () => {
    const first = session.refresh();
    const second = session.refresh();

    http.expectOne('/api/auth/refresh').flush({ accessToken: 'token-2', expiresAt: '' });

    expect(await first).toBe(true);
    expect(await second).toBe(true);
    expect(session.accessToken()).toBe('token-2');
  });

  it('signs out locally when the refresh is rejected', async () => {
    const refreshed = session.refresh();
    http.expectOne('/api/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(await refreshed).toBe(false);
    expect(session.accessToken()).toBeNull();
    expect(session.isSignedIn()).toBe(false);
  });

  it('restores the session from the refresh cookie on start', async () => {
    const restored = session.restore();
    http.expectOne('/api/auth/refresh').flush({ accessToken: 'token-3', expiresAt: '' });
    await flushMicrotasks();
    http.expectOne('/api/me').flush(eve);
    await restored;

    expect(session.isSignedIn()).toBe(true);
  });

  it('clears the session even when the server cannot be reached on sign-out', async () => {
    const signedOut = session.signOut();
    http.expectOne('/api/auth/logout').error(new ProgressEvent('error'));
    await signedOut;

    expect(session.isSignedIn()).toBe(false);
  });

  it('keeps this session with the new token after a password change', async () => {
    const changed = session.changePassword('old password!!', 'a new long password');
    http
      .expectOne({ method: 'POST', url: '/api/me/password' })
      .flush({ accessToken: 'token-4', expiresAt: '' });
    await changed;

    expect(session.accessToken()).toBe('token-4');
  });
});

describe('localizedPath', () => {
  it('swaps the locale segment when the language differs', () => {
    expect(localizedPath('/en/timesheets/2026-10-26', 'en-US', 'es')).toBe(
      '/es/timesheets/2026-10-26',
    );
    expect(localizedPath('/es/clock', 'es', 'en')).toBe('/en/clock');
  });

  it('stays put when the build matches or is served at the root', () => {
    expect(localizedPath('/en/clock', 'en-US', 'en')).toBeNull();
    expect(localizedPath('/clock', 'en-US', 'es')).toBeNull();
  });
});
