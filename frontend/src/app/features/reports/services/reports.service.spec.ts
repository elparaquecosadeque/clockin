import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ReportsService } from './reports.service';

describe('ReportsService', () => {
  let service: ReportsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ReportsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('downloads the CSV of one team with the server file name', async () => {
    const file = service.approvedCsv('2026-10-19', 't1');
    http.expectOne('/api/reports/approved-csv/2026-10-19?teamId=t1').flush(new Blob(['a,b']), {
      headers: {
        'Content-Disposition': "attachment; filename=approved-2026-10-19.csv; filename*=UTF-8''x",
      },
    });

    expect((await file).name).toBe('approved-2026-10-19.csv');
  });
});
