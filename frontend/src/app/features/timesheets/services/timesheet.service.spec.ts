import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TimesheetService } from './timesheet.service';

describe('TimesheetService', () => {
  let service: TimesheetService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(TimesheetService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const owner = '0192f0a4-7c1e-7b3a-9d2e-5f6a7b8c9d0e';

  it('requests independent pages for submitted weeks and corrections', async () => {
    const queue = service.reviewQueue(2, 3);
    const response = {
      timesheets: [],
      corrections: [],
      hasMoreTimesheets: false,
      hasMoreCorrections: true,
    };
    http.expectOne('/api/reports/review-queue?timesheetPage=2&correctionPage=3').flush(response);
    expect(await queue).toEqual(response);
  });

  it('reads and submits the own week or another user’s', async () => {
    void service.get(null, '2026-10-19');
    http.expectOne({ method: 'GET', url: '/api/timesheets/mine/2026-10-19' }).flush({});
    void service.submit(owner, '2026-10-19');
    http
      .expectOne({ method: 'POST', url: `/api/timesheets/users/${owner}/2026-10-19/submit` })
      .flush(null);
  });

  it('refuses an owner id from the URL that is not a user id, without calling the API', async () => {
    await expect(service.get('..', '2026-10-19')).rejects.toThrow();
    await expect(service.submit('../../reports/team-week', '2026-10-19')).rejects.toThrow();
  });

  it('sends the version with edits and deletes', () => {
    void service.editEntry('e1', 's', null, 'forgot to clock out', 3);
    expect(http.expectOne({ method: 'PUT', url: '/api/time-entries/e1' }).request.body).toEqual({
      start: 's',
      end: null,
      note: 'forgot to clock out',
      version: 3,
    });
    void service.deleteEntry('e1', 'duplicate', 4);
    expect(http.expectOne({ method: 'DELETE', url: '/api/time-entries/e1' }).request.body).toEqual({
      note: 'duplicate',
      version: 4,
    });
  });

  it('decides on correction requests', () => {
    void service.declineCorrection('r1', 'times are right');
    expect(
      http.expectOne({ method: 'POST', url: '/api/correction-requests/r1/decline' }).request.body,
    ).toEqual({ comment: 'times are right' });
  });
});
