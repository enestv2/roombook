import { describe, expect, it } from 'vitest';
import { normalizeLanguage, resolveInitialLanguage } from './i18n';

describe('language preference', () => {
  it('normalizes regional Turkish tags and falls back unsupported values to English', () => {
    expect(normalizeLanguage('tr-TR')).toBe('tr');
    expect(normalizeLanguage('de-DE')).toBe('en');
    expect(normalizeLanguage('')).toBe('en');
  });

  it('prefers a saved language and otherwise detects Turkish browser preferences', () => {
    expect(resolveInitialLanguage('en', ['tr-TR'])).toBe('en');
    expect(resolveInitialLanguage(null, ['tr-TR', 'en-US'])).toBe('tr');
    expect(resolveInitialLanguage(undefined, ['de-DE'])).toBe('en');
  });
});
