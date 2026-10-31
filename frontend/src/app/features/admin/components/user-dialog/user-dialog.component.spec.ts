import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatSelectHarness } from '@angular/material/select/testing';
import { UserDialogComponent, UserDialogData } from './user-dialog.component';

describe('UserDialogComponent', () => {
  const close = vi.fn();
  const save = vi.fn(async () => ({ token: 'x.y', expiresAt: '' }));
  let fixture: ComponentFixture<UserDialogComponent>;

  beforeEach(async () => {
    vi.clearAllMocks();
    const data: UserDialogData = {
      teams: [
        {
          id: 'w',
          name: 'Warehouse',
          supervisorId: 's',
          supervisorName: 'Sam',
          isArchived: false,
          employeeCount: 1,
        },
      ],
      save,
    };
    TestBed.configureTestingModule({
      imports: [UserDialogComponent],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: { close } },
      ],
    });
    fixture = TestBed.createComponent(UserDialogComponent);
    await fixture.whenStable();
  });

  function fill(name: string, value: string): void {
    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      `[formControlName="${name}"]`,
    )!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  async function submit(): Promise<void> {
    (fixture.nativeElement as HTMLElement)
      .querySelector('form')!
      .dispatchEvent(new Event('submit'));
    await new Promise((resolve) => setTimeout(resolve));
  }

  async function choose(label: string, option: string): Promise<void> {
    const select = await TestbedHarnessEnvironment.loader(fixture).getHarness(
      MatSelectHarness.with({ selector: `[formControlName="${label}"]` }),
    );
    await select.open();
    await select.clickOptions({ text: option });
  }

  it('needs a team for an employee', async () => {
    fill('name', 'Fay');
    fill('email', 'fay@clockin.test');
    await submit();
    expect(save).not.toHaveBeenCalled();

    await choose('teamId', 'Warehouse');
    await submit();
    expect(save).toHaveBeenCalledWith(
      expect.objectContaining({ role: 'Employee', teamId: 'w', language: 'en' }),
    );
    expect(close).toHaveBeenCalledWith({ token: 'x.y', expiresAt: '' });
  });

  it('sends no team for a supervisor', async () => {
    fill('name', 'Sid');
    fill('email', 'sid@clockin.test');
    await choose('role', 'Supervisor');
    await submit();

    expect(save).toHaveBeenCalledWith(
      expect.objectContaining({ role: 'Supervisor', teamId: null }),
    );
  });
});
