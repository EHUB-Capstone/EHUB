// @ts-nocheck
import { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { Link2, MoreHorizontal, Pencil, Plus, RotateCcw, Trash2 } from 'lucide-react';
import { useAuth } from '../../../hooks/useAuth';
import AddShortcutModal from './AddShortcutModal';
import { shortcutApi } from './shortcutApi';

function displayUrl(url) {
  try {
    const parsed = new URL(url);
    return `${parsed.hostname}${parsed.pathname === '/' ? '' : parsed.pathname}`;
  } catch {
    return url;
  }
}

function ShortcutIcon() {
  return (
    <div className="w-8 h-8 flex items-center justify-center rounded-md bg-slate-100 border border-slate-200 flex-shrink-0">
      <Link2 className="w-4 h-4 text-slate-500" />
    </div>
  );
}

function ShortcutActions({ onEdit, onDelete }) {
  const [open, setOpen] = useState(false);

  return (
    <div className="relative">
      <button
        type="button"
        onClick={(e) => {
          e.preventDefault();
          setOpen((value) => !value);
        }}
        className="w-7 h-7 flex items-center justify-center rounded hover:bg-slate-100 text-slate-400 hover:text-slate-700"
        aria-label="Shortcut actions"
      >
        <MoreHorizontal className="w-4 h-4" />
      </button>

      {open && (
        <div className="absolute right-0 top-8 z-20 w-32 rounded-md border border-slate-200 bg-white py-1 shadow-lg">
          <button
            type="button"
            onClick={(e) => {
              e.preventDefault();
              setOpen(false);
              onEdit();
            }}
            className="flex w-full items-center gap-2 px-3 py-1.5 text-left text-xs font-medium text-slate-700 hover:bg-slate-50"
          >
            <Pencil className="w-3.5 h-3.5" /> Edit
          </button>
          <button
            type="button"
            onClick={(e) => {
              e.preventDefault();
              setOpen(false);
              onDelete();
            }}
            className="flex w-full items-center gap-2 px-3 py-1.5 text-left text-xs font-medium text-red-600 hover:bg-red-50"
          >
            <Trash2 className="w-3.5 h-3.5" /> Delete
          </button>
        </div>
      )}
    </div>
  );
}

export default function QuickShortcuts({ teamId, isEditable }) {
  const { user } = useAuth();
  const [shortcuts, setShortcuts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState(null);
  const [isSaving, setIsSaving] = useState(false);

  const loadShortcuts = useCallback(async () => {
    try {
      setLoading(true);
      setError('');
      const res = await shortcutApi.getAll(teamId);
      setShortcuts(res.data || []);
    } catch (err) {
      const message = err.message || 'Failed to load shortcuts.';
      setError(message);
      toast.error(message);
    } finally {
      setLoading(false);
    }
  }, [teamId]);

  useEffect(() => {
    // Fetching the team shortcuts is the side effect this component owns.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (teamId) loadShortcuts();
  }, [teamId, loadShortcuts]);

  const canManage = (shortcut) => {
    if (!user) return false;
    const role = user.role?.toUpperCase();
    if (['ADMIN', 'LECTURER', 'MENTOR'].includes(role)) return true;
    return shortcut.createdBy?._id === user._id || shortcut.createdBy === user._id;
  };

  const closeModal = () => {
    if (!isSaving) {
      setModalOpen(false);
      setEditing(null);
    }
  };

  const handleSubmit = async ({ url, name }) => {
    const previous = shortcuts;

    try {
      setIsSaving(true);

      if (editing) {
        const optimistic = { ...editing, url, name };
        setShortcuts((items) => items.map((item) => (item._id === editing._id ? optimistic : item)));
        const res = await shortcutApi.update(teamId, editing._id, { url, name });
        setShortcuts((items) => items.map((item) => (item._id === editing._id ? res.data : item)));
        toast.success('Shortcut updated.');
      } else {
        const tempId = `temp-${Date.now()}`;
        const optimistic = {
          _id: tempId,
          url,
          name,
          createdBy: { _id: user?._id, name: user?.name, avatar: user?.avatar },
        };
        setShortcuts((items) => [optimistic, ...items]);
        const res = await shortcutApi.create(teamId, { url, name });
        setShortcuts((items) => items.map((item) => (item._id === tempId ? res.data : item)));
        toast.success('Shortcut added.');
      }

      setModalOpen(false);
      setEditing(null);
    } catch (err) {
      setShortcuts(previous);
      toast.error(err.message || 'Something went wrong.');
    } finally {
      setIsSaving(false);
    }
  };

  const handleDelete = async (shortcut) => {
    if (!window.confirm(`Delete shortcut "${shortcut.name}"?`)) return;

    const previous = shortcuts;
    setShortcuts((items) => items.filter((item) => item._id !== shortcut._id));

    try {
      await shortcutApi.remove(teamId, shortcut._id);
      toast.success('Shortcut deleted.');
    } catch (err) {
      setShortcuts(previous);
      toast.error(err.message || 'Delete failed.');
    }
  };

  return (
    <div className="max-w-2xl px-1 py-2">
      <div className="flex items-center justify-between border-b border-slate-200 pb-3">
        <h2 className="text-base font-semibold text-slate-900">Quick Shortcuts</h2>
        {isEditable && (
          <button
            type="button"
            onClick={() => {
              setEditing(null);
              setModalOpen(true);
            }}
            className="inline-flex items-center gap-1.5 rounded-md bg-blue-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-blue-700 disabled:opacity-60"
          >
            <Plus className="w-3.5 h-3.5" /> Add Shortcut
          </button>
        )}
      </div>

      {loading ? (
        <div className="mt-3 space-y-2">
          {[0, 1, 2].map((item) => (
            <div key={item} className="flex items-center gap-3 rounded-md border border-slate-200 bg-white px-3 py-2.5">
              <div className="h-8 w-8 animate-pulse rounded-md bg-slate-100" />
              <div className="min-w-0 flex-1 space-y-2">
                <div className="h-3 w-1/3 animate-pulse rounded bg-slate-100" />
                <div className="h-2.5 w-1/2 animate-pulse rounded bg-slate-100" />
              </div>
            </div>
          ))}
        </div>
      ) : error ? (
        <div className="mt-4 rounded-md border border-red-100 bg-red-50 px-4 py-3">
          <p className="text-sm font-medium text-red-700">{error}</p>
          <button
            type="button"
            onClick={loadShortcuts}
            className="mt-2 inline-flex items-center gap-1.5 text-xs font-semibold text-red-700 hover:text-red-800"
          >
            <RotateCcw className="w-3.5 h-3.5" /> Retry
          </button>
        </div>
      ) : shortcuts.length === 0 ? (
        <div className="py-10 text-sm text-slate-500">
          <p className="font-medium text-slate-700">No shortcuts yet.</p>
          <p>Add your first resource shortcut for this workspace.</p>
        </div>
      ) : (
        <ul className="mt-3 overflow-visible rounded-md border border-slate-200 bg-white">
          {shortcuts.map((shortcut) => (
            <li
              key={shortcut._id}
              className="group flex items-center gap-3 border-b border-slate-100 px-3 py-2.5 last:border-b-0 hover:bg-slate-50"
            >
              <ShortcutIcon />
              <a
                href={shortcut.url}
                target="_blank"
                rel="noopener noreferrer"
                className="min-w-0 flex-1"
              >
                <p className="truncate text-sm font-medium text-slate-800 group-hover:text-blue-700">
                  {shortcut.name}
                </p>
                <p className="truncate text-xs text-slate-500">{displayUrl(shortcut.url)}</p>
              </a>
              {canManage(shortcut) && (
                <ShortcutActions
                  onEdit={() => {
                    setEditing(shortcut);
                    setModalOpen(true);
                  }}
                  onDelete={() => handleDelete(shortcut)}
                />
              )}
            </li>
          ))}
        </ul>
      )}

      <AddShortcutModal
        isOpen={modalOpen}
        onClose={closeModal}
        onSubmit={handleSubmit}
        initialData={editing}
        isSaving={isSaving}
      />
    </div>
  );
}
