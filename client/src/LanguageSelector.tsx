import { useTranslation } from 'react-i18next';
import { currentLanguage, setLanguage } from './i18n';

export function LanguageSelector() {
  const { t } = useTranslation();
  const language = currentLanguage();
  return <label className="ml-2 flex items-center gap-1 text-xs font-semibold text-slate-600">
    <span className="sr-only">{t('language.label')}</span>
    <select aria-label={t('language.label')} className="h-7 min-h-7 cursor-pointer rounded-md border border-slate-200 bg-transparent px-2 py-0 text-[11px] font-bold text-slate-700 shadow-none transition-colors hover:border-slate-300" value={language} onChange={event => setLanguage(event.target.value)}>
      <option value="tr" aria-label={t('language.turkish')}>TR</option>
      <option value="en" aria-label={t('language.english')}>EN</option>
    </select>
  </label>;
}
