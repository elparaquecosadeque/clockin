import { Pipe, PipeTransform } from '@angular/core';
import { WeekStatus } from '../models/week-status.model';

/** A week status in the build language. */
@Pipe({ name: 'statusLabel' })
export class StatusLabelPipe implements PipeTransform {
  transform(status: WeekStatus): string {
    switch (status) {
      case 'NotStarted':
        return $localize`:@@statusNotStarted:Not started`;
      case 'Draft':
        return $localize`:@@statusDraft:Draft`;
      case 'Submitted':
        return $localize`:@@statusSubmitted:Submitted`;
      case 'Approved':
        return $localize`:@@statusApproved:Approved`;
    }
  }
}
