import { useCallback, useEffect, useState } from 'react';
import { AlertCircle, Loader2, RotateCcw, X } from 'lucide-react';
import { mentorAdminApi } from '../../api/mentorAdminApi';
import type { IncompleteMentor } from '../../types/mentorAdmin';
import { parseApiError } from '../../utils/apiError';
import Button from '../ui/Button';

interface IncompleteMentorsModalProps {
  onClose: () => void;
}

/** Lists mentors kept in the master list without a login account yet, with what is still missing. */
export default function IncompleteMentorsModal({ onClose }: IncompleteMentorsModalProps) {
  const [mentors, setMentors] = useState<IncompleteMentor[] | null>(null);
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    setError('');
    setMentors(null);
    try {
      const response = await mentorAdminApi.getIncompleteMasterMentors();
      setMentors(response.data);
    } catch (requestError: unknown) {
      setError(parseApiError(requestError, 'Failed to load incomplete mentors.').message);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handleEscape);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener('keydown', handleEscape);
    };
  }, [onClose]);

  return (
    <div className="fixed inset-0 z-[70] flex items-end justify-center p-0 sm:items-center sm:p-6" role="dialog" aria-modal="true" aria-labelledby="incomplete-mentors-title">
      <button type="button" className="absolute inset-0 cursor-default bg-slate-900/45 backdrop-blur-sm" onClick={onClose} aria-label="Close incomplete mentors" />
      <div className="relative flex max-h-[94vh] w-full max-w-3xl flex-col overflow-hidden rounded-t-2xl border border-slate-200/60 bg-white shadow-float animate-scale-in sm:max-h-[85vh] sm:rounded-2xl">
        <header className="flex shrink-0 items-start justify-between gap-4 border-b border-slate-100 px-5 py-4 sm:px-6">
          <div>
            <h2 id="incomplete-mentors-title" className="text-lg font-bold text-slate-900">Incomplete mentors</h2>
            <p className="text-sm text-slate-500">Saved from an import without a login email. Import a file with their email to create their accounts.</p>
          </div>
          <button type="button" onClick={onClose} className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700" aria-label="Close"><X className="h-5 w-5" /></button>
        </header>

        <main className="flex-1 overflow-y-auto px-5 py-5 sm:px-6">
          {error ? (
            <div className="flex flex-col items-center gap-3 py-8 text-center">
              <AlertCircle className="h-6 w-6 text-red-500" />
              <p className="text-sm text-red-700">{error}</p>
              <Button variant="outline" size="sm" icon={RotateCcw} onClick={() => void load()}>Retry</Button>
            </div>
          ) : mentors === null ? (
            <div className="flex items-center justify-center gap-2 py-10 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" /> Loading incomplete mentors…</div>
          ) : mentors.length === 0 ? (
            <p className="py-10 text-center text-sm text-slate-500">No incomplete mentors. Every imported mentor has a login email.</p>
          ) : (
            <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200">
              {mentors.map(mentor => (
                <li key={mentor.id} className="px-4 py-3">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-semibold text-slate-800">{mentor.fullName}</span>
                    <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-600">{mentor.mentorType === 'Enterprise' ? 'Industry mentor' : 'Lecturer mentor'}</span>
                  </div>
                  {mentor.missingFields.length > 0 && <p className="mt-1 text-xs text-slate-500">Missing: {mentor.missingFields.join(', ')}</p>}
                </li>
              ))}
            </ul>
          )}
        </main>

        <footer className="flex shrink-0 justify-end border-t border-slate-100 bg-slate-50/60 px-5 py-4 sm:px-6">
          <Button variant="outline" onClick={onClose}>Close</Button>
        </footer>
      </div>
    </div>
  );
}
