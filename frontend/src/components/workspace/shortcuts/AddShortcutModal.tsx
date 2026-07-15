// @ts-nocheck
import { useEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';

const DANGEROUS_PROTOCOLS = ['javascript:', 'data:', 'file:', 'vbscript:'];
const hasProtocol = (value) => /^[a-zA-Z][a-zA-Z\d+.-]*:\/\//.test(value);

function isValidUrl(raw) {
  const trimmed = raw.trim();
  const lower = trimmed.toLowerCase();

  if (!trimmed) return false;
  if (DANGEROUS_PROTOCOLS.some((protocol) => lower.startsWith(protocol))) return false;

  try {
    const candidate = hasProtocol(trimmed) ? trimmed : `https://${trimmed}`;
    const parsed = new URL(candidate);
    return parsed.protocol === 'http:' || parsed.protocol === 'https:';
  } catch {
    return false;
  }
}

export default function AddShortcutModal({ isOpen, onClose, onSubmit, initialData = null, isSaving }) {
  const [url, setUrl] = useState('');
  const [name, setName] = useState('');
  const [urlError, setUrlError] = useState('');
  const urlRef = useRef(null);
  const isEdit = Boolean(initialData);

  useEffect(() => {
    if (!isOpen) return;
    // Form state must reset each time the dialog opens for add/edit modes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setUrl(initialData?.url ?? '');
    setName(initialData?.name ?? '');
    setUrlError('');
    window.setTimeout(() => urlRef.current?.focus(), 0);
  }, [isOpen, initialData]);

  useEffect(() => {
    const handleKeyDown = (event) => {
      if (event.key === 'Escape' && isOpen && !isSaving) onClose();
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, isSaving, onClose]);

  const urlOk = isValidUrl(url);
  const nameOk = name.trim().length > 0;
  const canSubmit = urlOk && nameOk && !isSaving;

  const handleUrlBlur = () => {
    if (!url.trim()) {
      setUrlError('');
      return;
    }

    setUrlError(urlOk ? '' : 'Enter a valid http or https URL.');
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    if (!canSubmit) return;
    onSubmit({ url: url.trim(), name: name.trim() });
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[1000] flex items-center justify-center bg-black/30">
      <div className="absolute inset-0" onClick={isSaving ? undefined : onClose} />

      <div className="relative mx-4 w-full max-w-[440px] rounded-lg border border-slate-200 bg-white shadow-xl">
        <button
          type="button"
          onClick={onClose}
          disabled={isSaving}
          className="absolute right-4 top-4 text-slate-400 hover:text-slate-700 disabled:opacity-50"
          aria-label="Close"
        >
          <X className="w-4 h-4" />
        </button>

        <form onSubmit={handleSubmit} className="space-y-5 p-6">
          <h2 className="text-lg font-semibold text-slate-900">
            {isEdit ? 'Edit shortcut' : 'Add shortcut'}
          </h2>

          <div className="space-y-1.5">
            <label className="text-sm font-medium text-slate-700">
              Web address / URL <span className="text-red-500">*</span>
            </label>
            <input
              ref={urlRef}
              type="text"
              value={url}
              onChange={(event) => {
                setUrl(event.target.value);
                if (urlError) setUrlError('');
              }}
              onBlur={handleUrlBlur}
              placeholder="github.com/org/project"
              className={`w-full rounded-md border px-3 py-2 text-sm outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-500 ${
                urlError ? 'border-red-400 bg-red-50/40' : 'border-slate-300 bg-white'
              }`}
            />
            {urlError && <p className="text-xs font-medium text-red-600">{urlError}</p>}
          </div>

          <div className="space-y-1.5">
            <label className="text-sm font-medium text-slate-700">
              Name <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder="GitHub Repository"
              maxLength={100}
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <div className="flex items-center justify-end gap-3 pt-1">
            <button
              type="button"
              onClick={onClose}
              disabled={isSaving}
              className="rounded-md px-3 py-1.5 text-sm font-semibold text-slate-600 hover:bg-slate-100 disabled:opacity-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={!canSubmit}
              className="rounded-md bg-blue-600 px-4 py-1.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-400"
            >
              {isSaving ? 'Saving...' : isEdit ? 'Save' : 'Add'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
