import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatSelectHarness } from '@angular/material/select/testing';
import { provideRouter } from '@angular/router';
import { TeamWeek, TeamWeekGroup } from '../../models/team-week.model';
import { ReportsService } from '../../services/reports.service';
import { TeamWeekComponent } from './team-week.component';

function group(teamId: string, teamName: string, person: string): TeamWeekGroup {
  return {
    teamId,
    teamName,
    supervisorName: 'Sam',
    isSupervisors: false,
    summary: { notStarted: 0, draft: 1, submitted: 0, approved: 0 },
    rows: [
      {
        userId: person,
        name: person,
        status: 'Draft',
        timesheetId: null,
        daySeconds: [8 * 3600, 7.5 * 3600, 0, 0, 2 * 3600, 0, 0],
        openDay: 4,
        totalSeconds: 17.5 * 3600,
      },
    ],
  };
}

const grid: TeamWeek = {
  weekStart: '2026-10-26',
  groups: [group('w', 'Warehouse', 'Eve'), group('o', 'Office', 'Olga')],
  timeZone: 'America/Bogota',
};

describe('TeamWeekComponent', () => {
  const reports = {
    teamWeek: vi.fn(async () => grid),
    clockedIn: vi.fn(async () => [
      {
        userId: 'Eve',
        name: 'Eve',
        teamName: 'Warehouse',
        since: '2026-10-30T13:00:00Z',
        seconds: 7200,
        overLimit: false,
      },
    ]),
    exceptions: vi.fn(async () => []),
  };
  let fixture: ComponentFixture<TeamWeekComponent>;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [TeamWeekComponent],
      providers: [provideRouter([]), { provide: ReportsService, useValue: reports }],
    });
    fixture = TestBed.createComponent(TeamWeekComponent);
    fixture.componentRef.setInput('weekStart', '2026-10-26');
    await fixture.whenStable();
  });

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('shows one row per person with day totals, the open day and the status', () => {
    const cells = [...element().querySelectorAll('.group')[0].querySelectorAll('tbody td')].map(
      (td) => td.textContent!.trim(),
    );
    expect(cells).toEqual([
      '8:00',
      '7:30',
      '0:00',
      '0:00',
      '2:00',
      '0:00',
      '0:00',
      '17:30',
      'Draft',
    ]);
    expect(element().querySelector('td.open')?.textContent?.trim()).toBe('2:00');
    expect(element().querySelector('.group-head')?.textContent).toContain('Led by Sam');
  });

  it('narrows to one team', async () => {
    const select = await TestbedHarnessEnvironment.loader(fixture).getHarness(MatSelectHarness);
    await select.open();
    await select.clickOptions({ text: 'Office' });

    expect([...element().querySelectorAll('.group h3')].map((h) => h.textContent!.trim())).toEqual([
      'Office',
    ]);
  });

  it('shows who is clocked in now', () => {
    expect(element().querySelector('.panel')?.textContent).toContain('since Fri, Oct 30, 08:00');
  });
});
