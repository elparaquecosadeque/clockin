import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ToDosService } from '../../../../core/services/to-dos.service';
import { Timesheet } from '../../models/timesheet.model';
import { TimesheetService } from '../../services/timesheet.service';
import { TimesheetComponent } from './timesheet.component';

const actions = {
  changeEntries: false,
  submit: false,
  approve: false,
  return: false,
  reopen: false,
  requestCorrection: false,
  decideCorrection: false,
};

const week: Timesheet = {
  id: 't1',
  version: 3,
  ownerId: 'u1',
  ownerName: 'Eve',
  teamName: 'Warehouse',
  weekStart: '2026-10-19',
  status: 'Submitted',
  returnComment: null,
  approval: null,
  totalSeconds: 9 * 3600,
  days: [0, 1, 2, 3, 4, 5, 6].map((d) => ({
    date: `2026-10-${19 + d}`,
    seconds: d === 1 ? 9 * 3600 : 0,
    hasOpenEntry: false,
  })),
  entries: [
    {
      id: 'e1',
      date: '2026-10-20',
      start: '2026-10-20T13:00:00Z',
      end: '2026-10-20T22:00:00Z',
      seconds: 9 * 3600,
      manualChange: 'Edited',
      changedByName: 'Eve',
      note: 'badge was slow',
      punchedStart: '2026-10-20T13:02:00Z',
      punchedEnd: '2026-10-20T22:00:00Z',
    },
  ],
  history: [],
  latestCorrection: null,
  can: { ...actions, approve: true, return: true },
  timeZone: 'America/Bogota',
};

describe('TimesheetComponent', () => {
  const service = {
    get: vi.fn(async (): Promise<Timesheet> => week),
    approve: vi.fn(async () => undefined),
    return: vi.fn(async () => undefined),
  };
  const open = vi.fn();
  let fixture: ComponentFixture<TimesheetComponent>;

  beforeEach(async () => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      imports: [TimesheetComponent],
      providers: [
        provideRouter([]),
        { provide: TimesheetService, useValue: service },
        { provide: ToDosService, useValue: { load: vi.fn() } },
        { provide: MatDialog, useValue: { open } },
      ],
    });
    fixture = TestBed.createComponent(TimesheetComponent);
    fixture.componentRef.setInput('ownerId', 'u1');
    fixture.componentRef.setInput('weekStart', '2026-10-21');
    await fixture.whenStable();
  });

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent!.replace(/\s+/g, ' ');
  }

  function button(label: string): HTMLButtonElement {
    return [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find((b) =>
      b.textContent!.includes(label),
    )!;
  }

  it('loads the week holding the given day, for the given owner', () => {
    expect(service.get).toHaveBeenCalledWith('u1', '2026-10-19');
  });

  it('shows status, totals and each entry with its flag, note and punched values', () => {
    expect(text()).toContain('Eve');
    expect(text()).toContain('Submitted');
    expect(text()).toContain('9:00');
    expect(text()).toContain('08:00 – 17:00');
    expect(text()).toContain('Edited by Eve');
    expect(text()).toContain('badge was slow');
    expect(text()).toContain('Punched 08:02 – 17:00');
  });

  it('offers only the actions the server allows', () => {
    expect(button('Approve')).toBeTruthy();
    expect(button('Return')).toBeTruthy();
    expect(button('Submit')).toBeUndefined();
    expect(button('Add entry')).toBeUndefined();
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[aria-label="Edit entry"]'),
    ).toBeNull();
  });

  it('approves and reloads', async () => {
    button('Approve').click();
    await fixture.whenStable();

    expect(service.approve).toHaveBeenCalledWith('t1');
    expect(service.get).toHaveBeenCalledTimes(2);
  });

  it('returns with the comment from the dialog, and does nothing when cancelled', async () => {
    open.mockReturnValueOnce({ afterClosed: () => of(undefined) });
    button('Return').click();
    await fixture.whenStable();
    expect(service.return).not.toHaveBeenCalled();

    open.mockReturnValueOnce({ afterClosed: () => of('Wednesday is missing') });
    button('Return').click();
    await fixture.whenStable();
    expect(service.return).toHaveBeenCalledWith('t1', 'Wednesday is missing');
  });
});
