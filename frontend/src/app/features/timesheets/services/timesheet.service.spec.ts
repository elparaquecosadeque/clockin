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

  it('reads and submits the own week or another user’s', async () => {
    void service.get(null, '2026-10-19');
    http.expectOne({ method: 'GET', url: '/api/timesheets/mine/2026-10-19' }).flush({});
    void service.submit('u2', '2026-10-19');
    http
      .expectOne({ method: 'POST', url: '/api/timesheets/users/u2/2026-10-19/submit' })
      .flush(null);
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
