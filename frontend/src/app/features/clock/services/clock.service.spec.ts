import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SILENT_ERRORS } from '../../../core/models/silent-errors.model';
import { ClockStatus } from '../models/clock-status.model';
import { ClockService } from './clock.service';

const status: ClockStatus = {
  openEntry: null,
  openEntryStartedToday: false,
  openEntryStartedThisWeek: false,
  todayEntries: [],
  closedTodaySeconds: 0,
  closedWeekSeconds: 3600,
  weekStart: '2026-10-26',
  timeZone: 'America/Bogota',
};

describe('ClockService', () => {
  let service: ClockService;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ClockService);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('loads the status', async () => {
    const loading = service.load();
    backend.expectOne('/api/time-entries/clock').flush(status);
    await loading;

    expect(service.status()).toEqual(status);
  });

  it('reloads the status after clocking in', async () => {
    const clockingIn = service.clockIn();
    backend.expectOne({ method: 'POST', url: '/api/time-entries/clock-in' }).flush(null);
    await new Promise((resolve) => setTimeout(resolve));
    backend.expectOne('/api/time-entries/clock').flush(status);
    await clockingIn;

    expect(service.status()).toEqual(status);
  });

  it('leaves a too-long entry for the page to explain', async () => {
    const clockingOut = service.clockOut().catch(() => undefined);
    const request = backend.expectOne({ method: 'POST', url: '/api/time-entries/clock-out' });
    request.flush(null, { status: 422, statusText: 'Unprocessable' });
    await clockingOut;

    expect(request.request.context.get(SILENT_ERRORS)).toEqual(['EntryTooLong']);
  });
});
