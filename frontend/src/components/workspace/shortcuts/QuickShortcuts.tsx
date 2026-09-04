import { useState } from 'react';
import { Link2, MoreHorizontal, Pencil, Plus, Trash2 } from 'lucide-react';
import { useAuth } from '../../../hooks/useAuth';
import { useWorkspaceShortcuts } from '../../../hooks/useWorkspaceShortcuts';
import type { ProjectShortcut, SaveShortcutPayload } from '../../../types/workspaceTools';
import Button from '../../ui/Button';
import ConfirmDialog from '../../ui/ConfirmDialog';
import EmptyState from '../../ui/EmptyState';
import ErrorState from '../../ui/ErrorState';
import AddShortcutModal from './AddShortcutModal';

const displayUrl = (url: string) => { try { const parsed = new URL(url); return `${parsed.hostname}${parsed.pathname === '/' ? '' : parsed.pathname}`; } catch { return url; } };

interface ShortcutActionsProps { onEdit: () => void; onDelete: () => void }
function ShortcutActions({ onEdit, onDelete }: ShortcutActionsProps) {
  const [open, setOpen] = useState(false);
  return <div className="relative">
    <Button variant="ghost" size="xs" onClick={event => { event.preventDefault(); setOpen(value => !value); }} aria-label="Shortcut actions" className="h-7 w-7 px-0"><MoreHorizontal className="h-4 w-4" /></Button>
    {open && <div className="absolute right-0 top-8 z-20 w-32 rounded-xl border border-slate-200 bg-white p-1 shadow-lg">
      <Button variant="ghost" size="xs" className="w-full justify-start" icon={Pencil} onClick={event => { event.preventDefault(); setOpen(false); onEdit(); }}>Edit</Button>
      <Button variant="ghost" size="xs" className="w-full justify-start text-danger" icon={Trash2} onClick={event => { event.preventDefault(); setOpen(false); onDelete(); }}>Delete</Button>
    </div>}
  </div>;
}

interface QuickShortcutsProps { teamId: string; isEditable: boolean; isReadOnly?: boolean }
export default function QuickShortcuts({ teamId, isEditable, isReadOnly = false }: QuickShortcutsProps) {
  const { user } = useAuth();
  const { data: shortcuts = [], isLoading, error, refetch, save, remove } = useWorkspaceShortcuts(teamId);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<ProjectShortcut | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ProjectShortcut | null>(null);
  const canManage = (shortcut: ProjectShortcut) => {
    if (!user || isReadOnly) return false;
    const role = user.role?.toUpperCase();
    if (role === 'MENTOR') return false;
    if (role === 'ADMIN' || role === 'LECTURER') return true;
    return shortcut.createdBy._id === user._id;
  };
  const closeModal = () => { if (!save.isPending) { setModalOpen(false); setEditing(null); } };
  const submit = async (payload: SaveShortcutPayload) => {
    try {
      await save.mutateAsync({ shortcut: editing, payload });
      setModalOpen(false);
      setEditing(null);
    } catch { /* The hook reports the error; preserve the form for retry. */ }
  };

  return <section className="max-w-2xl px-1 py-2" aria-labelledby="quick-shortcuts-heading">
    <div className="flex items-center justify-between border-b border-slate-200 pb-3">
      <h2 id="quick-shortcuts-heading" className="text-base font-semibold text-slate-900">Quick Shortcuts</h2>
      {isEditable && !isReadOnly && ['ADMIN', 'LECTURER', 'STUDENT'].includes(user?.role?.toUpperCase() ?? '') && <Button size="sm" icon={Plus} onClick={() => { setEditing(null); setModalOpen(true); }}>Add Shortcut</Button>}
    </div>
    {isLoading ? <div className="mt-3 space-y-2" aria-label="Loading shortcuts">{[0, 1, 2].map(item => <div key={item} className="h-14 animate-pulse rounded-xl bg-slate-100" />)}</div>
      : error ? <div className="mt-4"><ErrorState message="Unable to load shortcuts." onRetry={() => void refetch()} /></div>
      : shortcuts.length === 0 ? <EmptyState icon={Link2} title="No shortcuts yet" description="Add the first shared resource for this workspace." />
      : <ul className="mt-3 overflow-visible rounded-xl border border-slate-200 bg-white">{shortcuts.map(shortcut => <li key={shortcut._id} className="group flex items-center gap-3 border-b border-slate-100 px-3 py-2.5 last:border-b-0 hover:bg-slate-50">
        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border border-slate-200 bg-slate-100"><Link2 className="h-4 w-4 text-slate-500" /></div>
        <a href={shortcut.url} target="_blank" rel="noopener noreferrer" className="min-w-0 flex-1"><p className="truncate text-sm font-medium text-slate-800 group-hover:text-primary">{shortcut.name}</p><p className="truncate text-xs text-slate-500">{displayUrl(shortcut.url)}</p></a>
        {canManage(shortcut) && <ShortcutActions onEdit={() => { setEditing(shortcut); setModalOpen(true); }} onDelete={() => setDeleteTarget(shortcut)} />}
      </li>)}</ul>}
    <AddShortcutModal isOpen={modalOpen} onClose={closeModal} onSubmit={submit} initialData={editing} isSaving={save.isPending} />
    <ConfirmDialog isOpen={Boolean(deleteTarget)} onClose={() => { if (!remove.isPending) setDeleteTarget(null); }} onConfirm={async () => { try { if (deleteTarget) await remove.mutateAsync(deleteTarget); setDeleteTarget(null); } catch { /* Keep the dialog open for retry. */ } }} title="Delete shortcut?" description={`Delete “${deleteTarget?.name ?? ''}” from this workspace?`} isSubmitting={remove.isPending} />
  </section>;
}
