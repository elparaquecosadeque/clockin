import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TimesheetEntry } from '../../models/timesheet-entry.model';
import { EntryDialogComponent, EntryDialogData } from './entry-dialog.component';

const zone = 'America/Bogota';

describe('EntryDialogComponent', () => {
  const close = vi.fn();
  const save = vi.fn(async () => undefined);

  async function render(entry?: TimesheetEntry): Promise<HTMLElement> {
    const data: EntryDialogData = { weekStart: '2026-10-19', timeZone: zone, entry, save };
    TestBed.configureTestingModule({
      imports: [EntryDialogComponent],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: { close } },
      ],
    });
    const fixture = TestBed.createComponent(EntryDialogComponent);
    await fixture.whenStable();
    return fixture.nativeElement;
  }

  function fill(element: HTMLElement, name: string, value: string): void {
    const field = element.querySelector<HTMLInputElement>(`[formControlName="${name}"]`)!;
    field.value = value;
    field.dispatchEvent(new Event('input'));
  }

  async function submit(element: HTMLElement): Promise<void> {
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    await new Promise((resolve) => setTimeout(resolve));
  }

  beforeEach(() => vi.clearAllMocks());

  it('adds an overnight entry in the company time zone', async () => {
    const element = await render();
    fill(element, 'date', '2026-10-20');
    fill(element, 'start', '22:00');
    fill(element, 'end', '02:00');
    fill(element, 'note', ' night shift, badge reader down ');
    await submit(element);

    expect(save).toHaveBeenCalledWith({
      start: '2026-10-21T03:00:00.000Z',
      end: '2026-10-21T07:00:00.000Z',
      note: 'night shift, badge reader down',
    });
    expect(close).toHaveBeenCalledWith(true);
  });

  it('needs a note', async () => {
    const element = await render();
    fill(element, 'date', '2026-10-20');
    fill(element, 'start', '08:00');
    fill(element, 'end', '12:00');
    await submit(element);

    expect(save).not.toHaveBeenCalled();
  });

  it('keeps an open entry open when the end is left empty', async () => {
    const element = await render({
      id: 'e1',
      date: '2026-10-23',
      start: '2026-10-23T13:00:00Z',
      end: null,
      seconds: 0,
      manualChange: 'None',
      changedByName: null,
      note: null,
      punchedStart: null,
      punchedEnd: null,
    });
    expect(element.querySelector<HTMLInputElement>('[formControlName="start"]')!.value).toBe(
      '08:00',
    );
    fill(element, 'start', '08:45');
    fill(element, 'note', 'started late');
    await submit(element);

    expect(save).toHaveBeenCalledWith({
      start: '2026-10-23T13:45:00.000Z',
      end: null,
      note: 'started late',
    });
  });

  it('stays open and lists validation errors from the server', async () => {
    save.mockRejectedValueOnce({
      status: 400,
      fieldErrors: { note: ['Use at most 500 characters.'] },
    });
    const element = await render();
    fill(element, 'date', '2026-10-20');
    fill(element, 'start', '08:00');
    fill(element, 'end', '12:00');
    fill(element, 'note', 'x');
    await submit(element);
    TestBed.tick();

    expect(close).not.toHaveBeenCalled();
    expect(element.querySelector('.server-error')?.textContent).toContain('500');
  });
});
