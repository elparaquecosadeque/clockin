import { DOCUMENT } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { AccessLink } from '../../models/access-link.model';

export interface LinkDialogData {
  name: string;
  link: AccessLink;
}

/**
 * Shows an activation or reset link once, for the admin to hand over (there is no email in v1).
 * The link opens the set-password page of the build the admin is using.
 */
@Component({
  selector: 'app-link-dialog',
  imports: [MatButtonModule, MatDialogModule, MatIconModule],
  templateUrl: './link-dialog.component.html',
  styleUrl: './link-dialog.component.scss',
})
export class LinkDialogComponent {
  protected readonly data = inject<LinkDialogData>(MAT_DIALOG_DATA);
  protected readonly url = new URL(
    `set-password?token=${encodeURIComponent(this.data.link.token)}`,
    inject(DOCUMENT).baseURI,
  ).href;
  protected readonly expires = new Date(this.data.link.expiresAt).toLocaleString();
  protected readonly copied = signal(false);

  protected async copy(): Promise<void> {
    await navigator.clipboard.writeText(this.url);
    this.copied.set(true);
  }
}
