import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TimesheetService } from '../../services/timesheet.service';
import { ReviewQueueComponent } from './review-queue.component';

describe('ReviewQueueComponent', () => {
  it('links each queued timesheet and correction to its week', async () => {
    TestBed.configureTestingModule({
      imports: [ReviewQueueComponent],
      providers: [
        provideRouter([]),
        {
          provide: TimesheetService,
          useValue: {
            reviewQueue: async () => ({
              timesheets: [
                {
                  timesheetId: 't1',
                  ownerId: 'u1',
                  name: 'Eve',
                  teamName: 'Warehouse',
                  weekStart: '2026-10-19',
                  totalSeconds: 40 * 3600,
                  submittedAt: null,
                },
              ],
              corrections: [
                {
                  requestId: 'r1',
                  timesheetId: 't2',
                  ownerId: 'u2',
                  name: 'Ed',
                  teamName: 'Warehouse',
                  weekStart: '2026-10-12',
                  timesheetStatus: 'Approved',
                  reason: 'Wednesday is missing',
                  requestedAt: '2026-10-28T15:00:00Z',
                },
              ],
            }),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(ReviewQueueComponent);
    await fixture.whenStable();

    const links = [...(fixture.nativeElement as HTMLElement).querySelectorAll('a')];
    expect(links.map((a) => a.getAttribute('href'))).toEqual([
      '/review/u1/2026-10-19',
      '/review/u2/2026-10-12',
    ]);
    expect(links[0].textContent).toContain('40:00');
    expect(links[1].textContent).toContain('Wednesday is missing');
  });

  it('says when there is nothing to do', async () => {
    TestBed.configureTestingModule({
      imports: [ReviewQueueComponent],
      providers: [
        provideRouter([]),
        {
          provide: TimesheetService,
          useValue: { reviewQueue: async () => ({ timesheets: [], corrections: [] }) },
        },
      ],
    });
    const fixture = TestBed.createComponent(ReviewQueueComponent);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Nothing to review.');
  });
});
