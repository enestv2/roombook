import type { ReactNode } from 'react';

export function Toast({ children, tone = 'error' }: { children: ReactNode; tone?: 'error' | 'success' | 'info' }) {
  const tones = {
    error: 'border-red-200 bg-red-50 text-red-800',
    success: 'border-emerald-200 bg-emerald-50 text-emerald-800',
    info: 'border-brand-200 bg-brand-50 text-brand-800',
  };
  return <div aria-live="polite" className={`rounded-xl border px-4 py-3 text-sm font-medium ${tones[tone]}`} role={tone === 'error' ? 'alert' : 'status'}>{children}</div>;
}
