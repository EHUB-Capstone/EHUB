import { useState } from 'react';
import { Flag } from 'lucide-react';
import toast from 'react-hot-toast';
import Button from '../ui/Button';
import Modal from '../ui/Modal';
import { showcaseApi } from '../../api/showcaseApi';
import { parseApiError } from '../../utils/apiError';
import type { ShowcaseReportReason } from '../../types/showcase';

const REASONS: Array<{ value: ShowcaseReportReason; label: string }> = [
  { value: 'Spam', label: 'Spam or scam' },
  { value: 'Harassment', label: 'Harassment or hate' },
  { value: 'InappropriateContent', label: 'Inappropriate content' },
  { value: 'CopyrightViolation', label: 'Copyright violation' },
  { value: 'Other', label: 'Other' },
];

export default function ShowcaseReportButton({ showcaseId, commentId }: { showcaseId: string; commentId?: string }) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState<ShowcaseReportReason>('InappropriateContent');
  const [note, setNote] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  const submit = async () => {
    if (submitting) return;
    setSubmitting(true);
    try {
      await showcaseApi.createReport(showcaseId, { commentId: commentId ?? null, reason, note: note.trim() || null });
      toast.success('Report submitted. Thank you for helping keep the community safe.');
      setSubmitted(true);
      setOpen(false);
      setNote('');
    } catch (error) {
      toast.error(parseApiError(error, 'Could not submit the report. Try again.').message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <Button size="sm" variant="outline" icon={Flag} disabled={submitted} onClick={() => setOpen(true)}>{submitted ? 'Reported' : 'Report'}</Button>
      <Modal isOpen={open} onClose={() => { if (!submitting) setOpen(false); }} title={commentId ? 'Report comment' : 'Report showcase'}
        submitText="Submit report" isSubmitting={submitting} onSubmit={submit}>
        <div className="space-y-4">
          <p className="text-body-sm text-slate-600">Reports are reviewed by authorised staff. Your identity is not shown to the project team.</p>
          <label className="block text-body-sm font-semibold text-secondary">Reason
            <select value={reason} onChange={(event) => setReason(event.target.value as ShowcaseReportReason)}
              className="mt-1.5 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-body">
              {REASONS.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
            </select>
          </label>
          <label className="block text-body-sm font-semibold text-secondary">Note <span className="font-normal text-slate-500">(optional)</span>
            <textarea value={note} onChange={(event) => setNote(event.target.value)} maxLength={1000} rows={4}
              className="mt-1.5 w-full resize-none rounded-lg border border-slate-300 px-3 py-2 text-body" placeholder="Add context for the reviewer" />
          </label>
        </div>
      </Modal>
    </>
  );
}
