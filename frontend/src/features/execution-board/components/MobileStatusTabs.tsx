// @ts-nocheck
import { useDroppable } from '@dnd-kit/core';
import { STATUS_CFG, STATUSES } from '../constants';

function MobileStatusTab({ status, activeStatus, onChange, grouped }) {
  const cfg = STATUS_CFG[status];
  const active = activeStatus === status;
  const count = grouped?.[status]?.length || 0;
  const { setNodeRef, isOver } = useDroppable({
    id: `column-tab-${status}`,
    data: { type: 'column', status },
  });

  return (
    <button
      ref={setNodeRef}
      type="button"
      onClick={() => onChange(status)}
      className={`inline-flex shrink-0 items-center gap-2 rounded-full border px-3 py-1.5 text-xs font-semibold transition-colors ${
        active || isOver
          ? `${cfg.border} ${cfg.bg} ${cfg.text} ${isOver ? 'ring-2 ring-blue-500/20' : ''}`
          : 'border-slate-200 bg-white text-slate-500 hover:bg-slate-50'
      }`}
    >
      <span className={`h-1.5 w-1.5 rounded-full ${cfg.dot}`} />
      {cfg.label}
      <span className="rounded-full bg-white/70 px-1.5">{count}</span>
    </button>
  );
}

export default function MobileStatusTabs({ activeStatus, onChange, grouped }) {
  return (
    <div className="md:hidden">
      <div className="flex gap-2 overflow-x-auto pb-2">
        {STATUSES.map((status) => (
          <MobileStatusTab
            key={status}
            status={status}
            activeStatus={activeStatus}
            onChange={onChange}
            grouped={grouped}
          />
        ))}
      </div>
    </div>
  );
}
