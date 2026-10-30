import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { correlationIdHeader, correlationIdInterceptor } from './correlation-id.interceptor';

describe('correlationIdInterceptor', () => {
  it('sends a fresh correlation ID the server accepts on every request', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([correlationIdInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    const http = TestBed.inject(HttpClient);
    const backend = TestBed.inject(HttpTestingController);

    http.get('/api/me').subscribe();
    http.get('/api/me').subscribe();
    const [first, second] = backend
      .match('/api/me')
      .map((r) => r.request.headers.get(correlationIdHeader));

    expect(first).toMatch(/^[A-Za-z0-9-]{1,64}$/);
    expect(second).not.toBe(first);
  });
});
