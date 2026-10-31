import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ClockStatus } from '../../models/clock-status.model';
import { ClockService } from '../../services/clock.service';
import { ClockComponent } from './clock.component';

// 2026-10-30 10:00 in Bogotá (UTC-5).
const now = new Date('2026-10-30T15:00:00Z');

const idle: ClockStatus = {
  openEntry: null,
  openEntryStartedToday: false,
  openEntryStartedThisWeek: false,
  todayEntries: [],
  closedTodaySeconds: 0,
  closedWeekSeconds: 5 * 3600,
  weekStart: '2026-10-26',
  timeZone: 'America/Bogota',
};

const openEntry = {
  id: 'e1',
  timesheetId: 't1',
  start: '2026-10-30T13:00:00Z',
  end: null,
  isManuallyChanged: false,
};

const clockedIn: ClockStatus = {
  ...idle,
  openEntry,
  openEntryStartedToday: true,
  openEntryStartedThisWeek: true,
  todayEntries: [openEntry],
};

describe('ClockComponent', () => {
  const status = signal<ClockStatus | null>(null);
  const clock = {
    status,
    load: vi.fn(async () => undefined),
    clockIn: vi.fn(async () => undefined),
    clockOut: vi.fn(async () => undefined),
  };
  let fixture: ComponentFixture<ClockComponent>;

  beforeEach(async () => {
    vi.useFakeTimers({ toFake: ['Date'], now });
    vi.clearAllMocks();
    status.set(idle);
    TestBed.configureTestingModule({
      imports: [ClockComponent],
      providers: [provideRouter([]), { provide: ClockService, useValue: clock }],
    });
    fixture = TestBed.createComponent(ClockComponent);
    await fixture.whenStable();
  });

  afterEach(() => vi.useRealTimers());

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent?.replace(/\s+/g, ' ') ?? '';
  }

  function button(): HTMLButtonElement {
    return (fixture.nativeElement as HTMLElement).querySelector('button.punch')!;
  }

  it('offers a single "Clock in" when not clocked in', () => {
    expect(button().textContent).toContain('Clock in');
    expect(text()).toContain('Not clocked in');
    expect(text()).toContain('No entries yet today.');
  });

  it('clocks in with the button', async () => {
    button().click();
    await fixture.whenStable();

    expect(clock.clockIn).toHaveBeenCalled();
  });

  it('shows since when, and adds the running entry to the totals', async () => {
    status.set(clockedIn);
    await fixture.whenStable();

    expect(button().textContent).toContain('Clock out');
    expect(text()).toContain('Clocked in since 08:00');
    expect(text()).toContain('2:00:00');
    const totals = [...(fixture.nativeElement as HTMLElement).querySelectorAll('.total')].map(
      (total) => total.textContent,
    );
    expect(totals).toEqual(['Today2:00', 'This week7:00']);
    expect(text()).toContain('08:00 – Open');
  });

  it('explains an entry that is too long to close', async () => {
    status.set(clockedIn);
    clock.clockOut.mockRejectedValueOnce({ status: 422, code: 'EntryTooLong', fieldErrors: {} });
    await fixture.whenStable();

    button().click();
    await fixture.whenStable();

    expect(text()).toContain('This entry is over 16 hours');
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('.warning a')!.getAttribute('href'),
    ).toBe('/timesheets/2026-10-26');
  });
});
