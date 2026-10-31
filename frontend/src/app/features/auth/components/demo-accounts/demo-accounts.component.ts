import { Component, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { DemoAccount, DemoAccounts } from '../../models/demo-accounts.model';

/** The demo's sample accounts (ADR 0005). Picking one fills the sign-in form. */
@Component({
  selector: 'app-demo-accounts',
  imports: [MatIconModule],
  templateUrl: './demo-accounts.component.html',
  styleUrl: './demo-accounts.component.scss',
})
export class DemoAccountsComponent {
  readonly demo = input.required<DemoAccounts>();
  readonly picked = output<DemoAccount>();
}
