import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';
import { SessionService } from '../../../../core/services/session.service';
import {
  ChoiceDialogComponent,
  ChoiceDialogData,
} from '../../../../shared/components/choice-dialog/choice-dialog.component';
import {
  TextDialogComponent,
  TextDialogData,
} from '../../../../shared/components/text-dialog/text-dialog.component';
import {
  LinkDialogComponent,
  LinkDialogData,
} from '../../components/link-dialog/link-dialog.component';
import {
  UserDialogComponent,
  UserDialogData,
} from '../../components/user-dialog/user-dialog.component';
import { AccessLink } from '../../models/access-link.model';
import { TeamListItem } from '../../models/team-list-item.model';
import { Role, UserListItem } from '../../models/user-list-item.model';
import { AdminService } from '../../services/admin.service';
import { RoleLabelPipe } from '../../pipes/role-label.pipe';

/** Everyone, with what an admin can do to each (create-user, change-role, deactivate, reactivate, unlock). */
@Component({
  selector: 'app-users',
  imports: [MatButtonModule, MatIconModule, MatMenuModule, MatProgressSpinnerModule, RoleLabelPipe],
  templateUrl: './users.component.html',
  styleUrl: './users.component.scss',
})
export class UsersComponent {
  private readonly admin = inject(AdminService);
  private readonly dialogs = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly myId = inject(SessionService).user()?.id;
  protected readonly users = signal<UserListItem[] | null>(null);
  protected readonly teams = signal<TeamListItem[]>([]);
  protected readonly loadFailed = signal(false);
  protected readonly activeTeams = computed(() => this.teams().filter((t) => !t.isArchived));

  constructor() {
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.loadFailed.set(false);
    try {
      const [users, teams] = await Promise.all([this.admin.users(), this.admin.teams()]);
      this.users.set(users);
      this.teams.set(teams);
    } catch {
      this.loadFailed.set(true);
    }
  }

  protected create(): void {
    const data: UserDialogData = {
      teams: this.activeTeams(),
      save: (user) => this.admin.createUser(user),
    };
    this.dialogs
      .open<UserDialogComponent, UserDialogData, AccessLink>(UserDialogComponent, {
        data,
        width: '520px',
      })
      .afterClosed()
      .subscribe((link) => {
        if (link) {
          void this.reload();
          this.showLink($localize`:@@theNewUser:the new user`, link);
        }
      });
  }

  protected async accessLink(user: UserListItem): Promise<void> {
    await this.act(async () => this.showLink(user.name, await this.admin.accessLink(user.id)));
  }

  protected async changeRole(user: UserListItem): Promise<void> {
    const role = (await this.choose({
      title: $localize`:@@changeRoleTitle:Change the role of ${user.name}:name:`,
      message: $localize`:@@changeRoleMessage:Their sessions end, so they sign in again with the new permissions.`,
      label: $localize`:@@role:Role`,
      confirm: $localize`:@@next:Next`,
      options: [
        { value: 'Employee', label: $localize`:@@roleEmployee:Employee` },
        { value: 'Supervisor', label: $localize`:@@roleSupervisor:Supervisor` },
      ].filter((option) => option.value !== user.role),
    })) as Role | undefined;
    if (!role) return;
    const teamId = role === 'Employee' ? ((await this.chooseTeam(user, null)) ?? null) : null;
    if (role === 'Employee' && !teamId) return;
    await this.act(
      () => this.admin.changeRole(user.id, role, teamId),
      $localize`:@@roleChanged:Role changed.`,
    );
  }

  protected async move(user: UserListItem): Promise<void> {
    const teamId = await this.chooseTeam(user, user.teamId);
    if (teamId && teamId !== user.teamId) {
      await this.act(
        () => this.admin.moveEmployee(user.id, teamId),
        $localize`:@@employeeMoved:Employee moved.`,
      );
    }
  }

  protected async unlock(user: UserListItem): Promise<void> {
    await this.act(
      () => this.admin.unlock(user.id),
      $localize`:@@userUnlocked:${user.name}:name: can sign in again.`,
    );
  }

  protected async deactivate(user: UserListItem): Promise<void> {
    const confirmed = await this.confirm({
      title: $localize`:@@deactivateTitle:Deactivate ${user.name}:name:?`,
      message: $localize`:@@deactivateMessage:They are signed out everywhere at once. Their records stay, and you can reactivate them later.`,
      confirm: $localize`:@@deactivate:Deactivate`,
    });
    if (confirmed) {
      await this.act(
        () => this.admin.deactivate(user.id),
        $localize`:@@userDeactivated:User deactivated.`,
      );
    }
  }

  protected async reactivate(user: UserListItem): Promise<void> {
    let teamId: string | null = null;
    if (user.role === 'Employee') {
      const current = this.activeTeams().some((t) => t.id === user.teamId) ? user.teamId : null;
      teamId = (await this.chooseTeam(user, current)) ?? null;
      if (!teamId) return;
    } else if (
      !(await this.confirm({
        title: $localize`:@@reactivateTitle:Reactivate ${user.name}:name:?`,
        message: $localize`:@@reactivateMessage:They get a new link to set a password; the old one no longer works.`,
        confirm: $localize`:@@reactivate:Reactivate`,
      }))
    ) {
      return;
    }

    await this.act(async () =>
      this.showLink(user.name, await this.admin.reactivate(user.id, teamId)),
    );
  }

  private chooseTeam(user: UserListItem, current: string | null): Promise<string | undefined> {
    return this.choose({
      title: $localize`:@@chooseTeamTitle:Team for ${user.name}:name:`,
      label: $localize`:@@team:Team`,
      confirm: $localize`:@@save:Save`,
      options: this.activeTeams().map((t) => ({ value: t.id, label: t.name })),
      value: current ?? undefined,
    });
  }

  private choose(data: ChoiceDialogData): Promise<string | undefined> {
    return firstValueFrom(
      this.dialogs
        .open<ChoiceDialogComponent, ChoiceDialogData, string>(ChoiceDialogComponent, {
          data,
          width: '440px',
        })
        .afterClosed(),
    );
  }

  private async confirm(data: TextDialogData): Promise<boolean> {
    const result = await firstValueFrom(
      this.dialogs
        .open<TextDialogComponent, TextDialogData, string>(TextDialogComponent, {
          data,
          width: '440px',
        })
        .afterClosed(),
    );
    return result !== undefined;
  }

  private showLink(name: string, link: AccessLink): void {
    this.dialogs.open<LinkDialogComponent, LinkDialogData>(LinkDialogComponent, {
      data: { name, link },
      width: '520px',
    });
  }

  /** Runs an action; the error interceptor shows failures, and the list reloads either way. */
  private async act(action: () => Promise<void>, done?: string): Promise<void> {
    try {
      await action();
      if (done) this.snackBar.open(done, undefined, { duration: 4000 });
    } catch {
      // Already shown.
    }

    await this.reload();
  }
}
