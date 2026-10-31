import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TextDialogComponent, TextDialogData } from './text-dialog.component';

describe('TextDialogComponent', () => {
  const close = vi.fn();

  async function render(data: Partial<TextDialogData>): Promise<HTMLElement> {
    TestBed.configureTestingModule({
      imports: [TextDialogComponent],
      providers: [
        {
          provide: MAT_DIALOG_DATA,
          useValue: {
            title: 'Return',
            label: 'Comment',
            confirm: 'Return',
            required: true,
            maxLength: 10,
            ...data,
          },
        },
        { provide: MatDialogRef, useValue: { close } },
      ],
    });
    const fixture = TestBed.createComponent(TextDialogComponent);
    await fixture.whenStable();
    return fixture.nativeElement;
  }

  function type(element: HTMLElement, value: string): void {
    const textarea = element.querySelector('textarea')!;
    textarea.value = value;
    textarea.dispatchEvent(new Event('input'));
  }

  beforeEach(() => close.mockClear());

  it('closes with the trimmed text', async () => {
    const element = await render({});
    type(element, '  short ');
    element.querySelector('form')!.dispatchEvent(new Event('submit'));

    expect(close).toHaveBeenCalledWith('short');
  });

  it('keeps a required dialog open while the text is blank or too long', async () => {
    const element = await render({});
    type(element, '   ');
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    type(element, 'more than ten');
    element.querySelector('form')!.dispatchEvent(new Event('submit'));

    expect(close).not.toHaveBeenCalled();
  });

  it('accepts an empty optional text', async () => {
    const element = await render({ required: false });
    element.querySelector('form')!.dispatchEvent(new Event('submit'));

    expect(close).toHaveBeenCalledWith('');
  });
});
