// @ts-nocheck
import { CheckSquare, Plus } from 'lucide-react';

export default function BoardHeader({ canCreate, onCreate }) {
  return (
    <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
      <div>
        <h1 className="flex items-center gap-2 text-2xl font-semibold tracking-tight text-slate-950">
          <CheckSquare className="h-6 w-6 text-blue-600" />
          Execution Board
        </h1>
        <p className="mt-1 text-sm text-slate-500">
          Weekly team tasks and milestone progress in one lightweight board.
        </p>
      </div>

      {canCreate && (
        <button
          type="button"
          onClick={onCreate}
          className="inline-flex h-9 items-center justify-center gap-2 rounded-lg bg-blue-600 px-3.5 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
        >
          <Plus className="h-4 w-4" />
          Add Task
        </button>
      )}
    </div>
  );
}
