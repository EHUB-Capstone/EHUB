// @ts-nocheck
import { Search, X } from 'lucide-react';
import { PRIORITY_CFG, WEEKS } from '../constants';

const memberId = (member) => member._id || member.studentId?._id || member.userId?._id;
const memberName = (member) => member.fullName || member.studentId?.fullName || member.userId?.name || 'Member';

export default function BoardFilters({ filters, onChange, onClear, teamMembers, isFetching }) {
  const hasFilters = filters.week !== 'ALL' || filters.assignee !== 'ALL' || filters.priority !== 'ALL' || filters.search.trim();

  return (
    <div className="mt-5 border-t border-slate-100 pt-5">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center">
        <label className="flex min-w-[220px] flex-1 items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm shadow-sm transition-colors focus-within:border-blue-400 focus-within:ring-2 focus-within:ring-blue-500/20">
          <Search className="h-4 w-4 text-slate-400" />
          <input
            type="search"
            value={filters.search}
            onChange={(event) => onChange({ search: event.target.value })}
            placeholder="Search tasks..."
            className="w-full bg-transparent text-sm text-slate-800 outline-none placeholder:text-slate-400"
          />
        </label>

        <select
          value={filters.week}
          onChange={(event) => onChange({ week: event.target.value })}
          className="h-10 rounded-lg border border-slate-200 bg-white px-3 text-sm text-slate-700 shadow-sm outline-none transition-colors hover:border-slate-300 focus:border-blue-400 focus:ring-2 focus:ring-blue-500/20"
        >
          <option value="ALL">All Weeks</option>
          {WEEKS.map((week) => <option key={week} value={week}>Week {week}</option>)}
        </select>

        <select
          value={filters.assignee}
          onChange={(event) => onChange({ assignee: event.target.value })}
          className="h-10 rounded-lg border border-slate-200 bg-white px-3 text-sm text-slate-700 shadow-sm outline-none transition-colors hover:border-slate-300 focus:border-blue-400 focus:ring-2 focus:ring-blue-500/20"
        >
          <option value="ALL">All Members</option>
          {teamMembers.map((member) => (
            <option key={memberId(member)} value={memberId(member)}>{memberName(member)}</option>
          ))}
        </select>

        <select
          value={filters.priority}
          onChange={(event) => onChange({ priority: event.target.value })}
          className="h-10 rounded-lg border border-slate-200 bg-white px-3 text-sm text-slate-700 shadow-sm outline-none transition-colors hover:border-slate-300 focus:border-blue-400 focus:ring-2 focus:ring-blue-500/20"
        >
          <option value="ALL">All Priorities</option>
          {Object.entries(PRIORITY_CFG).map(([priority, cfg]) => (
            <option key={priority} value={priority}>{cfg.label}</option>
          ))}
        </select>

        {hasFilters && (
          <button
            type="button"
            onClick={onClear}
            className="inline-flex h-10 items-center justify-center gap-1.5 rounded-lg px-3 text-sm font-medium text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-800"
          >
            <X className="h-4 w-4" />
            Clear
          </button>
        )}
      </div>

      {isFetching && (
        <div className="mt-3 h-1 overflow-hidden rounded-full bg-slate-100">
          <div className="h-full w-1/3 animate-pulse rounded-full bg-blue-500" />
        </div>
      )}
    </div>
  );
}
