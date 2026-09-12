import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import * as api from './api';
import { App } from './App';
import { setLanguage } from './i18n';

const rooms = [
  { id: 'board', name: 'Board room', timeZone: 'Europe/Istanbul', workingPeriods: [] },
  { id: 'focus', name: 'Focus room', timeZone: 'UTC', workingPeriods: [] },
];

describe('application workspace', () => {
  beforeEach(() => setLanguage('en'));
  afterEach(() => { cleanup(); vi.restoreAllMocks(); });

  it('shows an accessible loading skeleton before rooms arrive', async () => {
    let resolveRooms: (value: typeof rooms) => void = () => undefined;
    vi.spyOn(api, 'getRooms').mockReturnValue(new Promise(resolve => { resolveRooms = resolve; }));

    render(<App />);
    expect(screen.getByRole('status', { name: 'Loading rooms…' })).toBeTruthy();

    resolveRooms(rooms);
    expect(await screen.findByRole('radio', { name: /Board room/ })).toBeTruthy();
  });

  it('offers retry after a room loading failure', async () => {
    vi.spyOn(api, 'getRooms')
      .mockRejectedValueOnce(new Error('Temporary service issue'))
      .mockResolvedValueOnce(rooms);

    render(<App />);
    expect(await screen.findByText('Temporary service issue')).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(await screen.findByRole('radio', { name: /Board room/ })).toBeTruthy();
  });

  it('shows a purposeful empty state when no rooms are returned', async () => {
    vi.spyOn(api, 'getRooms').mockResolvedValue([]);

    render(<App />);
    expect(await screen.findByRole('heading', { name: 'No rooms are currently available.' })).toBeTruthy();
  });

  it('selects a real room and updates the booking context', async () => {
    vi.spyOn(api, 'getRooms').mockResolvedValue(rooms);

    render(<App />);
    await screen.findByRole('radio', { name: /Board room/ });
    await userEvent.click(screen.getByRole('radio', { name: /Focus room/ }));

    await waitFor(() => expect(screen.getByText('Room timezone: UTC')).toBeTruthy());
  });

  it('keeps the booking value while switching language in the responsive shell', async () => {
    vi.spyOn(api, 'getRooms').mockResolvedValue(rooms);

    render(<App />);
    await screen.findByRole('radio', { name: /Board room/ });
    expect(screen.getAllByRole('navigation', { name: 'Primary navigation' }).length).toBeGreaterThan(0);
    expect(screen.getByRole('combobox', { name: 'Language' })).toBeTruthy();
    const desktopSidebar = screen.getByRole('complementary', { name: 'Primary navigation' });
    const sidebarClassName = desktopSidebar.className;
    expect(sidebarClassName).toContain('h-full');
    expect(sidebarClassName).toContain('overflow-hidden');
    expect(sidebarClassName).toContain('w-72');
    expect(desktopSidebar.parentElement?.className).toContain('h-screen');
    expect(desktopSidebar.nextElementSibling?.className).toContain('overflow-y-auto');

    fireEvent.change(screen.getByLabelText('Start date and time'), { target: { value: '2030-01-01T09:00' } });
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Language' }), 'tr');

    expect(await screen.findByLabelText('Başlangıç tarihi ve saati')).toBeTruthy();
    expect((screen.getByLabelText('Başlangıç tarihi ve saati') as HTMLInputElement).value).toBe('2030-01-01T09:00');
    expect(desktopSidebar.className).toBe(sidebarClassName);
    setLanguage('en');
  });

  it('keeps the header sticky and the language control compact', async () => {
    vi.spyOn(api, 'getRooms').mockResolvedValue(rooms);

    render(<App />);
    await screen.findByRole('radio', { name: /Board room/ });

    const header = screen.getByRole('banner');
    const languageControl = screen.getByRole('combobox', { name: 'Language' });
    expect(header.className).toContain('sticky');
    expect(header.className).toContain('top-0');
    expect(header.className).toContain('py-6');
    expect(languageControl.className).toContain('h-7');
    expect(languageControl.className).toContain('rounded-md');
    expect(languageControl.className).toContain('bg-transparent');
    expect(languageControl.className).toContain('shadow-none');
    expect(languageControl.className).toContain('px-2');
    expect(languageControl.className).toContain('py-0');
    expect(languageControl.className).toContain('border-slate-200');
    expect(languageControl.className).toContain('hover:border-slate-300');
    expect(languageControl.className).toContain('min-h-7');
    expect(languageControl.className).toContain('text-[11px]');
    expect(languageControl.parentElement?.className).toContain('ml-2');
  });

  it('supports a collapsible desktop rail and an escapable mobile drawer', async () => {
    vi.spyOn(api, 'getRooms').mockResolvedValue(rooms);

    render(<App />);
    await screen.findByRole('radio', { name: /Board room/ });

    await userEvent.click(screen.getByRole('button', { name: 'Collapse navigation' }));
    expect(screen.getByRole('button', { name: 'Expand navigation' })).toBeTruthy();

    const openButton = screen.getByRole('button', { name: 'Open navigation' });
    await userEvent.click(openButton);
    const drawer = screen.getByRole('dialog', { name: 'Mobile navigation' });
    const closeButton = within(drawer).getByRole('button', { name: 'Close navigation' });
    expect(document.activeElement).toBe(closeButton);
    expect(openButton.getAttribute('aria-expanded')).toBe('true');

    fireEvent.keyDown(document, { key: 'Tab', shiftKey: true });
    expect(document.activeElement).toBe(within(drawer).getByRole('link', { name: 'Book a room' }));
    fireEvent.keyDown(document, { key: 'Tab' });
    expect(document.activeElement).toBe(closeButton);

    fireEvent.keyDown(document, { key: 'Escape' });
    expect(openButton.getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(openButton);

    await userEvent.click(openButton);
    const reopenedDrawer = screen.getByRole('dialog', { name: 'Mobile navigation' });
    await userEvent.click(within(reopenedDrawer).getByRole('button', { name: 'Close navigation' }));
    expect(openButton.getAttribute('aria-expanded')).toBe('false');

    await userEvent.click(openButton);
    const closeControls = screen.getAllByRole('button', { name: 'Close navigation' });
    await userEvent.click(closeControls[0]);
    expect(openButton.getAttribute('aria-expanded')).toBe('false');
  });
});
