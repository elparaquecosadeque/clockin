import { Pipe, PipeTransform } from '@angular/core';

/** Seconds as H:MM, or H:MM:SS with `withSeconds`. Hours are not wrapped at 24. */
@Pipe({ name: 'duration' })
export class DurationPipe implements PipeTransform {
  transform(totalSeconds: number, withSeconds = false): string {
    const seconds = Math.max(0, Math.floor(totalSeconds));
    const hours = Math.floor(seconds / 3600);
    const minutes = pad(Math.floor((seconds % 3600) / 60));
    return withSeconds ? `${hours}:${minutes}:${pad(seconds % 60)}` : `${hours}:${minutes}`;
  }
}

function pad(value: number): string {
  return value.toString().padStart(2, '0');
}
