import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { getRooms, type Room } from './api';
import { AppShell } from './components/AppShell';
import { RoomSelector } from './components/RoomSelector';
import { Button } from './components/ui/Button';
import { EmptyState } from './components/ui/EmptyState';
import { PageHeader } from './components/ui/PageHeader';
import { Skeleton } from './components/ui/Skeleton';
import { BookingFormView } from './BookingForm';
import i18n from './i18n';
import { LanguageSelector } from './LanguageSelector';

export function App() {
  const { t } = useTranslation();
  const [rooms, setRooms] = useState<Room[]>();
  const [selectedRoomId, setSelectedRoomId] = useState<string>();
  const [error, setError] = useState<string>();
  const [isLoading, setIsLoading] = useState(true);

  const loadRooms = useCallback(async () => {
    setIsLoading(true);
    setError(undefined);
    try {
      const loadedRooms = await getRooms();
      setRooms(loadedRooms);
      setSelectedRoomId(current => loadedRooms.some(room => room.id === current) ? current : loadedRooms[0]?.id);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : i18n.t('rooms.loadFailed'));
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => { void loadRooms(); }, [loadRooms]);

  const selectedRoom = rooms?.find(room => room.id === selectedRoomId);
  return <AppShell headerActions={<LanguageSelector />}>
    <div className="grid gap-8" id="booking">
      <PageHeader eyebrow={t('app.eyebrow')} subtitle={t('app.subtitle')} title={t('booking.title')} />
      {isLoading && <LoadingState />}
      {!isLoading && error && <div role="alert">
        <EmptyState
          action={<Button onClick={() => void loadRooms()}>{t('rooms.retry')}</Button>}
          description={error}
          icon={<RefreshIcon />}
          title={t('rooms.loadFailed')}
        />
      </div>}
      {!isLoading && !error && rooms?.length === 0 && <EmptyState description={t('rooms.emptyDescription')} icon={<RoomIcon />} title={t('rooms.empty')} />}
      {!isLoading && !error && rooms && rooms.length > 0 && <>
        <RoomSelector onSelect={room => setSelectedRoomId(room.id)} rooms={rooms} selectedRoomId={selectedRoomId} />
        {selectedRoom && <BookingFormView key={selectedRoom.id} roomId={selectedRoom.id} roomName={selectedRoom.name} timeZone={selectedRoom.timeZone} />}
      </>}
    </div>
  </AppShell>;
}

function LoadingState() {
  const { t } = useTranslation();
  return <div aria-label={t('rooms.loading')} className="grid gap-6" role="status">
    <div className="grid gap-3"><Skeleton className="h-3 w-24" /><Skeleton className="h-12 w-3/4 max-w-lg" /><Skeleton className="h-5 w-full max-w-2xl" /></div>
    <div className="grid gap-3 md:grid-cols-2"><Skeleton className="h-32" /><Skeleton className="h-32" /></div>
    <Skeleton className="h-80" />
  </div>;
}

function RefreshIcon() {
  return <svg aria-hidden="true" className="size-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="1.8"><path strokeLinecap="round" strokeLinejoin="round" d="M20 11a8.1 8.1 0 0 0-14.7-3.8L4 9m0 0V4m0 5h5M4 13a8.1 8.1 0 0 0 14.7 3.8L20 15m0 0v5m0-5h-5" /></svg>;
}

function RoomIcon() {
  return <svg aria-hidden="true" className="size-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="1.8"><path strokeLinecap="round" strokeLinejoin="round" d="M4 20V5a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v15M2 20h20M8 8h2m4 0h2m-8 4h2m4 0h2m-8 4h2m4 0h2" /></svg>;
}
