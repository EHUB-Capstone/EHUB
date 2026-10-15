import { useCallback, useEffect, useId, useState } from 'react';
import { createPortal } from 'react-dom';
import { Check, ChevronDown, Loader2, RefreshCw, X } from 'lucide-react';
import type { StartupIndustryDto } from '../../types/startupIndustries';

interface Props {
  industries: StartupIndustryDto[];
  selectedIds: string[];
  onChange: (ids: string[]) => void;
  labelId: string;
  loading: boolean;
  error: string;
  onRetry: () => void;
  max?: number;
  disabled?: boolean;
  invalid?: boolean;
  errorMessage?: string;
}

interface TooltipState {
  id: string;
  text: string;
  left: number;
  top?: number;
  bottom?: number;
}

const TOOLTIP_WIDTH = 288;
const VIEWPORT_MARGIN = 8;
const TOOLTIP_GAP = 8;

export default function StartupIndustryPicker({
  industries,
  selectedIds,
  onChange,
  labelId,
  loading,
  error,
  onRetry,
  max = 3,
  disabled = false,
  invalid = false,
  errorMessage,
}: Props) {
  const [open, setOpen] = useState(false);
  const [tooltip, setTooltip] = useState<TooltipState | null>(null);
  const tooltipId = useId();

  const selectedIndustries = selectedIds
    .map((id) => industries.find((industry) => industry.id === id))
    .filter((industry): industry is StartupIndustryDto => Boolean(industry));
  const limitReached = selectedIds.length >= max;
  const toggleDisabled = disabled || loading || Boolean(error) || industries.length === 0;

  const hideTooltip = useCallback(() => setTooltip(null), []);

  const showTooltip = (industry: StartupIndustryDto, element: HTMLElement) => {
    const rect = element.getBoundingClientRect();
    const left = Math.max(VIEWPORT_MARGIN, Math.min(rect.left, window.innerWidth - TOOLTIP_WIDTH - VIEWPORT_MARGIN));
    const roomBelow = window.innerHeight - rect.bottom;
    const placement = roomBelow >= 120 || roomBelow >= rect.top
      ? { top: rect.bottom + TOOLTIP_GAP }
      : { bottom: window.innerHeight - rect.top + TOOLTIP_GAP };
    setTooltip({ id: industry.id, text: industry.description?.trim() || 'No description', left, ...placement });
  };

  useEffect(() => {
    if (!open) return undefined;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { setTooltip(null); setOpen(false); }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  useEffect(() => {
    if (!tooltip) return undefined;
    window.addEventListener('scroll', hideTooltip, true);
    window.addEventListener('resize', hideTooltip);
    return () => {
      window.removeEventListener('scroll', hideTooltip, true);
      window.removeEventListener('resize', hideTooltip);
    };
  }, [tooltip, hideTooltip]);

  const toggleIndustry = (id: string) => {
    onChange(selectedIds.includes(id) ? selectedIds.filter((current) => current !== id) : [...selectedIds, id]);
  };

  return (
    <div>
      <div>
        <div className={`rounded-xl border bg-white p-2 transition focus-within:ring-2 focus-within:ring-primary/15 ${invalid ? 'border-red-300' : 'border-slate-200 focus-within:border-primary'}`}>
          {selectedIndustries.length > 0 && (
            <div className="mb-2 flex flex-wrap gap-1.5">
              {selectedIndustries.map((industry) => (
                <span key={industry.id} className="inline-flex items-center gap-1 rounded-md bg-primary-50 px-2 py-1 text-xs font-semibold text-primary-700">
                  {industry.name}
                  <button
                    type="button"
                    onClick={() => toggleIndustry(industry.id)}
                    disabled={disabled}
                    aria-label={`Remove ${industry.name}`}
                    className="text-primary-400 hover:text-red-500 disabled:opacity-50"
                  >
                    <X className="h-3 w-3" />
                  </button>
                </span>
              ))}
            </div>
          )}
          <button
            type="button"
            aria-labelledby={labelId}
            aria-expanded={open}
            aria-haspopup="listbox"
            disabled={toggleDisabled}
            onClick={() => { setTooltip(null); setOpen((current) => !current); }}
            className="flex w-full items-center justify-between gap-3 rounded-lg px-1 py-1 text-left text-sm text-slate-500 outline-none disabled:cursor-not-allowed disabled:opacity-60"
          >
            <span>{loading ? 'Loading industries…' : limitReached ? `Maximum ${max} industries selected` : 'Select startup industries'}</span>
            {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <ChevronDown className={`h-4 w-4 transition ${open ? 'rotate-180' : ''}`} />}
          </button>
        </div>
        {open && (
          <div className="mt-2 w-full rounded-xl border border-slate-200 bg-white" onClick={(event) => event.stopPropagation()}>
            <p className="border-b border-slate-100 px-3 py-2 text-xs text-slate-500">Hover a card to see its description · {selectedIds.length}/{max} selected</p>
            <div
              role="listbox"
              aria-multiselectable="true"
              aria-labelledby={labelId}
              className="grid grid-cols-2 gap-2 p-2 sm:grid-cols-3"
            >
              {industries.map((industry) => {
                const selected = selectedIds.includes(industry.id);
                const cardDisabled = disabled || (!selected && limitReached);
                return (
                  <div
                    key={industry.id}
                    onMouseEnter={(event) => showTooltip(industry, event.currentTarget)}
                    onMouseLeave={hideTooltip}
                  >
                    <button
                      type="button"
                      role="option"
                      aria-selected={selected}
                      aria-disabled={cardDisabled || undefined}
                      aria-describedby={tooltip?.id === industry.id ? tooltipId : undefined}
                      onClick={() => { if (!cardDisabled) toggleIndustry(industry.id); }}
                      onFocus={(event) => showTooltip(industry, event.currentTarget)}
                      onBlur={hideTooltip}
                      className={`relative flex min-h-11 w-full items-center rounded-lg border px-3 py-2 pr-7 text-left text-sm font-semibold transition focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/30 ${selected ? 'border-primary bg-primary-50 text-primary-700' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50'} ${cardDisabled && !selected ? 'cursor-not-allowed opacity-40' : ''}`}
                    >
                      <span className="line-clamp-2">{industry.name}</span>
                      {selected && <Check className="absolute right-2 top-2 h-4 w-4 text-primary-700" aria-hidden="true" />}
                    </button>
                  </div>
                );
              })}
            </div>
          </div>
        )}
      </div>
      <div className="mt-1 flex items-center justify-between gap-3 text-xs">
        <span className="text-red-600">{errorMessage}</span>
        <span className="text-slate-400">{selectedIds.length}/{max} selected</span>
      </div>
      {error && (
        <div className="mt-2 flex items-center justify-between gap-3 rounded-lg bg-red-50 px-3 py-2 text-xs text-red-700">
          <span>{error}</span>
          <button type="button" onClick={onRetry} className="inline-flex shrink-0 items-center gap-1 font-semibold hover:text-red-900">
            <RefreshCw className="h-3.5 w-3.5" /> Retry
          </button>
        </div>
      )}
      {!loading && !error && industries.length === 0 && (
        <p className="mt-2 text-xs text-amber-700">No active startup industries are available. Ask an administrator to create or activate one.</p>
      )}
      {tooltip && createPortal(
        <div
          id={tooltipId}
          role="tooltip"
          className="pointer-events-none fixed z-[100] max-h-[50vh] overflow-hidden whitespace-pre-wrap break-words rounded-lg bg-slate-900 px-3 py-2 text-left text-xs font-medium leading-5 text-white shadow-lg"
          style={{ width: TOOLTIP_WIDTH, maxWidth: `calc(100vw - ${VIEWPORT_MARGIN * 2}px)`, left: tooltip.left, top: tooltip.top, bottom: tooltip.bottom }}
        >
          {tooltip.text}
        </div>,
        document.body,
      )}
    </div>
  );
}
