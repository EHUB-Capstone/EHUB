// @ts-nocheck
import { useState, useMemo } from 'react';
import toast from 'react-hot-toast';
import { Search, Users, AlertTriangle, Trash2 } from 'lucide-react';
import EmptyState from '../ui/EmptyState';
import { getMajorName, TEAM_MAJOR_GROUPS } from '../../constants/majors';
import { getDisplayGroupName } from '../../utils/teamDisplay';

/**
 * Get display label for a major code.
 * If the code is in MAJOR_MAP, returns "CODE — Full Name".
 * Otherwise returns just the code.
 * Returns "-" if major is null/empty.
 */
const majorLabel = (major) => {
  if (!major || typeof major !== 'string' || !major.trim()) return null;
  return major.trim().toUpperCase();
};

const majorTooltip = (major) => {
  if (!major || typeof major !== 'string' || !major.trim()) return '';
  const code = major.trim().toUpperCase();
  return getMajorName(code) || code;
};

// Deterministic color from major code
const majorColor = (major) => {
  const colors = [
    'bg-blue-100 text-blue-700',
    'bg-purple-100 text-purple-700',
    'bg-cyan-100 text-cyan-700',
    'bg-orange-100 text-orange-700',
    'bg-pink-100 text-pink-700',
    'bg-teal-100 text-teal-700',
  ];
  if (!major) return 'bg-slate-100 text-slate-400';
  let hash = 0;
  for (const c of major) hash = c.charCodeAt(0) + ((hash << 5) - hash);
  return colors[Math.abs(hash) % colors.length];
};

