import { useState, useMemo, useEffect } from 'react';
import toast from 'react-hot-toast';
import { Search, Users, AlertTriangle, UserMinus, RotateCcw, ChevronLeft, ChevronRight, RefreshCw } from 'lucide-react';
import EmptyState from '../ui/EmptyState';
import { getMajorName, TEAM_MAJOR_GROUPS } from '../../constants/majors';
import { getDisplayGroupName } from '../../utils/teamDisplay';
import { isMissingTeamMajor } from '../../utils/teamManagement';

/**
 * Get display label for a major code.
 * If the code is in MAJOR_MAP, returns "CODE — Full Name".
 * Otherwise returns just the code.
 * Returns "-" if major is null/empty.
 */
const majorLabel = (major) => {
  if (isMissingTeamMajor(major)) return null;
  return major.trim().toUpperCase();
};

const majorTooltip = (major) => {
  if (isMissingTeamMajor(major)) return '';
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
  cls: _cls,
  selected = [],
  onSelectionChange = undefined,
  onRefresh: _onRefresh = undefined,
  onDeleteStudent = undefined,
  onReEnrollStudent = undefined,
  onSynchronizeMajors = undefined,
  synchronizingMajors = false,
  toolbarAction = null,
  selectionDisabled = false,
  maxSelection = 6,
  serverQuery = undefined,
  onServerQueryChange = undefined,
}) {
  const students = useMemo(() => (Array.isArray(rawStudents) ? rawStudents : []), [rawStudents]);
  const teams = useMemo(() => (Array.isArray(rawTeams) ? rawTeams : []), [rawTeams]);
  const [search, setSearch] = useState(serverQuery?.search || '');

  const teamMap = useMemo(() => {
    const map = new Map();
    teams.forEach(t => map.set(t._id.toString(), t));
    return map;
  }, [teams]);
  const [localFilterMajor, setLocalFilterMajor] = useState('');
  const filterMajor = serverQuery?.majorCode ?? localFilterMajor;

  useEffect(() => {
    if (!serverQuery || !onServerQueryChange || search === serverQuery.search) return undefined;
    const timeout = window.setTimeout(() => onServerQueryChange({ search }), 300);
    return () => window.clearTimeout(timeout);
  }, [onServerQueryChange, search, serverQuery]);

  const majors = useMemo(() => {
    const codes = students
      .map(s => s.major)
      .filter(m => typeof m === 'string' && m.trim().length > 0)
      .map(m => m.trim().toUpperCase());
    return [...new Set(codes)].sort();
  }, [students]);
  const majorMismatchCount = useMemo(
    () => students.filter(student => student.hasMajorMismatch).length,
    [students],
  );

  const filtered = useMemo(() => {
    let result = students.filter(s => {
      if (serverQuery) return true;
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
  }, [students, teams, search, filterMajor, teamMap, serverQuery]);

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
    const unassigned = filtered
      .filter(s => !s.teamId && s.enrollmentStatus === 'Active')
      .map(s => s._id);
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

  const canSelect = (s) => !selectionDisabled && !s.teamId && s.enrollmentStatus === 'Active';
  const getSelectionBlockReason = (s) => {
    if (selectionDisabled) return 'Selection is disabled.';
    if (s.teamId) return 'This student is already assigned or reserved by another team.';
    if (s.enrollmentStatus !== 'Active') return 'Only active enrollments can be selected.';
    return '';
  };

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
          onChange={(e) => serverQuery && onServerQueryChange
            ? onServerQueryChange({ majorCode: e.target.value })
            : setLocalFilterMajor(e.target.value)}
          className="min-h-8 rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-xs outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15 sm:text-sm"
        >
          <option value="">All majors</option>
          {TEAM_MAJOR_GROUPS.map(group => {
            const presentInGroup = serverQuery ? group.majors : group.majors.filter(m => majors.includes(m.code));
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
              <optgroup label="Other">
                {others.map(m => (
                  <option key={m} value={m}>{m}{getMajorName(m) ? ` — ${getMajorName(m)}` : ''}</option>
                ))}
              </optgroup>
            );
          })()}
        </select>
        {serverQuery && (
          <select
            value={serverQuery.status || ''}
            onChange={(e) => onServerQueryChange?.({ status: e.target.value })}
            className="min-h-8 rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-xs outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15 sm:text-sm"
          >
            <option value="">All enrollment statuses</option>
            <option value="Active">Active</option>
            <option value="Dropped">Dropped</option>
            <option value="Completed">Completed</option>
          </select>
        )}
        {selected.length > 0 && (
          <span className="inline-flex min-h-8 items-center rounded-lg bg-primary-50 px-2.5 py-1 text-xs font-semibold text-primary">
            {selected.length} selected
          </span>
        )}
        {majorMismatchCount > 0 && onSynchronizeMajors && (
          <button
            type="button"
            onClick={onSynchronizeMajors}
            disabled={synchronizingMajors}
            className="inline-flex min-h-8 items-center gap-1.5 rounded-lg border border-orange-200 bg-orange-50 px-2.5 py-1.5 text-xs font-semibold text-orange-700 transition hover:bg-orange-100 disabled:opacity-50"
            title="Use the official major imported for this class to correct registered profiles"
          >
            <RefreshCw className={`h-3.5 w-3.5 ${synchronizingMajors ? 'animate-spin' : ''}`} />
            Synchronize majors ({majorMismatchCount})
          </button>
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
                      checked={filtered.filter(s => !s.teamId && s.enrollmentStatus === 'Active').length > 0 && filtered.filter(s => !s.teamId && s.enrollmentStatus === 'Active').every(s => selected.includes(s._id))}
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
                <th className="px-3 py-2.5 text-center text-[10px] font-bold uppercase tracking-wider text-slate-500">Enrollment</th>
                {(onDeleteStudent || onReEnrollStudent) && (
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
                const selectionBlockReason = selectable ? '' : getSelectionBlockReason(s);

                const prevStudent = index > 0 ? filtered[index - 1] : null;
                const isFirstInTeam = s.teamId && (!prevStudent || prevStudent.teamId?.toString() !== s.teamId.toString());

                // Add border top if it's a new team block
                const rowClass = `transition-colors ${selectable ? 'cursor-pointer hover:bg-slate-50' : ''} ${isSelected ? 'bg-primary-50' : ''} ${isFirstInTeam ? 'border-t-2 border-slate-200' : ''}`;

                return (
                  <tr key={s._id} onClick={() => selectable && toggleSelect(s._id)} className={rowClass} title={selectionBlockReason}>
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
                        <div className="flex flex-col items-start gap-1">
                          <span className={`rounded-full px-1.5 py-0.5 text-[11px] font-semibold ${majorColor(s.major)}`} title={mTooltip}>
                            {mLabel}
                          </span>
                          <span className="text-[10px] font-medium text-slate-400">
                            {s.majorVerificationStatus || 'Unverified'}
                          </span>
                          {s.hasMajorMismatch && (
                            <span
                              className="flex items-center gap-1 rounded bg-orange-50 px-1.5 py-0.5 text-[10px] font-semibold text-orange-700"
                              title={`Registered major: ${s.profileMajorCode}. Official imported major: ${s.major}.`}
                            >
                              <AlertTriangle className="h-2.5 w-2.5" /> Registered as {s.profileMajorCode}
                            </span>
                          )}
                        </div>
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

                    <td className="px-3 py-2.5 text-center">
                      <span className={`rounded-full px-1.5 py-0.5 text-[11px] font-semibold ${
                        s.enrollmentStatus === 'Active'
                          ? 'bg-green-100 text-green-700'
                          : s.enrollmentStatus === 'Dropped'
                            ? 'bg-red-100 text-red-700'
                            : 'bg-blue-100 text-blue-700'
                      }`}>
                        {s.enrollmentStatus || 'Active'}
                      </span>
                    </td>
                    
                    {(onDeleteStudent || onReEnrollStudent) && (
                      <td className="px-3 py-2.5 text-center">
                        {s.enrollmentStatus === 'Active' && onDeleteStudent ? (
                          <button
                            onClick={(e) => { e.stopPropagation(); onDeleteStudent(s); }}
                            className="inline-flex h-7 w-7 items-center justify-center rounded-md text-slate-400 transition-colors hover:bg-red-50 hover:text-red-600"
                            title="Drop enrollment"
                            aria-label={`Drop enrollment for ${s.fullName || 'student'}`}
                          >
                            <UserMinus className="h-3.5 w-3.5" />
                          </button>
                        ) : s.enrollmentStatus === 'Dropped' && onReEnrollStudent ? (
                          <button
                            onClick={(e) => { e.stopPropagation(); onReEnrollStudent(s); }}
                            className="inline-flex h-7 w-7 items-center justify-center rounded-md text-slate-400 transition-colors hover:bg-green-50 hover:text-green-700"
                            title="Re-enroll student"
                            aria-label={`Re-enroll ${s.fullName || 'student'}`}
                          >
                            <RotateCcw className="h-3.5 w-3.5" />
                          </button>
                        ) : null}
                      </td>
                    )}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <div className="flex flex-col gap-2 border-t border-slate-100 px-3 py-2.5 text-[11px] text-slate-400 sm:flex-row sm:items-center sm:justify-between">
        <span>
          Showing {filtered.length} of {serverQuery?.totalCount ?? students.length} students · {students.filter(s => s.teamId).length} assigned on this page
        </span>
        {serverQuery && serverQuery.totalPages > 1 && (
          <div className="flex items-center gap-2">
            <button type="button" disabled={serverQuery.page <= 1} onClick={() => onServerQueryChange?.({ page: serverQuery.page - 1 })} className="inline-flex h-7 items-center gap-1 rounded-md border border-slate-200 px-2 font-semibold text-slate-600 hover:bg-slate-50 disabled:opacity-40">
              <ChevronLeft className="h-3.5 w-3.5" /> Previous
            </button>
            <span>Page {serverQuery.page} of {serverQuery.totalPages}</span>
            <button type="button" disabled={serverQuery.page >= serverQuery.totalPages} onClick={() => onServerQueryChange?.({ page: serverQuery.page + 1 })} className="inline-flex h-7 items-center gap-1 rounded-md border border-slate-200 px-2 font-semibold text-slate-600 hover:bg-slate-50 disabled:opacity-40">
              Next <ChevronRight className="h-3.5 w-3.5" />
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
