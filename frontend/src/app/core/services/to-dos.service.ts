import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SILENT_ERRORS } from '../models/silent-errors.model';
import { ToDos } from '../models/to-dos.model';

/** The badges in the navigation, derived by the server from stored state (to-do-badges.feature). */
@Injectable({ providedIn: 'root' })
export class ToDosService {
  private readonly http = inject(HttpClient);
  private readonly current = signal<ToDos | null>(null);

  readonly toDos = this.current.asReadonly();

  /** Returned and unsubmitted weeks, and recently declined requests. */
  readonly ownCount = computed(() => {
    const toDos = this.current();
    return toDos
      ? toDos.returnedWeeks.length + toDos.unsubmittedWeeks.length + toDos.declinedCorrections
      : 0;
  });

  readonly reviewCount = computed(() => {
    const toDos = this.current();
    return toDos ? toDos.timesheetsToReview + toDos.correctionsToDecide : 0;
  });

  /** Badges are a convenience: a failed load keeps the last counts and stays quiet. */
  async load(): Promise<void> {
    try {
      const context = new HttpContext().set(SILENT_ERRORS, ['*']);
      this.current.set(await firstValueFrom(this.http.get<ToDos>('/api/me/to-dos', { context })));
    } catch {
      // Keep the last counts.
    }
  }
}
