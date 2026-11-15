import { AbstractControl, FormGroup } from '@angular/forms';
import { ApiError } from './api-error';

/** Puts server-side validation messages on the matching controls. Returns messages that matched no control. */
export function applyServerErrors(form: FormGroup, error: ApiError): string[] {
  const unmatched: string[] = [];
  for (const [field, messages] of Object.entries(error.fieldErrors)) {
    const control: AbstractControl | null = form.get(field);
    if (control) {
      control.setErrors({ ...control.errors, server: messages.join(' ') });
      control.markAsTouched();
    } else {
      unmatched.push(...messages);
    }
  }
  return unmatched;
}
