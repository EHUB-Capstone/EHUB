import { useEffect, useState, type FormEvent } from 'react';
import Modal from '../../ui/Modal';
import Button from '../../ui/Button';
import type { ProjectShortcut, SaveShortcutPayload } from '../../../types/workspaceTools';

const DANGEROUS_PROTOCOLS = ['javascript:', 'data:', 'file:', 'vbscript:'];
const normalizeUrl = (raw: string) => /^[a-zA-Z][a-zA-Z\d+.-]*:\/\//.test(raw.trim()) ? raw.trim() : `https://${raw.trim()}`;

function isValidUrl(raw: string): boolean {
  const lower = raw.trim().toLowerCase();
  if (!lower || DANGEROUS_PROTOCOLS.some(protocol => lower.startsWith(protocol))) return false;
  try {
    const parsed = new URL(normalizeUrl(raw));
    return parsed.protocol === 'http:' || parsed.protocol === 'https:';
  } catch { return false; }
}

interface AddShortcutModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (payload: SaveShortcutPayload) => void | Promise<void>;
  initialData?: ProjectShortcut | null;
  isSaving: boolean;
}

export default function AddShortcutModal({ isOpen, onClose, onSubmit, initialData = null, isSaving }: AddShortcutModalProps) {
  const [url, setUrl] = useState('');
  const [name, setName] = useState('');
  const [urlError, setUrlError] = useState('');

  useEffect(() => {
    if (!isOpen) return;
    setUrl(initialData?.url ?? '');
    setName(initialData?.name ?? '');
    setUrlError('');
  }, [isOpen, initialData]);

  const canSubmit = isValidUrl(url) && name.trim().length > 0 && !isSaving;
  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (canSubmit) void onSubmit({ url: normalizeUrl(url), name: name.trim(), shortcutType: initialData?.shortcutType ?? 'OTHER' });
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={initialData ? 'Edit shortcut' : 'Add shortcut'}>
      <form onSubmit={submit} className="space-y-5">
        <label className="block space-y-1.5 text-sm font-medium text-slate-700">
          <span>Web address / URL <span className="text-danger">*</span></span>
          <input autoFocus value={url} onChange={event => { setUrl(event.target.value); setUrlError(''); }} onBlur={() => setUrlError(url.trim() && !isValidUrl(url) ? 'Enter a valid HTTP or HTTPS URL.' : '')} placeholder="github.com/org/project" className="w-full rounded-xl border border-slate-300 px-3 py-2 outline-none focus:border-primary focus:ring-2 focus:ring-primary/15" aria-invalid={Boolean(urlError)} />
          {urlError && <span className="block text-xs text-danger">{urlError}</span>}
        </label>
        <label className="block space-y-1.5 text-sm font-medium text-slate-700">
          <span>Name <span className="text-danger">*</span></span>
          <input value={name} onChange={event => setName(event.target.value)} maxLength={100} placeholder="GitHub Repository" className="w-full rounded-xl border border-slate-300 px-3 py-2 outline-none focus:border-primary focus:ring-2 focus:ring-primary/15" />
        </label>
        <div className="flex justify-end gap-3 pt-1">
          <Button variant="outline" onClick={onClose} disabled={isSaving}>Cancel</Button>
          <Button type="submit" isLoading={isSaving} disabled={!canSubmit}>{initialData ? 'Save' : 'Add'}</Button>
        </div>
      </form>
    </Modal>
  );
}
