import { Component, inject } from '@angular/core';
import { SessionService } from '../../../../core/services/session.service';

/** Where users without a clock land. ponytail: placeholder until the admin and supervisor pages. */
@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent {
  protected readonly user = inject(SessionService).user;
}
