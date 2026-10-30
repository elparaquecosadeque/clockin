import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';
import { SILENT_ERRORS } from '../models/silent-errors.model';
import { errorInterceptor } from './error.interceptor';

describe('errorInterceptor', () => {
  const open = vi.fn();
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    open.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: MatSnackBar, useValue: { open } },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  async function fail(
    status: number,
    body: object | null,
    context?: HttpContext,
  ): Promise<unknown> {
    const response = firstValueFrom(http.post('/api/time-entries/clock-in', null, { context }));
    backend.expectOne('/api/time-entries/clock-in').flush(body, { status, statusText: 'Error' });
    return response.catch((error: unknown) => error);
  }

  it('turns a broken rule into a typed error and shows its message', async () => {
    const error = await fail(422, {
      title: 'A business rule was broken.',
      detail: 'An entry is already open.',
      code: 'EntryAlreadyOpen',
    });

    expect(error).toEqual({
      status: 422,
      title: 'A business rule was broken.',
      detail: 'An entry is already open.',
      code: 'EntryAlreadyOpen',
      fieldErrors: {},
    });
    expect(open).toHaveBeenCalledWith('An entry is already open.', 'Dismiss', { duration: 6000 });
  });

  it('keeps field errors for forms and shows no snackbar on 400', async () => {
    const error = await fail(400, { title: 'Invalid.', errors: { password: ['Too short.'] } });

    expect(error).toMatchObject({ status: 400, fieldErrors: { password: ['Too short.'] } });
    expect(open).not.toHaveBeenCalled();
  });

  it('shows a generic message for server errors', async () => {
    await fail(500, { title: 'An unexpected error occurred.' });

    expect(open).toHaveBeenCalledWith(
      'Something went wrong on our side. Try again in a moment.',
      'Dismiss',
      { duration: 6000 },
    );
  });

  it('stays quiet for the codes the caller handles itself', async () => {
    await fail(
      422,
      { code: 'EntryTooLong' },
      new HttpContext().set(SILENT_ERRORS, ['EntryTooLong']),
    );

    expect(open).not.toHaveBeenCalled();
  });
});
