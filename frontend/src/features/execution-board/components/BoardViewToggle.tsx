// @ts-nocheck
import { Kanban, Table2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import i18n from '../../../i18n';

const VIEWS = [
  { key: 'kanban', get label() { return i18n.t('workspace:boardViewToggle.kanban'); }, icon: Kanban },
  { key: 'table', get label() { return i18n.t('workspace:boardViewToggle.table'); }, icon: Table2 },
];

export default function BoardViewToggle({ view, onChange }) {
  const { t } = useTranslation('workspace');
  return (
    <div className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-slate-50 p-1">
      <span className="hidden px-2 text-xs font-semibold text-slate-500 sm:inline">{t('common:actions.view')}</span>
      {VIEWS.map(({ key, label, icon: Icon }) => {
        const active = view === key;
        return (
          <button
            key={key}
            type="button"
            onClick={() => onChange(key)}
            className={`inline-flex h-8 items-center gap-1.5 rounded-md px-2.5 text-xs font-semibold transition-colors ${
              active
                ? 'bg-white text-slate-950 shadow-sm ring-1 ring-slate-200'
                : 'text-slate-500 hover:bg-white/70 hover:text-slate-800'
            }`}
            aria-pressed={active}
          >
            <Icon className="h-3.5 w-3.5" />
            {label}
          </button>
        );
      })}
    </div>
  );
}
