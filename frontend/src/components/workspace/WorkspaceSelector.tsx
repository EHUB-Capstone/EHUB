// @ts-nocheck
import { useEffect, useRef, useState } from 'react';
import { Archive, Check, ChevronDown, Clock3, History } from 'lucide-react';

export default function WorkspaceSelector({ selectedWorkspace, availableWorkspaces = [], onChangeWorkspace, disabled = false }) {
  const [open, setOpen] = useState(false);
  const menuRef = useRef(null);

  useEffect(() => {
    if (!open) return undefined;

    const handlePointerDown = (event) => {
      if (menuRef.current && !menuRef.current.contains(event.target)) {
        setOpen(false);
      }
    };

    const handleKeyDown = (event) => {
      if (event.key === 'Escape') setOpen(false);
    };

    document.addEventListener('pointerdown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);

    return () => {
      document.removeEventListener('pointerdown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [open]);

  if (!selectedWorkspace) return null;

  const hasMultipleWorkspaces = availableWorkspaces.length > 1;
  const selectedTeamId = String(selectedWorkspace.teamId);
  const selectedLabel = [
    selectedWorkspace.courseCode || 'Workspace',
    selectedWorkspace.semester,
  ].filter(Boolean).join(' - ');

  const handleSelect = (teamId) => {
    setOpen(false);
    if (String(teamId) !== selectedTeamId) onChangeWorkspace(teamId);
  };

  return (
    <div ref={menuRef} className="relative w-full sm:w-auto">
      <button
        type="button"
        onClick={() => hasMultipleWorkspaces && !disabled && setOpen((value) => !value)}
        disabled={disabled || !hasMultipleWorkspaces}
        className="flex w-full min-w-[260px] items-center justify-between gap-3 rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-left shadow-sm transition-all hover:border-primary/40 hover:shadow-md focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/30 disabled:cursor-default disabled:hover:border-slate-200 disabled:hover:shadow-sm sm:w-auto"
        aria-haspopup="listbox"
        aria-expanded={open}
      >
        <span className="flex min-w-0 items-center gap-2">
          <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-primary-50 text-primary">
            <History className="h-4 w-4" />
          </span>
          <span className="min-w-0">
            <span className="block text-[10px] font-bold uppercase tracking-wide text-slate-400">
              Workspace
            </span>
            <span className="block truncate text-sm font-bold text-slate-800">
              {selectedLabel}
            </span>
          </span>
        </span>

        <span className="flex shrink-0 items-center gap-2">
          {selectedWorkspace.isArchived ? (
            <span className="hidden items-center gap-1 rounded-full bg-amber-50 px-2 py-0.5 text-[10px] font-bold text-amber-700 sm:inline-flex">
              <Archive className="h-3 w-3" />
              Archived
            </span>
          ) : (
            <span className="hidden rounded-full bg-emerald-50 px-2 py-0.5 text-[10px] font-bold text-emerald-700 sm:inline-flex">
              Current
            </span>
          )}
          <ChevronDown className={`h-4 w-4 text-slate-400 transition-transform ${open ? 'rotate-180' : ''}`} />
        </span>
      </button>

      {open && (
        <div className="absolute right-0 z-50 mt-2 w-full min-w-[320px] overflow-hidden rounded-xl border border-slate-200 bg-white shadow-xl sm:w-[360px]">
          <div className="border-b border-slate-100 px-3 py-2">
            <p className="text-xs font-bold text-slate-800">Switch workspace</p>
            <p className="text-[11px] text-slate-400">Move between detected EXE101, EXE201, and archived startup workspaces.</p>
          </div>

          <div className="max-h-72 overflow-y-auto p-1" role="listbox" aria-label="Available workspaces">
            {availableWorkspaces.map((workspace) => {
              const isSelected = String(workspace.teamId) === selectedTeamId;
              const label = [workspace.courseCode || 'Workspace', workspace.semester].filter(Boolean).join(' - ');
              const subtitle = [workspace.classCode, workspace.teamName].filter(Boolean).join(' | ');

              return (
                <button
                  key={workspace.teamId}
                  type="button"
                  onClick={() => handleSelect(workspace.teamId)}
                  className={`flex w-full items-center justify-between gap-3 rounded-lg px-3 py-2.5 text-left transition-colors ${
                    isSelected ? 'bg-primary-50 text-primary' : 'text-slate-700 hover:bg-slate-50'
                  }`}
                  role="option"
                  aria-selected={isSelected}
                >
                  <span className="flex min-w-0 items-center gap-2.5">
                    <span className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg ${
                      workspace.isArchived ? 'bg-amber-50 text-amber-700' : 'bg-emerald-50 text-emerald-700'
                    }`}>
                      {workspace.isArchived ? <Archive className="h-4 w-4" /> : <Clock3 className="h-4 w-4" />}
                    </span>
                    <span className="min-w-0">
                      <span className="block truncate text-sm font-bold">{label}</span>
                      {subtitle && (
                        <span className="block truncate text-xs text-slate-400">{subtitle}</span>
                      )}
                    </span>
                  </span>

                  <span className="flex shrink-0 items-center gap-2">
                    <span className={`rounded-full px-2 py-0.5 text-[10px] font-bold ${
                      workspace.accessMode === 'READ_ONLY'
                        ? 'bg-amber-50 text-amber-700'
                        : 'bg-emerald-50 text-emerald-700'
                    }`}>
                      {workspace.accessMode === 'READ_ONLY' ? 'Read-only' : 'Editable'}
                    </span>
                    {isSelected && <Check className="h-4 w-4" />}
                  </span>
                </button>
              );
            })}
          </div>

          {!hasMultipleWorkspaces && (
            <div className="px-3 pb-3 text-xs text-slate-400">
              No related workspace detected yet.
            </div>
          )}
        </div>
      )}

      {!hasMultipleWorkspaces && (
        <p className="mt-1 text-[11px] font-medium text-slate-400">
          No related EXE101/EXE201 workspace detected yet.
        </p>
      )}
    </div>
  );
}
