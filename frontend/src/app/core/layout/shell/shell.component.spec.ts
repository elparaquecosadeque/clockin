import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { SessionUser } from '../../models/session-user.model';
import { SessionService } from '../../services/session.service';
import { ShellComponent } from './shell.component';

describe('ShellComponent', () => {
  const signOut = vi.fn(async () => undefined);
  const user = signal<SessionUser | null>({
    id: 'u1',
    name: 'Eve',
    email: 'eve@clockin.test',
    role: 'Employee',
    permissions: [],
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [provideRouter([]), { provide: SessionService, useValue: { user, signOut } }],
    });
  });

  it('shows the product name and signs out from the account menu', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(ShellComponent);
    const element: HTMLElement = fixture.nativeElement;
    await fixture.whenStable();

    expect(element.querySelector('mat-toolbar')?.textContent).toContain('ClockIn');

    element.querySelector<HTMLButtonElement>('button.account')!.click();
    await fixture.whenStable();
    const menu = document.querySelector('.mat-mdc-menu-panel')!;
    expect(menu.textContent).toContain('Eve');
    menu.querySelector<HTMLButtonElement>('button[mat-menu-item]')!.click();
    await fixture.whenStable();

    expect(signOut).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/sign-in']);
  });
});
