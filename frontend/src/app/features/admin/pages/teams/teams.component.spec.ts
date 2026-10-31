import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import { AdminService } from '../../services/admin.service';
import { TeamsComponent } from './teams.component';

describe('TeamsComponent', () => {
  const admin = {
    teams: vi.fn(async () => [
      {
        id: 'w',
        name: 'Warehouse',
        supervisorId: 'sam',
        supervisorName: 'Sam',
        isArchived: false,
        employeeCount: 2,
      },
      {
        id: 'o',
        name: 'Old Store',
        supervisorId: 'sue',
        supervisorName: 'Sue',
        isArchived: true,
        employeeCount: 0,
      },
    ]),
    users: vi.fn(async () => [
      { id: 'sam', name: 'Sam', role: 'Supervisor', status: 'Active', ledTeamId: 'w' },
      { id: 'sid', name: 'Sid', role: 'Supervisor', status: 'Active', ledTeamId: null },
      { id: 'rob', name: 'Rob', role: 'Supervisor', status: 'Deactivated', ledTeamId: null },
    ]),
    createTeam: vi.fn(async () => undefined),
  };
  const open = vi.fn(() => ({ afterClosed: () => of(true) }));

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      imports: [TeamsComponent],
      providers: [
        { provide: AdminService, useValue: admin },
        { provide: MatDialog, useValue: { open } },
      ],
    });
  });

  it('lists teams with supervisor and headcount, and no actions on archived ones', async () => {
    const fixture = TestBed.createComponent(TeamsComponent);
    await fixture.whenStable();

    const rows = [...(fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr')];
    expect(rows[0].textContent).toContain('Sam');
    expect(rows[0].querySelector('.menu button')).not.toBeNull();
    expect(rows[1].textContent).toContain('Archived');
    expect(rows[1].querySelector('.menu button')).toBeNull();
  });

  it('offers only active supervisors who lead no team for a new team', async () => {
    const fixture = TestBed.createComponent(TeamsComponent);
    await fixture.whenStable();

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('.head button')!
      .click();

    const data = (open.mock.calls[0] as unknown[])[1] as {
      data: { supervisors: { id: string }[] };
    };
    expect(data.data.supervisors.map((s) => s.id)).toEqual(['sid']);
  });
});
