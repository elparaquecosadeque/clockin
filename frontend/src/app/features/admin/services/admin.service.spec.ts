import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AdminService } from './admin.service';

describe('AdminService', () => {
  let service: AdminService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AdminService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('changes a role with the team an employee goes to', () => {
    void service.changeRole('u1', 'Employee', 't1');
    expect(http.expectOne({ method: 'PUT', url: '/api/users/u1/role' }).request.body).toEqual({
      role: 'Employee',
      teamId: 't1',
    });
  });

  it('moves an employee to a team', () => {
    void service.moveEmployee('u1', 't2');
    http.expectOne({ method: 'PUT', url: '/api/teams/t2/employees/u1' }).flush(null);
  });

  it('reactivates into a chosen team or the old one', () => {
    void service.reactivate('u1', null);
    expect(
      http.expectOne({ method: 'POST', url: '/api/users/u1/reactivate' }).request.body,
    ).toEqual({
      teamId: null,
    });
  });
});
