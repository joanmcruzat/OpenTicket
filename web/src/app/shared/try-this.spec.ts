import { describe, expect, it } from 'vitest';
import { USER_ROLES } from '@app/core/api/models';
import { DEMO_ROLES } from '@app/core/auth/demo-roles';
import { TRY_THIS } from './try-this';

describe('demo onboarding content', () => {
  it('offers one demo persona per role', () => {
    expect(DEMO_ROLES.map((r) => r.role).sort()).toEqual([...USER_ROLES].sort());
  });

  it('suggests first steps for every role, each pointing at an in-app page', () => {
    for (const role of USER_ROLES) {
      expect(TRY_THIS[role].length).toBeGreaterThan(0);
      for (const step of TRY_THIS[role]) expect(step.link.startsWith('/')).toBe(true);
    }
  });
});
