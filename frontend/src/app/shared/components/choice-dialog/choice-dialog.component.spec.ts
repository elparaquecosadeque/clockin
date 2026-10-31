import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { ChoiceDialogComponent, ChoiceDialogData } from './choice-dialog.component';

describe('ChoiceDialogComponent', () => {
  const close = vi.fn();

  async function render(value?: string): Promise<HTMLElement> {
    const data: ChoiceDialogData = {
      title: 'Move to team',
      label: 'Team',
      confirm: 'Move',
      options: [
        { value: 't1', label: 'Warehouse' },
        { value: 't2', label: 'Office' },
      ],
      value,
    };
    TestBed.configureTestingModule({
      imports: [ChoiceDialogComponent],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: { close } },
      ],
    });
    const fixture = TestBed.createComponent(ChoiceDialogComponent);
    await fixture.whenStable();
    return fixture.nativeElement;
  }

  beforeEach(() => close.mockClear());

  it('closes with the chosen value', async () => {
    const element = await render('t2');
    element.querySelector('form')!.dispatchEvent(new Event('submit'));

    expect(close).toHaveBeenCalledWith('t2');
  });

  it('needs a choice', async () => {
    const element = await render();
    element.querySelector('form')!.dispatchEvent(new Event('submit'));

    expect(close).not.toHaveBeenCalled();
  });
});
