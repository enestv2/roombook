import type { ReactNode } from 'react';

type FieldProps = {
  children: ReactNode;
  error?: string;
  hint?: string;
  id: string;
  label: string;
};

export function Field({ children, error, hint, id, label }: FieldProps) {
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  return <div className="grid gap-2">
    <label className="text-sm font-semibold text-slate-700" htmlFor={id}>{label}</label>
    {hint && <p className="text-xs text-slate-500" id={hintId}>{hint}</p>}
    {children}
    {error && <p className="text-sm font-medium text-red-700" id={errorId} role="alert">{error}</p>}
  </div>;
}

export function fieldDescribedBy(id: string, hasError: boolean, hasHint: boolean): string | undefined {
  const ids = [hasHint ? `${id}-hint` : '', hasError ? `${id}-error` : ''].filter(Boolean);
  return ids.length > 0 ? ids.join(' ') : undefined;
}
