import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';
import {
  ChoiceDialogComponent,
  ChoiceDialogData,
} from '../../../../shared/components/choice-dialog/choice-dialog.component';
import {
  TextDialogComponent,
  TextDialogData,
} from '../../../../shared/components/text-dialog/text-dialog.component';
import {
  TeamDialogComponent,
  TeamDialogData,
} from '../../components/team-dialog/team-dialog.component';
import { TeamListItem } from '../../models/team-list-item.model';
import { UserListItem } from '../../models/user-list-item.model';
import { AdminService } from '../../services/admin.service';

/** Teams, active first (create-team, rename-team, change-team-supervisor, archive-team). */
@Component({
  selector: 'app-teams',
  imports: [MatButtonModule, MatIconModule, MatMenuModule, MatProgressSpinnerModule],
  templateUrl: './teams.component.html',
  styleUrl: './teams.component.scss',
})
export class TeamsComponent {
  private readonly admin = inject(AdminService);
  private readonly dialogs = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly teams = signal<TeamListItem[] | null>(null);
  protected readonly users = signal<UserListItem[]>([]);
  protected readonly loadFailed = signal(false);

  /** Active supervisors who lead no team. */
  protected readonly freeSupervisors = computed(() =>
    this.users().filter(
      (u) => u.role === 'Supervisor' && u.status !== 'Deactivated' && !u.ledTeamId,
    ),
  );

  constructor() {
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.loadFailed.set(false);
    try {
      const [teams, users] = await Promise.all([this.admin.teams(), this.admin.users()]);
      this.teams.set(teams);
      this.users.set(users);
    } catch {
      this.loadFailed.set(true);
    }
  }

  protected create(): void {
    this.openTeamDialog(
      {
        supervisors: this.freeSupervisors(),
        save: (name, supervisorId) => this.admin.createTeam(name, supervisorId),
      },
      $localize`:@@teamCreated:Team created.`,
    );
  }

  protected rename(team: TeamListItem): void {
    this.openTeamDialog(
      { name: team.name, save: (name) => this.admin.renameTeam(team.id, name) },
      $localize`:@@teamRenamed:Team renamed.`,
    );
  }

  protected async changeSupervisor(team: TeamListItem): Promise<void> {
    const data: ChoiceDialogData = {
      title: $localize`:@@changeSupervisorTitle:New supervisor for ${team.name}:name:`,
      message: $localize`:@@changeSupervisorMessage:Pending timesheets and correction requests move to the new supervisor.`,
      label: $localize`:@@supervisor:Supervisor`,
      confirm: $localize`:@@save:Save`,
      options: this.freeSupervisors().map((u) => ({ value: u.id, label: u.name })),
    };
    const supervisorId = await firstValueFrom(
      this.dialogs
        .open<ChoiceDialogComponent, ChoiceDialogData, string>(ChoiceDialogComponent, {
          data,
          width: '440px',
        })
        .afterClosed(),
    );
    if (supervisorId) {
      await this.act(
        () => this.admin.changeSupervisor(team.id, supervisorId),
        $localize`:@@supervisorChanged:Supervisor changed.`,
      );
    }
  }

  protected async archive(team: TeamListItem): Promise<void> {
    const data: TextDialogData = {
      title: $localize`:@@archiveTeamTitle:Archive ${team.name}:name:?`,
      message: $localize`:@@archiveTeamMessage:The team keeps its name in history and exports, but nobody can join it again.`,
      confirm: $localize`:@@archive:Archive`,
    };
    const confirmed = await firstValueFrom(
      this.dialogs
        .open<TextDialogComponent, TextDialogData, string>(TextDialogComponent, {
          data,
          width: '440px',
        })
        .afterClosed(),
    );
    if (confirmed !== undefined) {
      await this.act(
        () => this.admin.archiveTeam(team.id),
        $localize`:@@teamArchived:Team archived.`,
      );
    }
  }

  private openTeamDialog(data: TeamDialogData, done: string): void {
    this.dialogs
      .open<TeamDialogComponent, TeamDialogData, boolean>(TeamDialogComponent, {
        data,
        width: '440px',
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.snackBar.open(done, undefined, { duration: 4000 });
          void this.reload();
        }
      });
  }

  /** Runs an action; the error interceptor shows failures, and the list reloads either way. */
  private async act(action: () => Promise<void>, done: string): Promise<void> {
    try {
      await action();
      this.snackBar.open(done, undefined, { duration: 4000 });
    } catch {
      // Already shown.
    }

    await this.reload();
  }
}
