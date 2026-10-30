import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterOutlet } from '@angular/router';
import { SessionService } from '../../services/session.service';

/** The frame around every signed-in page. */
@Component({
  selector: 'app-shell',
  imports: [MatButtonModule, MatIconModule, MatMenuModule, MatToolbarModule, RouterOutlet],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);

  protected readonly user = this.session.user;

  protected async signOut(): Promise<void> {
    await this.session.signOut();
    await this.router.navigate(['/sign-in']);
  }
}
