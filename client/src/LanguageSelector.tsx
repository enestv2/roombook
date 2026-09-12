import { useTranslation } from 'react-i18next';
import { currentLanguage, setLanguage } from './i18n';

export function LanguageSelector() {
  const { t } = useTranslation();
  const language = currentLanguage();
  return <label>
    {t('language.label')}
    <select aria-label={t('language.label')} value={language} onChange={event => setLanguage(event.target.value)}>
      <option value="tr" aria-label={t('language.turkish')}>TR</option>
      <option value="en" aria-label={t('language.english')}>EN</option>
    </select>
  </label>;
}
