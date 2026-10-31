import { Component, DestroyRef, computed, inject } from '@angular/core';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTabsModule } from '@angular/material/tabs';
import { MatToolbarModule } from '@angular/material/toolbar';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { Permissions } from '../../models/permissions.model';
import { SessionService } from '../../services/session.service';
import { ToDosService } from '../../services/to-dos.service';

interface NavLink {
  path: string;
  label: string;
  icon: string;
  badge: () => number;
}

/** The frame around every signed-in page: navigation by permission, with to-do badges. */
@Component({
  selector: 'app-shell',
  imports: [
    MatBadgeModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatTabsModule,
    MatToolbarModule,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly toDos = inject(ToDosService);

  protected readonly user = this.session.user;

  protected readonly links = computed<NavLink[]>(() => {
    // Reading the user makes the links follow the session.
    if (!this.user()) return [];
    const can = (...permissions: string[]) =>
      permissions.some((p) => this.session.hasPermission(p));
    const none = () => 0;
    const all: (NavLink & { show: boolean })[] = [
      {
        path: '/clock',
        label: $localize`:@@navClock:Clock`,
        icon: 'schedule',
        badge: none,
        show: can(Permissions.punchTime),
      },
      {
        path: '/timesheets',
        label: $localize`:@@navTimesheets:My timesheets`,
        icon: 'calendar_month',
        badge: this.toDos.ownCount,
        show: can(Permissions.punchTime),
      },
      {
        path: '/review',
        label: $localize`:@@navReview:Review`,
        icon: 'fact_check',
        badge: this.toDos.reviewCount,
        show: can(Permissions.reviewTeam, Permissions.reviewAll),
      },
      {
        path: '/team',
        label: $localize`:@@navTeam:Team week`,
        icon: 'groups',
        badge: none,
        show: can(Permissions.reviewTeam, Permissions.reviewAll),
      },
      {
        path: '/users',
        label: $localize`:@@navUsers:Users`,
        icon: 'manage_accounts',
        badge: none,
        show: can(Permissions.manageUsers),
      },
      {
        path: '/teams',
        label: $localize`:@@navTeams:Teams`,
        icon: 'workspaces',
        badge: none,
        show: can(Permissions.manageTeams),
      },
    ];
    return all.filter((link) => link.show);
  });

  constructor() {
    void this.toDos.load();
    const navigations = this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe(() => void this.toDos.load());
    inject(DestroyRef).onDestroy(() => navigations.unsubscribe());
  }

  protected async signOut(): Promise<void> {
    await this.session.signOut();
    await this.router.navigate(['/sign-in']);
  }
}
