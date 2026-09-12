import type { ReactNode } from 'react';
import { Card } from './Card';

export function EmptyState({ action, description, icon, title }: { action?: ReactNode; description: string; icon?: ReactNode; title: string }) {
  return <Card className="flex flex-col items-center px-6 py-12 text-center sm:px-12">
    {icon && <div className="mb-4 flex size-12 items-center justify-center rounded-2xl bg-brand-50 text-brand-600">{icon}</div>}
    <h2 className="text-lg font-bold text-slate-950">{title}</h2>
    <p className="mt-2 max-w-md text-sm leading-6 text-slate-500">{description}</p>
    {action && <div className="mt-6">{action}</div>}
  </Card>;
}
