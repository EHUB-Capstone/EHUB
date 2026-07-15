// @ts-nocheck
import { useMemo, useState } from 'react';
import { FileText, Loader2, Save } from 'lucide-react';
import toast from 'react-hot-toast';
import { workspaceApi } from '../../api/workspaceApi';

const countWords = (value) => {
  const clean = value.trim();
  return clean ? clean.split(/\s+/u).length : 0;
};

const statusStyles = {
  PENDING: 'bg-amber-50 text-amber-700 border-amber-200',
  APPROVED: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  CHANGES_REQUESTED: 'bg-red-50 text-red-700 border-red-200',
};

const statusLabels = {
  PENDING: 'Pending review',
  APPROVED: 'Approved',
  CHANGES_REQUESTED: 'Changes requested',
};

export default function ProjectDirectionCard({ team, canEdit, onSaved }) {
  const [value, setValue] = useState(team?.projectDirection || '');
  const [saving, setSaving] = useState(false);
  const wordCount = useMemo(() => countWords(value), [value]);
  const isValid = wordCount >= 30 && wordCount <= 500;
  const isUnchanged = value.trim() === (team?.projectDirection || '').trim();

  const handleSave = async () => {
    if (!isValid || isUnchanged) return;
    setSaving(true);
    try {
      await workspaceApi.updateProjectDirection(team._id, value.trim());
      toast.success('Project direction saved');
      await onSaved?.();
    } catch (error) {
      toast.error(error?.response?.data?.error || error?.message || 'Unable to save project direction');
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6">
      <div className="flex items-start justify-between gap-4 border-b border-slate-100 pb-4">
        <div className="flex items-center gap-2">
          <FileText className="h-5 w-5 text-primary" />
          <div>
            <h2 className="text-lg font-bold text-slate-800">Project Direction</h2>
            <p className="mt-0.5 text-xs text-slate-500">Describe what the team is building and its implementation direction.</p>
          </div>
        </div>
        <div className="flex shrink-0 flex-col items-end gap-1.5">
          {team?.projectDirectionStatus && team.projectDirectionStatus !== 'NOT_SUBMITTED' && (
            <span className={`rounded-full border px-2.5 py-1 text-xs font-semibold ${statusStyles[team.projectDirectionStatus] || 'border-slate-200 bg-slate-50 text-slate-600'}`}>
              {statusLabels[team.projectDirectionStatus] || team.projectDirectionStatus}
            </span>
          )}
          {team?.projectDirectionUpdatedAt && (
            <span className="text-xs text-slate-400">
              Updated {new Date(team.projectDirectionUpdatedAt).toLocaleDateString('en-GB')}
            </span>
          )}
        </div>
      </div>

      {canEdit ? (
        <div className="mt-4 space-y-3">
          <textarea
            value={value}
            onChange={(event) => setValue(event.target.value)}
            rows={8}
            placeholder="Describe the problem, target users, proposed solution, and development direction..."
            className="w-full resize-y rounded-xl border border-slate-200 px-4 py-3 text-sm leading-6 text-slate-700 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15"
          />
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className={`text-xs font-medium ${wordCount > 0 && !isValid ? 'text-red-500' : 'text-slate-500'}`}>
              {wordCount}/500 words · Minimum 30 words
            </p>
            <button
              type="button"
              onClick={handleSave}
              disabled={saving || !isValid || isUnchanged}
              className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition hover:bg-primary-700 disabled:cursor-not-allowed disabled:bg-slate-200 disabled:text-slate-500"
            >
              {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
              Save Direction
            </button>
          </div>
        </div>
      ) : (
        <div className="mt-4">
          {team?.projectDirection ? (
            <p className="whitespace-pre-wrap text-sm leading-7 text-slate-700">{team.projectDirection}</p>
          ) : (
            <p className="rounded-xl border border-dashed border-slate-200 bg-slate-50 px-4 py-6 text-center text-sm text-slate-500">
              The team leader has not updated the project direction yet.
            </p>
          )}
        </div>
      )}

      {team?.projectDirectionReviewComment && (
        <div className="mt-4 rounded-xl border border-blue-100 bg-blue-50 px-4 py-3">
          <p className="text-xs font-bold uppercase text-blue-700">Lecturer comment</p>
          <p className="mt-1 whitespace-pre-wrap text-sm leading-6 text-blue-900">
            {team.projectDirectionReviewComment}
          </p>
          {team.projectDirectionReviewedAt && (
            <p className="mt-2 text-xs text-blue-600">
              Reviewed {new Date(team.projectDirectionReviewedAt).toLocaleDateString('en-GB')}
            </p>
          )}
        </div>
      )}
    </section>
  );
}