export default function StudentTable({
  students: rawStudents,
  teams: rawTeams,
  selected,
  onSelectionChange,
  onDeleteStudent,
  toolbarAction,
  selectionDisabled = false,
  maxSelection = 6,
}) {
  const students = useMemo(() => (Array.isArray(rawStudents) ? rawStudents : []), [rawStudents]);
  const teams = useMemo(() => (Array.isArray(rawTeams) ? rawTeams : []), [rawTeams]);
  const [search,      setSearch]      = useState('');

  const teamMap = useMemo(() => {
    const map = new Map();
    teams.forEach(t => map.set(t._id.toString(), t));
    return map;
  }, [teams]);
  const [filterMajor, setFilterMajor] = useState('');

  const majors = useMemo(() => {
    const codes = students
      .map(s => s.major)
      .filter(m => typeof m === 'string' && m.trim().length > 0)
      .map(m => m.trim().toUpperCase());
    return [...new Set(codes)].sort();
  }, [students]);

  const filtered = useMemo(() => {
    let result = students.filter(s => {
      const matchSearch = !search || [s.fullName, s.rollNumber, s.email]
        .some(v => v?.toLowerCase().includes(search.toLowerCase()));
      const matchMajor = !filterMajor || (s.major && s.major.toUpperCase() === filterMajor);
      return matchSearch && matchMajor;
    });

    const teamIndexMap = new Map();
    teams.forEach((t, index) => teamIndexMap.set(t._id.toString(), index));

    result.sort((a, b) => {
      if (a.teamId && b.teamId) {
        const aTeamId = a.teamId.toString();
        const bTeamId = b.teamId.toString();
        if (aTeamId === bTeamId) {
          const t = teamMap.get(aTeamId);
          // Leader first
          const aId = typeof a._id === 'object' ? a._id.toString() : a._id;
          const bId = typeof b._id === 'object' ? b._id.toString() : b._id;
          const leaderId = t?.leaderId?._id ? t.leaderId._id.toString() : (typeof t?.leaderId === 'string' ? t.leaderId : null);
          
          if (leaderId === aId) return -1;
          if (leaderId === bId) return 1;
          return 0;
        }
        return (teamIndexMap.get(aTeamId) ?? 999) - (teamIndexMap.get(bTeamId) ?? 999);
      }
      if (a.teamId) return -1;
      if (b.teamId) return 1;
      return 0;
    });

    return result;
  }, [students, teams, search, filterMajor, teamMap]);

  const hideProjectName = useMemo(() => {
    if (teams.length === 0) return false;
    return teams.every(t => !t.projectName || t.projectName.trim() === '' || t.projectName === t.groupName);
  }, [teams]);

  const toggleSelect = (id) => {
    if (selectionDisabled) return;
    onSelectionChange(prev => {
      if (prev.includes(id)) return prev.filter(x => x !== id);
      if (prev.length >= maxSelection) {
        toast.error(`You can only select up to ${maxSelection} students.`);
        return prev;
      }
      return [...prev, id];
    });
  };

  const toggleAll = () => {
    if (selectionDisabled) return;
    const unassigned = filtered.filter(s => !s.teamId).map(s => s._id);
    const allSelected = unassigned.length > 0 && unassigned.every(id => selected.includes(id));
    if (allSelected) {
      onSelectionChange(prev => prev.filter(id => !unassigned.includes(id)));
    } else {
      onSelectionChange(prev => {
        const newIds = unassigned.filter(id => !prev.includes(id));
        const allowedToAdd = maxSelection - prev.length;
        if (allowedToAdd <= 0) {
          toast.error(`Maximum of ${maxSelection} students reached.`);
          return prev;
        }
        if (newIds.length > allowedToAdd) {
          toast.error(`You can only add ${allowedToAdd} more students to reach the maximum of 6.`);
          return [...prev, ...newIds.slice(0, allowedToAdd)];
        }
        return [...prev, ...newIds];
      });
    }
  };

  const canSelect = (s) => !selectionDisabled && !s.teamId;

  return (
    <div className="overflow-hidden rounded-xl border border-slate-200/70 bg-white shadow-xs">
      {/* Toolbar */}
      <div className="flex flex-wrap items-center gap-2 border-b border-slate-100 p-3">
        <div className="relative flex-1 min-w-[180px]">
          <Search className="absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" />
          <input
            type="text"
            placeholder="Search name, roll, email…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="min-h-8 w-full rounded-lg border border-slate-200 bg-white py-1.5 pl-8 pr-3 text-xs outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15 sm:text-sm"
          />
        </div>
        <select
          value={filterMajor}
          onChange={(e) => setFilterMajor(e.target.value)}
          className="min-h-8 rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-xs outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15 sm:text-sm"
        >
          <option value="">Tất cả chuyên ngành</option>
          {TEAM_MAJOR_GROUPS.map(group => {
            const presentInGroup = group.majors.filter(m => majors.includes(m.code));
            if (presentInGroup.length === 0) return null;
            return (
              <optgroup key={group.key} label={group.label}>
                {presentInGroup.map(m => (
                  <option key={m.code} value={m.code}>{m.code} — {m.name}</option>
                ))}
              </optgroup>
            );
          })}
          {(() => {
            const teamMajorCodes = TEAM_MAJOR_GROUPS.flatMap(g => g.majors.map(m => m.code));
            const others = majors.filter(m => !teamMajorCodes.includes(m));
            if (others.length === 0) return null;
            return (
              <optgroup label="Khác">
                {others.map(m => (
                  <option key={m} value={m}>{m}{getMajorName(m) ? ` — ${getMajorName(m)}` : ''}</option>
                ))}
              </optgroup>
            );
          })()}
        </select>
        {selected.length > 0 && (
          <span className="inline-flex min-h-8 items-center rounded-lg bg-primary-50 px-2.5 py-1 text-xs font-semibold text-primary">
            {selected.length} selected
          </span>
        )}
        {toolbarAction}
      </div>

      {filtered.length === 0 ? (
        <div className="p-7">
          <EmptyState icon={Users} title="No students found" description="Try adjusting your search or filters" />
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 border-b border-slate-100">
              <tr>
                {!selectionDisabled && (
                  <th className="w-9 px-3 py-2.5">
                    <input
                      type="checkbox"
                      className="rounded"
                      checked={filtered.filter(s => !s.teamId).length > 0 && filtered.filter(s => !s.teamId).every(s => selected.includes(s._id))}
                      onChange={toggleAll}
                    />
                  </th>
                )}
                <th className="min-w-[220px] px-3 py-2.5 text-left text-[10px] font-bold uppercase tracking-wider text-slate-500">Student</th>
                <th className="hidden px-3 py-2.5 text-left text-[10px] font-bold uppercase tracking-wider text-slate-500 sm:table-cell">Roll No.</th>
                <th className="px-3 py-2.5 text-left text-[10px] font-bold uppercase tracking-wider text-slate-500">Major</th>
                <th className="hidden px-3 py-2.5 text-left text-[10px] font-bold uppercase tracking-wider text-slate-500 lg:table-cell">GroupName</th>
                {!hideProjectName && (
                  <th className="hidden px-3 py-2.5 text-left text-[10px] font-bold uppercase tracking-wider text-slate-500 2xl:table-cell">Project Name</th>
                )}
                <th className="hidden w-1/4 px-3 py-2.5 text-left text-[10px] font-bold uppercase tracking-wider text-slate-500 2xl:table-cell">Description</th>
                <th className="px-3 py-2.5 text-center text-[10px] font-bold uppercase tracking-wider text-slate-500">Team Status</th>
                {onDeleteStudent && (
                  <th className="px-3 py-2.5 text-center text-[10px] font-bold uppercase tracking-wider text-slate-500">Action</th>
                )}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {filtered.map((s, index) => {
                const isSelected = selected.includes(s._id);
                const selectable = canSelect(s);
                const mLabel = majorLabel(s.major);
                const mTooltip = majorTooltip(s.major);
                const team = s.teamId ? teamMap.get(s.teamId.toString()) : null;

                const prevStudent = index > 0 ? filtered[index - 1] : null;
                const isFirstInTeam = s.teamId && (!prevStudent || prevStudent.teamId?.toString() !== s.teamId.toString());

                // Add border top if it's a new team block
                const rowClass = `transition-colors ${selectable ? 'cursor-pointer hover:bg-slate-50' : ''} ${isSelected ? 'bg-primary-50' : ''} ${isFirstInTeam ? 'border-t-2 border-slate-200' : ''}`;

                return (
                  <tr key={s._id} onClick={() => selectable && toggleSelect(s._id)} className={rowClass}>
                    {!selectionDisabled && (
                      <td className="px-3 py-2.5">
                        <div className={`flex h-4.5 w-4.5 items-center justify-center rounded-full border-2 transition-all ${isSelected ? 'border-primary bg-primary' : 'border-slate-300'} ${!selectable ? 'opacity-30' : ''}`}>
                          {isSelected && <div className="h-1.5 w-1.5 rounded-full bg-white" />}
                        </div>
                      </td>
                    )}

                    <td className="min-w-[220px] px-3 py-2.5">
                      <div className="flex items-center gap-2.5">
                        <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-primary-300 to-secondary text-[11px] font-bold text-white">
                          {s.fullName?.charAt(0)?.toUpperCase() || '?'}
                        </div>
                        <div className="flex min-w-0 flex-col">
                          <span className="flex flex-wrap items-center gap-1 whitespace-normal break-words text-sm font-medium leading-snug text-slate-800">
                            {s.fullName || 'Unknown'}
                            {team && team.leaderId && (typeof team.leaderId === 'string' ? team.leaderId : team.leaderId._id).toString() === s._id.toString() && (
                              <span className="px-1.5 py-0.5 bg-amber-100 text-amber-700 text-[10px] rounded font-bold ml-1">L</span>
                            )}
                          </span>
                        </div>
                      </div>
                    </td>

                    <td className="hidden px-3 py-2.5 font-mono text-xs text-slate-500 sm:table-cell">{s.rollNumber || '—'}</td>

                    <td className="px-3 py-2.5">
                      {mLabel ? (
                        <span className={`rounded-full px-1.5 py-0.5 text-[11px] font-semibold ${majorColor(s.major)}`} title={mTooltip}>
                          {mLabel}
                        </span>
                      ) : (
                        <span className="flex w-fit items-center gap-1 rounded-full bg-amber-100 px-1.5 py-0.5 text-[11px] font-semibold text-amber-700" title="Missing major">
                          <AlertTriangle className="h-2.5 w-2.5" /> Missing
                        </span>
                      )}
                    </td>

                    <td className="hidden px-3 py-2.5 text-xs font-medium text-slate-500 lg:table-cell">
                      {(!s.teamId || isFirstInTeam) ? (getDisplayGroupName(team) || '—') : ''}
                    </td>

                    {!hideProjectName && (
                      <td className="hidden max-w-[150px] truncate px-3 py-2.5 text-xs text-slate-500 2xl:table-cell" title={team?.projectName}>
                        {(!s.teamId || isFirstInTeam) ? (team?.projectName || '—') : ''}
                      </td>
                    )}

                    <td className="hidden px-3 py-2.5 text-xs text-slate-500 2xl:table-cell" title={team?.description}>
                      {(!s.teamId || isFirstInTeam) ? (
                        <div className="line-clamp-2 max-w-sm">{team?.description || '—'}</div>
                      ) : ''}
                    </td>

                    <td className="px-3 py-2.5 text-center">
                      {(!s.teamId || isFirstInTeam) ? (
                        team ? (
                          team.status === 'PENDING' ? (
                            <span className="rounded-full bg-orange-100 px-1.5 py-0.5 text-[11px] font-semibold text-orange-700">Pending</span>
                          ) : team.status === 'NEEDS_REVISION' ? (
                            <span className="rounded-full bg-amber-100 px-1.5 py-0.5 text-[11px] font-semibold text-amber-700">Needs revision</span>
                          ) : team.status === 'REJECTED' ? (
                            <span className="rounded-full bg-red-100 px-1.5 py-0.5 text-[11px] font-semibold text-red-700">Rejected</span>
                          ) : (
                            <span className="rounded-full bg-green-100 px-1.5 py-0.5 text-[11px] font-semibold text-green-700">Approved</span>
                          )
                        ) : (
                          <span className="rounded-full bg-slate-100 px-1.5 py-0.5 text-[11px] font-semibold text-slate-500">Unassigned</span>
                        )
                      ) : ''}
                    </td>
                    
                    {onDeleteStudent && (
                      <td className="px-3 py-2.5 text-center">
                        <button
                          onClick={(e) => { e.stopPropagation(); onDeleteStudent(s); }}
                          className="inline-flex h-7 w-7 items-center justify-center rounded-md text-slate-400 transition-colors hover:bg-red-50 hover:text-red-600"
                          title="Xóa sinh viên"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </td>
                    )}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <div className="border-t border-slate-100 px-3 py-2.5 text-[11px] text-slate-400">
        Showing {filtered.length} of {students.length} students · {students.filter(s => s.teamId).length} assigned
      </div>
    </div>
  );
}
