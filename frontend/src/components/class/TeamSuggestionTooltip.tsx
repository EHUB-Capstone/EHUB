import { Info } from 'lucide-react';

export default function TeamSuggestionTooltip({ children, label = 'Xem gợi ý tạo nhóm' }) {
  return (
    <div className="group/tooltip relative inline-flex shrink-0">
      <button
        type="button"
        aria-label={label}
        className="inline-flex h-8 w-8 items-center justify-center rounded-full border border-indigo-200 bg-indigo-50 text-indigo-700 transition-colors hover:bg-indigo-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2"
      >
        <Info className="h-4 w-4" />
      </button>
      <div
        role="tooltip"
        className="pointer-events-none absolute right-0 top-full z-30 mt-2 hidden w-72 max-w-[calc(100vw-2rem)] rounded-lg bg-slate-900 px-3 py-2 text-left text-xs font-medium leading-5 text-white shadow-lg group-hover/tooltip:block group-focus-within/tooltip:block"
      >
        {children}
        <span className="absolute -top-1 right-3 h-2 w-2 rotate-45 bg-slate-900" />
      </div>
    </div>
  );
}
