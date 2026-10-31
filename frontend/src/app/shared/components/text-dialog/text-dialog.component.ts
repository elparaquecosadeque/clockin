import { Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface TextDialogData {
  title: string;
  /** Shown above the field, e.g. what happens on confirm. */
  message?: string;
  /** Without a label the dialog only confirms, and closes with ''. */
  label?: string;
  confirm: string;
  required?: boolean;
  maxLength?: number;
}

/** Asks for a comment, reason or note, or just confirms. Closes with the trimmed text, or undefined when cancelled. */
@Component({
  selector: 'app-text-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    ReactiveFormsModule,
  ],
  templateUrl: './text-dialog.component.html',
  styleUrl: './text-dialog.component.scss',
})
export class TextDialogComponent {
  protected readonly data = inject<TextDialogData>(MAT_DIALOG_DATA);
  private readonly dialog = inject<MatDialogRef<TextDialogComponent, string>>(MatDialogRef);

  protected readonly text = new FormControl('', {
    nonNullable: true,
    validators: [
      ...(this.data.required ? [Validators.required, Validators.pattern(/\S/)] : []),
      Validators.maxLength(this.data.maxLength ?? 1000),
    ],
  });

  protected submit(event: Event): void {
    event.preventDefault();
    if (this.text.invalid) {
      this.text.markAsTouched();
      return;
    }

    this.dialog.close(this.text.value.trim());
  }
}
