import { useEffect, useState, type FormEvent } from 'react';
import { Link2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { checkpointApi } from '../../../api/checkpointApi';
import type { WorkspaceCheckpointLink } from '../../../types/workspaceCheckpoints';
import { parseApiError } from '../../../utils/apiError';
import {
  CHECKPOINT_LINK_NAME_MAX_LENGTH,
  normalizeCheckpointLinkUrl,
  validateCheckpointLinkUrl,
} from '../../../utils/checkpointLink';
import Button from '../../ui/Button';

interface CheckpointLinkFormProps {
  teamId: string;
  checkpointNumber: number;
  editingLink?: WorkspaceCheckpointLink | null;
  onCancelEdit?: () => void;
  onSaved: () => void | Promise<void>;
}

export default function CheckpointLinkForm({
  teamId,
  checkpointNumber,
  editingLink = null,
  onCancelEdit,
  onSaved,
}: CheckpointLinkFormProps) {
  const [name, setName] = useState('');
  const [url, setUrl] = useState('');
  const [urlError, setUrlError] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setName(editingLink?.name ?? '');
    setUrl(editingLink?.url ?? '');
    setUrlError('');
  }, [editingLink]);

  const trimmedName = name.trim();
  const canSubmit = Boolean(trimmedName) && trimmedName.length <= CHECKPOINT_LINK_NAME_MAX_LENGTH &&
    !validateCheckpointLinkUrl(url) && !saving;

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const validationMessage = validateCheckpointLinkUrl(url);
    setUrlError(validationMessage ?? '');
    if (!canSubmit || validationMessage) return;

    setSaving(true);
    try {
      const payload = { name: trimmedName, url: normalizeCheckpointLinkUrl(url) };
      if (editingLink) {
        await checkpointApi.updateLink(teamId, checkpointNumber, editingLink._id, payload);
      } else {
        await checkpointApi.createLink(teamId, checkpointNumber, payload);
      }
      toast.success(editingLink ? 'Submitted link updated.' : 'Link submitted.');
      setName('');
      setUrl('');
      setUrlError('');
      onCancelEdit?.();
      await onSaved();
    } catch (error) {
      toast.error(parseApiError(error, 'Unable to save the submitted link.').message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={submit} className="space-y-4">
      <div className="grid gap-4 md:grid-cols-2">
        <label className="space-y-1.5 text-sm font-semibold text-slate-700">
          <span>Link name <span className="text-red-500">*</span></span>
          <input
            value={name}
            onChange={(event) => setName(event.target.value)}
            autoFocus={Boolean(editingLink)}
            maxLength={CHECKPOINT_LINK_NAME_MAX_LENGTH}
            placeholder="Prototype demo"
            className="w-full rounded-xl border border-slate-300 px-3 py-2.5 outline-none focus:border-primary focus:ring-2 focus:ring-primary/15"
          />
        </label>
        <label className="space-y-1.5 text-sm font-semibold text-slate-700">
          <span>Public HTTPS link <span className="text-red-500">*</span></span>
          <input
            value={url}
            onChange={(event) => { setUrl(event.target.value); setUrlError(''); }}
            onBlur={() => setUrlError(validateCheckpointLinkUrl(url) ?? '')}
            placeholder="https://drive.google.com/..."
            inputMode="url"
            aria-invalid={Boolean(urlError)}
            className="w-full rounded-xl border border-slate-300 px-3 py-2.5 outline-none focus:border-primary focus:ring-2 focus:ring-primary/15"
          />
          {urlError && <span className="block text-xs font-normal text-red-600">{urlError}</span>}
        </label>
      </div>
      <div className="flex items-center justify-between gap-3">
        <p className="text-xs text-slate-500">Only public HTTPS links are accepted · Maximum 10 active links</p>
        <div className="flex gap-2">
          {editingLink && <Button type="button" variant="outline" onClick={onCancelEdit} disabled={saving}>Cancel</Button>}
          <Button type="submit" icon={Link2} isLoading={saving} disabled={!canSubmit}>
            {editingLink ? 'Save link' : 'Submit link'}
          </Button>
        </div>
      </div>
    </form>
  );
}
