import { describe, expect, it } from 'vitest';
import { externalLoginError } from '@app/core/auth/auth-options';
import { passwordStrength } from './password-strength';

describe('passwordStrength', () => {
  it('asks for more characters below the API minimum of 10', () => {
    expect(passwordStrength('')).toMatchObject({ score: 0 });
    expect(passwordStrength('abc')).toEqual({ score: 1, label: 'Too short: 7 more characters.' });
  });

  it('rewards length and character variety', () => {
    expect(passwordStrength('abcdefghij').score).toBe(2);
    expect(passwordStrength('abcdefghijkl').score).toBe(3);
    expect(passwordStrength('Abcdefgh1jk!').score).toBe(4);
    expect(passwordStrength('correct horse battery').score).toBe(4);
  });
});

describe('externalLoginError', () => {
  it('maps known codes and falls back for unknown ones', () => {
    expect(externalLoginError(undefined)).toBeNull();
    expect(externalLoginError('auth.account_disabled')).toContain('disabled');
    expect(externalLoginError('something_new')).toBe('Sign-in failed. Please try again.');
  });
});
