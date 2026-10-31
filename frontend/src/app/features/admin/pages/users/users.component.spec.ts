import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import { SessionService } from '../../../../core/services/session.service';
import { UserListItem } from '../../models/user-list-item.model';
import { AdminService } from '../../services/admin.service';
import { UsersComponent } from './users.component';

function user(id: string, overrides: Partial<UserListItem> = {}): UserListItem {
  return {
    id,
    name: id,
    email: `${id.toLowerCase()}@clockin.test`,
    role: 'Employee',
    status: 'Active',
    teamId: 'w',
    teamName: 'Warehouse',
    ledTeamId: null,
    isLockedOut: false,
    ...overrides,
  };
}

describe('UsersComponent', () => {
  const admin = {
    users: vi.fn(async () => [
      user('Ada', { role: 'Admin', teamId: null, teamName: null }),
      user('Eve', { isLockedOut: true }),
      user('Ed', { status: 'Deactivated' }),
    ]),
    teams: vi.fn(async () => [
      {
        id: 'w',
        name: 'Warehouse',
        supervisorId: 's',
        supervisorName: 'Sam',
        isArchived: false,
        employeeCount: 2,
      },
    ]),
    unlock: vi.fn(async () => undefined),
    reactivate: vi.fn(async () => ({ token: 'x.y', expiresAt: '2026-11-02T15:00:00Z' })),
  };
  const open = vi.fn();
  let fixture: ComponentFixture<UsersComponent>;

  beforeEach(async () => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      imports: [UsersComponent],
      providers: [
        { provide: AdminService, useValue: admin },
        { provide: SessionService, useValue: { user: () => ({ id: 'Ada' }) } },
        { provide: MatDialog, useValue: { open } },
      ],
    });
    fixture = TestBed.createComponent(UsersComponent);
    await fixture.whenStable();
  });

  async function openMenu(name: string): Promise<HTMLButtonElement[]> {
    const row = [...(fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr')].find(
      (tr) => tr.querySelector('.name')!.textContent!.includes(name),
    )!;
    row.querySelector<HTMLButtonElement>('.menu button')!.click();
    await fixture.whenStable();
    // A closed menu's panel can linger while it animates out; the newest one is this row's.
    const panels = document.querySelectorAll('.mat-mdc-menu-panel');
    return [...panels[panels.length - 1].querySelectorAll<HTMLButtonElement>('button')];
  }

  async function menuOf(name: string): Promise<string[]> {
    const items = (await openMenu(name)).map((b) => b.textContent!.trim());
    document.querySelector<HTMLElement>('.cdk-overlay-backdrop')?.click();
    await fixture.whenStable();
    return items;
  }

  it('shows role, team and status, flagging locked users', () => {
    const eve = [...(fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr')][1]
      .textContent!;
    expect(eve).toContain('Employee');
    expect(eve).toContain('Warehouse');
    expect(eve).toContain('Active');
    expect(eve).toContain('Locked');
  });

  it('offers what applies to each user, and never deactivating oneself', async () => {
    expect(await menuOf('Ada')).toEqual(['Activation or reset link']);
    expect(await menuOf('Eve')).toEqual([
      'Activation or reset link',
      'Change role',
      'Move to another team',
      'Unlock',
      'Deactivate',
    ]);
    expect(await menuOf('Ed')).toEqual(['Reactivate']);
  });

  it('reactivates an employee into the chosen team and shows the new link', async () => {
    open.mockReturnValueOnce({ afterClosed: () => of('w') }).mockReturnValueOnce({});
    (await openMenu('Ed'))[0].click();
    await fixture.whenStable();

    expect(admin.reactivate).toHaveBeenCalledWith('Ed', 'w');
    expect(open).toHaveBeenCalledTimes(2);
  });
});
