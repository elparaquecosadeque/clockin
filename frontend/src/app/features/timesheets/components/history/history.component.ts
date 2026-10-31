import { Component, input } from '@angular/core';
import { CompanyTimePipe } from '../../../../shared/pipes/company-time.pipe';
import { HistoryItem } from '../../models/history-item.model';

/** A timesheet's transitions and entry changes, oldest first, as the server lists them. */
@Component({
  selector: 'app-history',
  imports: [CompanyTimePipe],
  templateUrl: './history.component.html',
  styleUrl: './history.component.scss',
})
export class HistoryComponent {
  readonly items = input.required<HistoryItem[]>();
  readonly timeZone = input.required<string>();
}
