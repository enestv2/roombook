import type { ReactNode } from 'react';

export function SectionHeader({ children, description, id, title }: { children?: ReactNode; description?: string; id?: string; title: string }) {
  return <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
    <div>
      <h2 className="text-lg font-bold text-slate-950" id={id}>{title}</h2>
      {description && <p className="mt-1 text-sm leading-6 text-slate-500">{description}</p>}
    </div>
    {children}
  </div>;
}
