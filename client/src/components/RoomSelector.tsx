import { useTranslation } from 'react-i18next';
import type { Room } from '../api';
import { Card } from './ui/Card';
import { SectionHeader } from './ui/SectionHeader';
import { StatusBadge } from './ui/StatusBadge';

export function RoomSelector({ onSelect, rooms, selectedRoomId }: { onSelect: (room: Room) => void; rooms: Room[]; selectedRoomId?: string }) {
  const { t } = useTranslation();
  return <section aria-labelledby="room-selection-title" className="grid gap-5">
    <SectionHeader description={t('rooms.chooseDescription')} id="room-selection-title" title={t('rooms.chooseTitle')} />
    <div aria-label={t('rooms.chooseTitle')} className="grid gap-3 md:grid-cols-2" role="radiogroup">
      {rooms.map(room => {
        const selected = room.id === selectedRoomId;
        return <Card className={`transition-shadow ${selected ? 'border-brand-500 ring-2 ring-brand-100' : ''}`} key={room.id}>
          <button
            aria-checked={selected}
            className="flex min-h-32 w-full flex-col items-start justify-between gap-5 p-5 text-left"
            onClick={() => onSelect(room)}
            role="radio"
            type="button"
          >
            <span className="flex w-full items-start justify-between gap-3">
              <span>
                <span className="block text-base font-bold text-slate-950">{room.name}</span>
                <span className="mt-1 block text-sm text-slate-500">{room.timeZone}</span>
              </span>
              <span className={`mt-1 size-4 rounded-full border-2 ${selected ? 'border-brand-600 bg-brand-600 ring-4 ring-brand-100' : 'border-slate-300'}`} />
            </span>
            <StatusBadge tone={selected ? 'success' : 'neutral'}>{selected ? t('rooms.selected') : t('rooms.available')}</StatusBadge>
          </button>
        </Card>;
      })}
    </div>
  </section>;
}
