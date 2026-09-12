import { StrictMode, useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { getRooms } from './api';
import { BookingFormView } from './BookingForm';
import './styles.css';

function App() {
  const [rooms, setRooms] = useState<Awaited<ReturnType<typeof getRooms>>>();
  const [error, setError] = useState<string>();
  useEffect(() => { getRooms().then(setRooms).catch(reason => setError(reason instanceof Error ? reason.message : 'Rooms could not be loaded.')); }, []);
  if (error) return <p role="alert">{error}</p>;
  if (!rooms) return <p>Loading rooms…</p>;
  if (rooms.length === 0) return <p role="alert">No rooms are currently available.</p>;
  return <BookingFormView roomId={rooms[0].id} timeZone={rooms[0].timeZone} />;
}

createRoot(document.getElementById('root')!).render(<StrictMode><App /></StrictMode>);
