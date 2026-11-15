import { describe, expect, it } from 'vitest';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom, of } from 'rxjs';
import { confirmAction } from './confirm-dialog';

/** A dialog stub that closes with the given result. */
function dialogClosingWith(result: unknown): MatDialog {
  return { open: () => ({ afterClosed: () => of(result) }) } as unknown as MatDialog;
}

const data = { title: 'Delete?', message: 'Gone for good.', confirmLabel: 'Delete' };

describe('confirmAction', () => {
  it('emits true only when the user confirms', async () => {
    expect(await firstValueFrom(confirmAction(dialogClosingWith(true), data))).toBe(true);
  });

  it('treats Cancel, Escape and a backdrop click as no', async () => {
    expect(await firstValueFrom(confirmAction(dialogClosingWith(false), data))).toBe(false);
    expect(await firstValueFrom(confirmAction(dialogClosingWith(undefined), data))).toBe(false);
  });
});
