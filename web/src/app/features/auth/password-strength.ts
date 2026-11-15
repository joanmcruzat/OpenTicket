export interface PasswordStrength {
  /** 0 (empty) to 4 (strong). */
  score: 0 | 1 | 2 | 3 | 4;
  label: string;
}

/**
 * A rough guide shown while typing. The API only enforces a minimum length of 10; length matters far more than
 * symbols, so the score mostly rewards length and mixes of character kinds.
 */
export function passwordStrength(password: string): PasswordStrength {
  if (password.length === 0) return { score: 0, label: 'At least 10 characters. A short phrase works well.' };
  if (password.length < 10) return { score: 1, label: `Too short: ${10 - password.length} more characters.` };
  const kinds = [/[a-z]/, /[A-Z]/, /\d/, /[^A-Za-z0-9]/].filter((re) => re.test(password)).length;
  if (password.length >= 16 || (password.length >= 12 && kinds >= 3)) return { score: 4, label: 'Strong password.' };
  if (password.length >= 12 || kinds >= 3) return { score: 3, label: 'Good password.' };
  return { score: 2, label: 'Okay. Longer is stronger.' };
}
