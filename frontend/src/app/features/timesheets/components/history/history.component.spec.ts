import { TestBed } from '@angular/core/testing';
import { HistoryItem } from '../../models/history-item.model';
import { HistoryComponent } from './history.component';

describe('HistoryComponent', () => {
  it('lists transitions and entry changes with before and after', async () => {
    const items: HistoryItem[] = [
      {
        kind: 'Submitted',
        actorName: 'Ada',
        byOwner: false,
        at: '2026-10-26T15:00:00Z',
        comment: null,
        before: null,
        after: null,
      },
      {
        kind: 'EntryEdited',
        actorName: 'Eve',
        byOwner: true,
        at: '2026-10-27T15:00:00Z',
        comment: 'badge was slow',
        before: {
          entryId: 'e1',
          start: '2026-10-20T13:02:00Z',
          end: '2026-10-20T22:00:00Z',
          punchedStart: null,
          punchedEnd: null,
        },
        after: {
          entryId: 'e1',
          start: '2026-10-20T13:00:00Z',
          end: '2026-10-20T22:00:00Z',
          punchedStart: null,
          punchedEnd: null,
        },
      },
    ];
    const fixture = TestBed.createComponent(HistoryComponent);
    fixture.componentRef.setInput('items', items);
    fixture.componentRef.setInput('timeZone', 'America/Bogota');
    await fixture.whenStable();

    const rows = [...(fixture.nativeElement as HTMLElement).querySelectorAll('li')].map((li) =>
      li.textContent!.replace(/\s+/g, ' '),
    );
    expect(rows[0]).toContain("Submitted by Ada on the owner's behalf");
    expect(rows[1]).toContain('Entry edited by Eve');
    expect(rows[1]).toContain('08:02 – 17:00 → Tue, Oct 20, 08:00 – 17:00');
    expect(rows[1]).toContain('badge was slow');
  });
});
