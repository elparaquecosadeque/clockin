import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { SessionService } from '../../../../core/services/session.service';
import { DemoAccounts } from '../../models/demo-accounts.model';
import { AuthService } from '../../services/auth.service';
import { SignInComponent } from './sign-in.component';

describe('SignInComponent', () => {
  const signIn = vi.fn<(email: string, password: string) => Promise<void>>();
  const demoAccounts = vi.fn<() => Promise<DemoAccounts | null>>();
  let fixture: ComponentFixture<SignInComponent>;
  let element: HTMLElement;

  beforeEach(async () => {
    signIn.mockReset();
    demoAccounts.mockResolvedValue(null);
    TestBed.configureTestingModule({
      imports: [SignInComponent],
      providers: [
        provideRouter([]),
        { provide: SessionService, useValue: { signIn } },
        { provide: AuthService, useValue: { demoAccounts } },
      ],
    });
    fixture = TestBed.createComponent(SignInComponent);
    element = fixture.nativeElement;
    await fixture.whenStable();
  });

  function type(selector: string, value: string): void {
    const input = element.querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  async function submit(): Promise<void> {
    element.querySelector<HTMLButtonElement>('button[type=submit]')!.click();
    await fixture.whenStable();
  }

  it('signs in and goes to the start page', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    signIn.mockResolvedValue();
    type('input[type=email]', 'eve@clockin.test');
    type('input[type=password]', 'correct horse battery');

    await submit();

    expect(signIn).toHaveBeenCalledWith('eve@clockin.test', 'correct horse battery');
    expect(navigate).toHaveBeenCalledWith('/');
  });

  it('shows one generic message for bad credentials', async () => {
    signIn.mockRejectedValue({ status: 422, code: 'InvalidCredentials', fieldErrors: {} });
    type('input[type=email]', 'eve@clockin.test');
    type('input[type=password]', 'wrong');

    await submit();

    expect(element.querySelector('[role=alert]')?.textContent).toContain(
      'The email or password is incorrect.',
    );
  });

  it('does not call the API with an empty form', async () => {
    await submit();

    expect(signIn).not.toHaveBeenCalled();
    expect(element.textContent).toContain('Enter your email.');
  });

  it('explains an oversized email before calling the API', async () => {
    type(
      'input[type=email]',
      'a'.repeat(64) + '@' + 'b'.repeat(63) + '.' + 'c'.repeat(63) + '.' + 'd'.repeat(62),
    );
    type('input[type=password]', 'correct horse battery');

    await submit();

    expect(signIn).not.toHaveBeenCalled();
    expect(element.textContent).toContain('Use 254 characters or fewer for your email.');
  });

  it('accepts an email at the 254-character boundary', async () => {
    vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    signIn.mockResolvedValue();
    const email =
      'a'.repeat(64) + '@' + 'b'.repeat(63) + '.' + 'c'.repeat(63) + '.' + 'd'.repeat(61);
    type('input[type=email]', email);
    type('input[type=password]', 'correct horse battery');

    await submit();

    expect(signIn).toHaveBeenCalledWith(email, 'correct horse battery');
  });

  it.each(['email', 'password'])(
    'shows a server validation error beside the %s field and allows correction',
    async (field) => {
      signIn.mockRejectedValue({ status: 400, fieldErrors: { [field]: ['Check this value.'] } });
      type('input[type=email]', 'eve@clockin.test');
      type('input[type=password]', 'correct horse battery');

      await submit();

      const input = element.querySelector<HTMLInputElement>(`input[type=${field}]`)!;
      expect(input.closest('mat-form-field')!.textContent).toContain('Check this value.');

      type(`input[type=${field}]`, field === 'email' ? 'other@clockin.test' : 'another passphrase');
      signIn.mockRejectedValue({ status: 422, code: 'InvalidCredentials', fieldErrors: {} });
      await submit();

      expect(signIn).toHaveBeenCalledTimes(2);
      expect(input.closest('mat-form-field')!.textContent).not.toContain('Check this value.');
    },
  );

  it('fills the form with a demo account in a demo', async () => {
    demoAccounts.mockResolvedValue({
      password: 'clockin-demo',
      accounts: [
        {
          email: 'eve@clockin.demo',
          name: 'Eve',
          role: 'Employee',
          team: 'Warehouse',
          scenario: 'ClockedIn',
        },
      ],
    });
    fixture = TestBed.createComponent(SignInComponent);
    element = fixture.nativeElement;
    await fixture.whenStable();

    element.querySelector<HTMLButtonElement>('app-demo-accounts button')!.click();
    await fixture.whenStable();

    expect(element.querySelector<HTMLInputElement>('input[type=email]')!.value).toBe(
      'eve@clockin.demo',
    );
    expect(element.querySelector<HTMLInputElement>('input[type=password]')!.value).toBe(
      'clockin-demo',
    );
  });

  it('shows no demo accounts outside a demo', () => {
    expect(element.querySelector('app-demo-accounts')).toBeNull();
  });
});
