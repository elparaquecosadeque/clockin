import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AccessLink } from '../models/access-link.model';
import { NewUser } from '../models/new-user.model';
import { TeamListItem } from '../models/team-list-item.model';
import { Role, UserListItem } from '../models/user-list-item.model';

/** User and team administration. Admins only; the API enforces it. */
@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);

  users(): Promise<UserListItem[]> {
    return firstValueFrom(this.http.get<UserListItem[]>('/api/users'));
  }

  createUser(user: NewUser): Promise<AccessLink> {
    return firstValueFrom(this.http.post<AccessLink>('/api/users', user));
  }

  accessLink(userId: string): Promise<AccessLink> {
    return firstValueFrom(this.http.post<AccessLink>(`/api/users/${userId}/access-links`, null));
  }

  changeRole(userId: string, role: Role, teamId: string | null): Promise<void> {
    return this.send('PUT', `/api/users/${userId}/role`, { role, teamId });
  }

  deactivate(userId: string): Promise<void> {
    return this.send('POST', `/api/users/${userId}/deactivate`);
  }

  reactivate(userId: string, teamId: string | null): Promise<AccessLink> {
    return firstValueFrom(
      this.http.post<AccessLink>(`/api/users/${userId}/reactivate`, { teamId }),
    );
  }

  unlock(userId: string): Promise<void> {
    return this.send('POST', `/api/users/${userId}/unlock`);
  }

  teams(): Promise<TeamListItem[]> {
    return firstValueFrom(this.http.get<TeamListItem[]>('/api/teams'));
  }

  createTeam(name: string, supervisorId: string): Promise<void> {
    return this.send('POST', '/api/teams', { name, supervisorId });
  }

  renameTeam(teamId: string, name: string): Promise<void> {
    return this.send('PUT', `/api/teams/${teamId}/name`, { name });
  }

  changeSupervisor(teamId: string, supervisorId: string): Promise<void> {
    return this.send('PUT', `/api/teams/${teamId}/supervisor`, { supervisorId });
  }

  archiveTeam(teamId: string): Promise<void> {
    return this.send('POST', `/api/teams/${teamId}/archive`);
  }

  moveEmployee(userId: string, teamId: string): Promise<void> {
    return this.send('PUT', `/api/teams/${teamId}/employees/${userId}`);
  }

  private async send(method: 'POST' | 'PUT', url: string, body: unknown = null): Promise<void> {
    await firstValueFrom(this.http.request(method, url, { body }));
  }
}
