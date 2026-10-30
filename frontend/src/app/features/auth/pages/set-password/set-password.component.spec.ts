import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { SetPasswordComponent } from './set-password.component';

describe('SetPasswordComponent', () => {
  const setPassword = vi.fn<(token: string, password: string) => Promise<void>>();
  let fixture: ComponentFixture<SetPasswordComponent>;
  let element: HTMLElement;

  async function open(token?: string): Promise<void> {
    setPassword.mockReset();
    TestBed.configureTestingModule({
      imports: [SetPasswordComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: { setPassword } }],
    });
    fixture = TestBed.createComponent(SetPasswordComponent);
    if (token) fixture.componentRef.setInput('token', token);
    element = fixture.nativeElement;
    await fixture.whenStable();
  }

  async function submit(password: string): Promise<void> {
    const input = element.querySelector<HTMLInputElement>('input[type=password]')!;
    input.value = password;
    input.dispatchEvent(new Event('input'));
    element.querySelector<HTMLButtonElement>('button[type=submit]')!.click();
    await fixture.whenStable();
  }

  it('sets the password and sends the user to sign in', async () => {
    await open('abc.def');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    setPassword.mockResolvedValue();

    await submit('correct horse battery');

    expect(setPassword).toHaveBeenCalledWith('abc.def', 'correct horse battery');
    expect(navigate).toHaveBeenCalledWith(['/sign-in'], { queryParams: { passwordSet: 1 } });
  });

  it('checks the length before calling the API', async () => {
    await open('abc.def');

    await submit('eleven char');

    expect(setPassword).not.toHaveBeenCalled();
    expect(element.textContent).toContain('Use 12 to 128 characters.');
  });

  it('explains an unusable link without revealing why', async () => {
    await open('abc.def');
    setPassword.mockRejectedValue({ status: 422, code: 'InvalidOrExpiredLink', fieldErrors: {} });

    await submit('correct horse battery');

    expect(element.querySelector('h1')?.textContent).toContain("This link doesn't work");
  });

  it('explains a link without a token', async () => {
    await open();

    expect(element.querySelector('h1')?.textContent).toContain("This link doesn't work");
  });
});
