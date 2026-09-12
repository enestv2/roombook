import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { BookingFormView } from './BookingForm';
import { LanguageSelector } from './LanguageSelector';
import { setLanguage } from './i18n';

describe('language selector', () => {
  it('shows TR and EN codes with accessible full-language labels', async () => {
    setLanguage('en');
    render(<LanguageSelector />);

    expect(screen.getByRole('option', { name: 'Turkish' }).textContent).toBe('TR');
    expect(screen.getByRole('option', { name: 'English' }).textContent).toBe('EN');
    const user = userEvent.setup();
    await user.selectOptions(screen.getByRole('combobox'), 'tr');
    expect((screen.getByRole('combobox') as HTMLSelectElement).value).toBe('tr');
    expect(localStorage.getItem('roombook.language')).toBe('tr');
  });

  it('changes booking labels without clearing form values', async () => {
    setLanguage('en');
    render(<BookingFormView roomId="room" timeZone="UTC" />);
    const user = userEvent.setup();
    const inputs = screen.getAllByDisplayValue('');
    fireEvent.change(inputs[0], { target: { value: '2030-01-01T09:00' } });
    await user.selectOptions(screen.getByRole('combobox'), 'tr');

    expect(screen.getByRole('heading', { name: 'Oda ayırt' })).toBeTruthy();
    expect((inputs[0] as HTMLInputElement).value).toBe('2030-01-01T09:00');
  });
});
