import { StrictMode, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { createRoot } from 'react-dom/client';
import { getRooms } from './api';
import { BookingFormView } from './BookingForm';
import { LanguageSelector } from './LanguageSelector';
import './i18n';
import './styles.css';

function App() {
  const { t } = useTranslation();
  const [rooms, setRooms] = useState<Awaited<ReturnType<typeof getRooms>>>();
  const [error, setError] = useState<string>();
  useEffect(() => { getRooms().then(setRooms).catch(reason => setError(reason instanceof Error ? reason.message : t('rooms.loadFailed'))); }, []);
  return <>
    <header><LanguageSelector /></header>
    {error && <p role="alert">{error}</p>}
    {!error && !rooms && <p>{t('rooms.loading')}</p>}
    {!error && rooms?.length === 0 && <p role="alert">{t('rooms.empty')}</p>}
    {!error && rooms && rooms.length > 0 && <BookingFormView roomId={rooms[0].id} timeZone={rooms[0].timeZone} />}
  </>;
}

createRoot(document.getElementById('root')!).render(<StrictMode><App /></StrictMode>);
