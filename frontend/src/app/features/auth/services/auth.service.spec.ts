import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  it('sets the password with the link token', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const backend = TestBed.inject(HttpTestingController);

    const setting = TestBed.inject(AuthService).setPassword('abc.def', 'correct horse battery');
    const request = backend.expectOne({ method: 'POST', url: '/api/auth/set-password' });
    request.flush(null, { status: 204, statusText: 'No Content' });
    await setting;

    expect(request.request.body).toEqual({ token: 'abc.def', password: 'correct horse battery' });
  });
});
