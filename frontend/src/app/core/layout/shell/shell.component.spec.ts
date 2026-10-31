import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { SessionUser } from '../../models/session-user.model';
import { SessionService } from '../../services/session.service';
import { ToDosService } from '../../services/to-dos.service';
import { ShellComponent } from './shell.component';

describe('ShellComponent', () => {
  const signOut = vi.fn(async () => undefined);
  const user = signal<SessionUser | null>(null);
  const toDos = { load: vi.fn(async () => undefined), ownCount: signal(2), reviewCount: signal(0) };

  function signIn(permissions: string[]): void {
    user.set({
      id: 'u1',
      name: 'Sam',
      email: 'sam@clockin.test',
      role: 'Supervisor',
      permissions,
      language: 'en',
      timeFormat: '24h',
    });
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideRouter([]),
        {
          provide: SessionService,
          useValue: {
            user,
            signOut,
            hasPermission: (p: string) => !!user()?.permissions.includes(p),
          },
        },
        { provide: ToDosService, useValue: toDos },
      ],
    });
  });

  it('shows the product name and signs out from the account menu', async () => {
    signIn([]);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(ShellComponent);
    const element: HTMLElement = fixture.nativeElement;
    await fixture.whenStable();

    expect(element.querySelector('mat-toolbar')?.textContent).toContain('ClockIn');

    element.querySelector<HTMLButtonElement>('button.account')!.click();
    await fixture.whenStable();
    const menu = document.querySelector('.mat-mdc-menu-panel')!;
    expect(menu.textContent).toContain('Sam');
    menu.querySelector<HTMLButtonElement>('button.sign-out')!.click();
    await fixture.whenStable();

    expect(signOut).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/sign-in']);
  });

  it('shows the pages the permissions allow, with to-do badges', async () => {
    signIn(['time-entries.punch', 'timesheets.review-team']);
    const fixture = TestBed.createComponent(ShellComponent);
    await fixture.whenStable();

    const links = [...(fixture.nativeElement as HTMLElement).querySelectorAll('nav a')];
    expect(links.map((a) => a.querySelector('.label')!.firstChild!.textContent!.trim())).toEqual([
      'Clock',
      'My timesheets',
      'Review',
      'Team week',
    ]);
    expect(links[1].querySelector('.mat-badge-content')?.textContent).toBe('2');
    expect(links[2].querySelector('.mat-badge-content')).toBeNull();
    expect(toDos.load).toHaveBeenCalled();
  });
});
