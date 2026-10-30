import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { SessionService } from '../../../../core/services/session.service';
import { SignInComponent } from './sign-in.component';

describe('SignInComponent', () => {
  const signIn = vi.fn<(email: string, password: string) => Promise<void>>();
  let fixture: ComponentFixture<SignInComponent>;
  let element: HTMLElement;

  beforeEach(async () => {
    signIn.mockReset();
    TestBed.configureTestingModule({
      imports: [SignInComponent],
      providers: [provideRouter([]), { provide: SessionService, useValue: { signIn } }],
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
});
