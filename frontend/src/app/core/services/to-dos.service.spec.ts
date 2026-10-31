import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ToDosService } from './to-dos.service';

describe('ToDosService', () => {
  let service: ToDosService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ToDosService);
    http = TestBed.inject(HttpTestingController);
  });

  it('adds up the own and the review counts', async () => {
    const loaded = service.load();
    http.expectOne('/api/me/to-dos').flush({
      clockedInSince: null,
      returnedWeeks: ['2026-10-12'],
      unsubmittedWeeks: ['2026-10-19'],
      declinedCorrections: 1,
      timesheetsToReview: 2,
      correctionsToDecide: 1,
    });
    await loaded;

    expect(service.ownCount()).toBe(3);
    expect(service.reviewCount()).toBe(3);
  });

  it('keeps zero counts when the load fails', async () => {
    const loaded = service.load();
    http.expectOne('/api/me/to-dos').flush(null, { status: 500, statusText: 'Error' });
    await loaded;

    expect(service.ownCount()).toBe(0);
  });
});
