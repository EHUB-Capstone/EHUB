// @ts-nocheck
const ITEMS = [
  { key: 'total', label: 'Total' },
  { key: 'completed', label: 'Completed' },
  { key: 'inProgress', label: 'In Progress' },
  { key: 'overdue', label: 'Overdue' },
];

export default function BoardSummary({ summary }) {
  if (!summary) return null;

  return (
    <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50/60 p-3">
      <div className="grid grid-cols-2 gap-2 md:grid-cols-5">
        {ITEMS.map((item) => (
          <div key={item.key} className="rounded-md bg-white px-3 py-2 ring-1 ring-slate-200/70">
            <div className="text-lg font-semibold text-slate-950">{summary[item.key] || 0}</div>
            <div className="text-xs font-medium text-slate-500">{item.label}</div>
          </div>
        ))}
        <div className="col-span-2 rounded-md bg-white px-3 py-2 ring-1 ring-slate-200/70 md:col-span-1">
          <div className="flex items-center justify-between">
            <div className="text-lg font-semibold text-slate-950">{summary.completionPercentage || 0}%</div>
            <div className="text-xs font-medium text-slate-500">Completion</div>
          </div>
          <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-slate-100">
            <div
              className="h-full rounded-full bg-emerald-500 transition-[width] duration-300"
              style={{ width: `${summary.completionPercentage || 0}%` }}
            />
          </div>
        </div>
      </div>
    </div>
  );
}
