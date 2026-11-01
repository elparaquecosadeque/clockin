import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TimesheetService } from '../../services/timesheet.service';
import { ReviewQueueComponent } from './review-queue.component';

describe('ReviewQueueComponent', () => {
  it('pages the two queues independently and keeps the page on a failed request', async () => {
    const reviewQueue = vi.fn(async () => ({
      timesheets: [],
      corrections: [],
      hasMoreTimesheets: true,
      hasMoreCorrections: true,
    }));
    TestBed.configureTestingModule({
      imports: [ReviewQueueComponent],
      providers: [provideRouter([]), { provide: TimesheetService, useValue: { reviewQueue } }],
    });
    const fixture = TestBed.createComponent(ReviewQueueComponent);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    element
      .querySelector<HTMLButtonElement>('[aria-label="Timesheet pages"] button:last-child')!
      .click();
    await fixture.whenStable();
    expect(reviewQueue).toHaveBeenLastCalledWith(2, 1);
    element
      .querySelector<HTMLButtonElement>('[aria-label="Correction pages"] button:last-child')!
      .click();
    await fixture.whenStable();
    expect(reviewQueue).toHaveBeenLastCalledWith(2, 2);
    reviewQueue.mockRejectedValueOnce(new Error('offline'));
    element
      .querySelector<HTMLButtonElement>('[aria-label="Timesheet pages"] button:last-child')!
      .click();
    await fixture.whenStable();
    expect(element.querySelector('[role=alert]')).not.toBeNull();
    expect(element.querySelector('[aria-label="Timesheet pages"]')!.textContent).toContain(
      'Page 2',
    );
  });

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
