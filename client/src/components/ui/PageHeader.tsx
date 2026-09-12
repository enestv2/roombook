import type { ReactNode } from 'react';

export function PageHeader({ actions, eyebrow, subtitle, title }: { actions?: ReactNode; eyebrow: string; subtitle: string; title: string }) {
  return <div className="flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between">
    <div className="max-w-2xl">
      <p className="mb-2 text-xs font-bold uppercase tracking-[0.18em] text-brand-600">{eyebrow}</p>
      <h1 className="text-3xl font-bold tracking-tight text-slate-950 sm:text-4xl">{title}</h1>
      <p className="mt-3 text-base leading-7 text-slate-600">{subtitle}</p>
    </div>
    {actions}
  </div>;
}
