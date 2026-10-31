import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { SessionService } from '../../../../core/services/session.service';
import { SettingsComponent } from './settings.component';

describe('SettingsComponent', () => {
  const session = {
    user: () => ({ language: 'en', timeFormat: '24h' }),
    updatePreferences: vi.fn(async () => undefined),
    changePassword: vi.fn(async () => undefined),
  };
  let fixture: ComponentFixture<SettingsComponent>;

  beforeEach(async () => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      imports: [SettingsComponent],
      providers: [
        { provide: SessionService, useValue: session },
        { provide: MatSnackBar, useValue: { open: vi.fn() } },
      ],
    });
    fixture = TestBed.createComponent(SettingsComponent);
    await fixture.whenStable();
  });

  function fill(name: string, value: string): void {
    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      `[formControlName="${name}"]`,
    )!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('blur'));
  }

  async function submitPassword(): Promise<void> {
    (fixture.nativeElement as HTMLElement)
      .querySelectorAll('form')[1]
      .dispatchEvent(new Event('submit'));
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  }

  it('changes the password when the new one is confirmed', async () => {
    fill('currentPassword', 'old password!!');
    fill('newPassword', 'a new long password');
    fill('confirmation', 'a new long password');
    await submitPassword();

    expect(session.changePassword).toHaveBeenCalledWith('old password!!', 'a new long password');
  });

  it('stops a mismatched confirmation', async () => {
    fill('currentPassword', 'old password!!');
    fill('newPassword', 'a new long password');
    fill('confirmation', 'another password');
    await submitPassword();

    expect(session.changePassword).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      "The passwords don't match.",
    );
  });

  it('marks a wrong current password on its field', async () => {
    session.changePassword.mockRejectedValueOnce({
      status: 422,
      code: 'InvalidCurrentPassword',
      fieldErrors: {},
    });
    fill('currentPassword', 'wrong');
    fill('newPassword', 'a new long password');
    fill('confirmation', 'a new long password');
    await submitPassword();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'The current password is incorrect.',
    );
  });
});
