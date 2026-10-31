import { Pipe, PipeTransform } from '@angular/core';
import { Role } from '../models/user-list-item.model';

/** A role in the build language. */
@Pipe({ name: 'roleLabel' })
export class RoleLabelPipe implements PipeTransform {
  transform(role: Role): string {
    switch (role) {
      case 'Employee':
        return $localize`:@@roleEmployee:Employee`;
      case 'Supervisor':
        return $localize`:@@roleSupervisor:Supervisor`;
      case 'Admin':
        return $localize`:@@roleAdmin:Admin`;
    }
  }
}
