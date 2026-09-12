import { useEffect, useRef, useState, type ReactNode, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from './ui/Button';

export function AppShell({ children, headerActions }: { children: ReactNode; headerActions: ReactNode }) {
  const { t } = useTranslation();
  const [isCollapsed, setIsCollapsed] = useState(false);
  const [isMobileOpen, setIsMobileOpen] = useState(false);
  const mobileMenuButtonRef = useRef<HTMLButtonElement>(null);
  const mobileCloseButtonRef = useRef<HTMLButtonElement>(null);
  const mobileDialogRef = useRef<HTMLElement>(null);
  const wasMobileOpen = useRef(false);

  useEffect(() => {
    if (!isMobileOpen) {
      if (wasMobileOpen.current) {
        mobileMenuButtonRef.current?.focus();
        wasMobileOpen.current = false;
      }
      return undefined;
    }
    wasMobileOpen.current = true;
    mobileCloseButtonRef.current?.focus();
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        setIsMobileOpen(false);
        return;
      }
      if (event.key !== 'Tab') return;
      const focusable = Array.from(mobileDialogRef.current?.querySelectorAll<HTMLElement>('button, a, input, select, textarea, [tabindex]:not([tabindex="-1"])') ?? []).filter(element => !element.hasAttribute('disabled'));
      if (focusable.length === 0) return;
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [isMobileOpen]);

  return <div className="flex h-screen min-h-0 overflow-hidden bg-slate-50 text-slate-900">
    <aside aria-label={t('app.primaryNavigation')} className={`hidden h-full min-w-0 shrink-0 overflow-hidden border-r border-slate-200 bg-white py-7 transition-[width,padding] duration-200 lg:flex lg:flex-col ${isCollapsed ? 'w-20 px-3' : 'w-72 px-6'}`}>
      <SidebarContent collapsed={isCollapsed} onCollapse={() => setIsCollapsed(value => !value)} />
    </aside>
    {isMobileOpen && <>
      <button aria-label={t('app.closeNavigation')} className="fixed inset-0 z-30 bg-slate-950/40 lg:hidden" onClick={() => setIsMobileOpen(false)} type="button" />
      <aside aria-label={t('app.mobileNavigation')} aria-modal="true" className="fixed inset-y-0 left-0 z-40 flex w-72 min-w-0 flex-col overflow-hidden border-r border-slate-200 bg-white px-6 py-7 shadow-2xl lg:hidden" id="mobile-navigation" ref={mobileDialogRef} role="dialog">
        <SidebarContent closeButtonRef={mobileCloseButtonRef} onClose={() => setIsMobileOpen(false)} />
      </aside>
    </>}
    <div className="min-h-0 min-w-0 flex-1 overflow-y-auto">
      <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/90 px-4 py-6 backdrop-blur sm:px-8 lg:px-10">
        <div className="mx-auto flex w-full min-w-0 max-w-full items-center gap-4 lg:max-w-6xl">
          <div className="flex min-w-0 shrink-0 items-center gap-3 lg:hidden">
            <Button aria-controls="mobile-navigation" aria-expanded={isMobileOpen} aria-label={t('app.openNavigation')} className="size-10 !min-h-10 !p-0" onClick={() => setIsMobileOpen(true)} ref={mobileMenuButtonRef} variant="ghost">
              <MenuIcon />
            </Button>
            <Brand compact />
          </div>
        </div>
        <div className="absolute right-4 top-1/2 z-auto -translate-y-1/2 sm:right-8 lg:right-10">{headerActions}</div>
      </header>
      <main className="mx-auto w-full max-w-6xl px-4 py-8 sm:px-8 sm:py-10 lg:px-10">{children}</main>
    </div>
  </div>;
}

function SidebarContent({ closeButtonRef, collapsed = false, onClose, onCollapse }: { closeButtonRef?: RefObject<HTMLButtonElement | null>; collapsed?: boolean; onClose?: () => void; onCollapse?: () => void }) {
  const { t } = useTranslation();
  return <>
    <div className={`flex min-w-0 items-center ${collapsed ? 'justify-center' : 'justify-between'}`}>
      <Brand iconOnly={collapsed} />
      {onCollapse && <Button aria-label={collapsed ? t('app.expandNavigation') : t('app.collapseNavigation')} className="size-10 !min-h-10 !p-0" onClick={onCollapse} variant="ghost">
        {collapsed ? <ChevronRightIcon /> : <ChevronLeftIcon />}
      </Button>}
      {onClose && <Button aria-label={t('app.closeNavigation')} className="size-10 !min-h-10 !p-0" onClick={onClose} ref={closeButtonRef} variant="ghost"><CloseIcon /></Button>}
    </div>
    <nav aria-label={t('app.primaryNavigation')} className="mt-12 min-w-0">
      <a aria-current="page" className={`flex min-w-0 items-center rounded-xl bg-brand-50 py-3 text-sm font-bold text-brand-700 ${collapsed ? 'justify-center px-0' : 'gap-3 px-3'}`} href="#booking" onClick={onClose} title={collapsed ? t('app.navBooking') : undefined}>
        <CalendarIcon />
        {!collapsed && <span className="min-w-0 truncate">{t('app.navBooking')}</span>}
      </a>
    </nav>
    {!collapsed && <p className="mt-auto min-w-0 break-words text-xs leading-5 text-slate-400">{t('app.tagline')}</p>}
  </>;
}

function Brand({ compact = false, iconOnly = false }: { compact?: boolean; iconOnly?: boolean }) {
  const { t } = useTranslation();
  return <div className="flex min-w-0 items-center gap-3">
    <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-brand-600 text-sm font-black text-white shadow-lg shadow-indigo-200">R</span>
    <div className={`min-w-0 ${iconOnly ? 'hidden' : compact ? 'hidden sm:block' : ''}`}>
      <p className="truncate text-base font-black tracking-tight text-slate-950">RoomBook</p>
      <p className="truncate text-xs font-medium text-slate-400">{t('app.brandSubtitle')}</p>
    </div>
  </div>;
}

function CalendarIcon() {
  return <svg aria-hidden="true" className="size-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="1.8"><path strokeLinecap="round" strokeLinejoin="round" d="M8 2v4m8-4v4M4 9h16M6 4h12a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Z" /></svg>;
}

function MenuIcon() {
  return <svg aria-hidden="true" className="size-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2"><path strokeLinecap="round" d="M4 6h16M4 12h16M4 18h16" /></svg>;
}

function CloseIcon() {
  return <svg aria-hidden="true" className="size-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2"><path strokeLinecap="round" d="m6 6 12 12M18 6 6 18" /></svg>;
}

function ChevronLeftIcon() {
  return <svg aria-hidden="true" className="size-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2"><path strokeLinecap="round" strokeLinejoin="round" d="m15 18-6-6 6-6" /></svg>;
}

function ChevronRightIcon() {
  return <svg aria-hidden="true" className="size-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2"><path strokeLinecap="round" strokeLinejoin="round" d="m9 18 6-6-6-6" /></svg>;
}
