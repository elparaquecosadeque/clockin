import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SessionService } from '../services/session.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  const token = signal<string | null>('old-token');
  const refresh = vi.fn<() => Promise<boolean>>();
  const navigate = vi.fn();
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    token.set('old-token');
    refresh.mockReset();
    navigate.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: SessionService, useValue: { accessToken: token, refresh } },
        { provide: Router, useValue: { navigate } },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('attaches the access token', () => {
    http.get('/api/me').subscribe();

    expect(backend.expectOne('/api/me').request.headers.get('Authorization')).toBe(
      'Bearer old-token',
    );
  });

  it('leaves the auth endpoints alone', () => {
    http.post('/api/auth/refresh', null).subscribe();

    expect(backend.expectOne('/api/auth/refresh').request.headers.has('Authorization')).toBe(false);
  });

  it('refreshes once and retries with the new token after a 401', async () => {
    refresh.mockImplementation(async () => {
      token.set('new-token');
      return true;
    });
    const response = firstValueFrom(http.get<{ ok: boolean }>('/api/me'));

    backend.expectOne('/api/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await new Promise((resolve) => setTimeout(resolve));
    const retry = backend.expectOne('/api/me');
    retry.flush({ ok: true });

    expect(retry.request.headers.get('Authorization')).toBe('Bearer new-token');
    expect(await response).toEqual({ ok: true });
    expect(refresh).toHaveBeenCalledTimes(1);
  });

  it('sends the user to sign in when the refresh fails', async () => {
    refresh.mockResolvedValue(false);
    const response = firstValueFrom(http.get('/api/me'));

    backend.expectOne('/api/me').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(response).rejects.toMatchObject({ status: 401 });
    expect(navigate).toHaveBeenCalledWith(['/sign-in']);
  });
});
