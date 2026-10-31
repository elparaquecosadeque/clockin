import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { LinkDialogComponent } from './link-dialog.component';

describe('LinkDialogComponent', () => {
  it('shows the set-password link of this build and copies it', async () => {
    const writeText = vi.fn(async () => undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    TestBed.configureTestingModule({
      imports: [LinkDialogComponent],
      providers: [
        {
          provide: MAT_DIALOG_DATA,
          useValue: { name: 'Fay', link: { token: 'abc.def', expiresAt: '2026-11-02T15:00:00Z' } },
        },
      ],
    });
    const fixture = TestBed.createComponent(LinkDialogComponent);
    await fixture.whenStable();
    const element: HTMLElement = fixture.nativeElement;
    const url = element.querySelector('code')!.textContent!;

    expect(url).toBe(new URL('set-password?token=abc.def', document.baseURI).href);

    element.querySelector<HTMLButtonElement>('button')!.click();
    await fixture.whenStable();
    expect(writeText).toHaveBeenCalledWith(url);
    expect(element.textContent).toContain('Copied');
  });
});
