import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import en from './locales/en.json';
import tr from './locales/tr.json';

export const supportedLanguages = ['en', 'tr'] as const;
export type Language = typeof supportedLanguages[number];
export const languageStorageKey = 'roombook.language';

export function normalizeLanguage(value: string | null | undefined): Language {
  const language = value?.trim().toLowerCase().split('-')[0];
  return language === 'tr' ? 'tr' : 'en';
}

export function resolveInitialLanguage(
  savedLanguage: string | null | undefined,
  browserLanguages: readonly string[] = [],
): Language {
  if (savedLanguage !== null && savedLanguage !== undefined) return normalizeLanguage(savedLanguage);
  return browserLanguages.some(language => language.trim().toLowerCase().startsWith('tr')) ? 'tr' : 'en';
}

function browserLanguages(): readonly string[] {
  return typeof navigator === 'undefined' ? [] : navigator.languages;
}

function savedLanguage(): string | null {
  return typeof localStorage === 'undefined' ? null : localStorage.getItem(languageStorageKey);
}

export const initialLanguage = resolveInitialLanguage(savedLanguage(), browserLanguages());

void i18n.use(initReactI18next).init({
  resources: { en: { translation: en }, tr: { translation: tr } },
  lng: initialLanguage,
  fallbackLng: 'en',
  supportedLngs: supportedLanguages,
  interpolation: { escapeValue: false },
});

export function setLanguage(language: string): Language {
  const selected = normalizeLanguage(language);
  if (typeof localStorage !== 'undefined') localStorage.setItem(languageStorageKey, selected);
  void i18n.changeLanguage(selected);
  return selected;
}

export function currentLanguage(): Language {
  return normalizeLanguage(i18n.resolvedLanguage ?? i18n.language);
}

export default i18n;
