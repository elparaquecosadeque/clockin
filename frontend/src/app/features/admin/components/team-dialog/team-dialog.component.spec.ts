import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TeamDialogComponent, TeamDialogData } from './team-dialog.component';

describe('TeamDialogComponent', () => {
  const close = vi.fn();
  const save = vi.fn(async () => undefined);

  it('renames without asking for a supervisor', async () => {
    const data: TeamDialogData = { name: 'Warehouse', save };
    TestBed.configureTestingModule({
      imports: [TeamDialogComponent],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: { close } },
      ],
    });
    const fixture = TestBed.createComponent(TeamDialogComponent);
    await fixture.whenStable();
    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelector('mat-select')).toBeNull();

    const input = element.querySelector<HTMLInputElement>('input')!;
    input.value = ' WAREHOUSE ';
    input.dispatchEvent(new Event('input'));
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    await new Promise((resolve) => setTimeout(resolve));

    expect(save).toHaveBeenCalledWith('WAREHOUSE', '');
    expect(close).toHaveBeenCalledWith(true);
  });
});
