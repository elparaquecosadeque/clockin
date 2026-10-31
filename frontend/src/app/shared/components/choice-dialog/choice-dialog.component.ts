import { Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';

export interface ChoiceDialogData {
  title: string;
  message?: string;
  label: string;
  confirm: string;
  options: { value: string; label: string }[];
  /** Preselected value. */
  value?: string;
}

/** Picks one of a few options. Closes with the value, or undefined when cancelled. */
@Component({
  selector: 'app-choice-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatSelectModule,
    ReactiveFormsModule,
  ],
  templateUrl: './choice-dialog.component.html',
  styleUrl: './choice-dialog.component.scss',
})
export class ChoiceDialogComponent {
  protected readonly data = inject<ChoiceDialogData>(MAT_DIALOG_DATA);
  private readonly dialog = inject<MatDialogRef<ChoiceDialogComponent, string>>(MatDialogRef);

  protected readonly choice = new FormControl(this.data.value ?? '', {
    nonNullable: true,
    validators: Validators.required,
  });

  protected submit(event: Event): void {
    event.preventDefault();
    if (this.choice.invalid) {
      this.choice.markAsTouched();
      return;
    }

    this.dialog.close(this.choice.value);
  }
}
