import { useCallback, useEffect, useState } from 'react';
import { AlertCircle, AlertTriangle, ArrowLeft, ArrowRight, Pencil, RotateCcw, Trash2, UserX } from 'lucide-react';
import toast from 'react-hot-toast';
import { mentorAdminApi } from '../../api/mentorAdminApi';
import type { IncompleteMentor, IncompleteMentorList } from '../../types/mentorAdmin';
import { parseApiError } from '../../utils/apiError';
import { summarizeMissingFields } from '../../utils/mentorMasterImport';
import Button from '../ui/Button';
import ConfirmDialog from '../ui/ConfirmDialog';
import IncompleteMentorEditModal from './IncompleteMentorEditModal';
import MentorKindTag from './MentorKindTag';
import TemporaryMentorBadge from './TemporaryMentorBadge';
import EmptyState from '../ui/EmptyState';
import LoadingSkeleton from '../ui/LoadingSkeleton';
import { useTranslation } from 'react-i18next';

interface IncompleteMentorsPanelProps {
  search: string;
  /** 'ALL', 'Enterprise' (enterprise mentors) or 'Academic' (lecturer mentors). */
  mentorType: string;
  /** Called with the unfiltered total so the tab badge stays accurate. */
  onUnfilteredTotal?: (total: number) => void;
  /** Called after a mentor was edited or deleted, so the parent can refresh its own numbers. */
  onChanged?: () => void;
}

const PAGE_SIZE = 10;

/** Mentors saved without a login account yet. Mount it with a `key` that changes with the filters to restart at page 1. */
export default function IncompleteMentorsPanel({ search, mentorType, onUnfilteredTotal, onChanged }: IncompleteMentorsPanelProps) {
  const { t } = useTranslation('admin');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<IncompleteMentorList | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<IncompleteMentor | null>(null);
  const [deleting, setDeleting] = useState<IncompleteMentor | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);

  const load = useCallback(async (isCancelled: () => boolean = () => false) => {
    setLoading(true);
    setError('');
    try {
      const response = await mentorAdminApi.getIncompleteMasterMentors({
        page,
        limit: PAGE_SIZE,
        ...(search ? { search } : {}),
        ...(mentorType !== 'ALL' ? { mentorType } : {}),
      });
      if (isCancelled()) return;
      setData(response.data);
      if (!search && mentorType === 'ALL') onUnfilteredTotal?.(response.data.pagination.total);
    } catch (requestError: unknown) {
      if (!isCancelled()) setError(parseApiError(requestError, 'Failed to load mentors that need information.').message);
    } finally {
      if (!isCancelled()) setLoading(false);
    }
  }, [page, search, mentorType, onUnfilteredTotal]);

  useEffect(() => {
    let cancelled = false;
    void load(() => cancelled);
    return () => { cancelled = true; };
  }, [load]);

  const confirmDelete = async () => {
    if (!deleting || deleteBusy) return;
    setDeleteBusy(true);
    try {
      await mentorAdminApi.deleteIncompleteMentor(deleting.id);
      toast.success(`${deleting.fullName} deleted.`);
      setDeleting(null);
      // The last row of a later page may have been removed, so step back instead of showing an empty page.
      if (mentors.length === 1 && page > 1) setPage(page - 1); else await load();
      onChanged?.();
    } catch (requestError: unknown) {
      // The reason (for example teams still using the mentor) is shown as is; the dialog closes so the list can be fixed first.
      toast.error(parseApiError(requestError, 'Failed to delete the mentor').message, { duration: 8000 });
      setDeleting(null);
    } finally {
      setDeleteBusy(false);
    }
  };

  const mentors: IncompleteMentor[] = data?.mentors ?? [];
  const totalPages = data?.pagination.pages ?? 1;

  return (
    <div className="bg-white border border-slate-200/60 rounded-2xl shadow-sm overflow-hidden">
      <div className="flex items-start gap-2.5 border-b border-slate-100 bg-amber-50/60 px-6 py-3 text-xs leading-5 text-amber-900">
        <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
        <p>{t('incompleteMentorsPanel.theseMentorsWereImportedWithoutA')}</p>
      </div>

      {error ? (
        <div className="flex flex-col items-center gap-3 p-10 text-center">
          <AlertCircle className="h-6 w-6 text-red-500" />
          <p className="text-sm text-red-700">{error}</p>
          <Button variant="outline" size="sm" icon={RotateCcw} onClick={() => void load()}>{t('classManagement.retry')}</Button>
        </div>
      ) : loading && !data ? (
        <div className="p-6"><LoadingSkeleton lines={6} /></div>
      ) : mentors.length === 0 ? (
        <div className="p-12"><EmptyState icon={UserX} title={t('incompleteMentorsPanel.noMentorsNeedInformation')} description={search || mentorType !== 'ALL' ? t('userManagement.tryAdjustingYourSearchOrFilters') : t('incompleteMentorsPanel.everyImportedMentorHasALogin')} /></div>
      ) : (
        <div className={`overflow-x-auto transition-opacity duration-200 ${loading ? 'pointer-events-none opacity-60' : 'opacity-100'}`} aria-busy={loading}>
          <table className="w-full min-w-[640px]">
            <thead>
              <tr className="border-b border-slate-100 bg-slate-50/60">
                <th className="py-3.5 px-6 text-xs text-slate-600 uppercase text-left font-semibold tracking-wider">{t('subjectManagement.mentor')}</th>
                <th className="py-3.5 px-6 text-xs text-slate-600 uppercase text-left font-semibold tracking-wider">{t('mentorProfileDetail.status')}</th>
                <th className="py-3.5 px-6 text-xs text-slate-600 uppercase text-left font-semibold tracking-wider">{t('incompleteMentorsPanel.missingInformation')}</th>
                <th className="sticky right-0 bg-slate-50 py-3.5 px-4 text-xs text-slate-600 uppercase text-right font-semibold tracking-wider">{t('startupIndustryManagement.actions')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {mentors.map(mentor => {
                const missing = summarizeMissingFields(mentor.missingFields);
                return (
                  <tr key={mentor.id} className="hover:bg-primary-50/20 transition-colors">
                    <td className="py-3.5 px-6">
                      <div className="flex flex-col items-start gap-1">
                        <span className="font-semibold text-slate-900">{mentor.fullName}</span>
                        <div className="flex flex-wrap items-center gap-1.5">
                          <MentorKindTag type={mentor.mentorType} />
                          {(mentor.activeTeamCount ?? 0) > 0 && (
                            <span className="inline-flex items-center gap-1">
                              <TemporaryMentorBadge />
                              <span className="text-overline font-semibold text-amber-800">{t('incompleteMentorsPanel.onTeams', { count: mentor.activeTeamCount })}</span>
                            </span>
                          )}
                        </div>
                      </div>
                    </td>
                    <td className="py-3.5 px-6">
                      <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-caption font-semibold text-amber-800">
                        <AlertTriangle className="h-3 w-3" /> {t('subjectManagement.needsInformation')}
                      </span>
                    </td>
                    <td className="py-3.5 px-6 text-xs text-slate-600" title={mentor.missingFields.join(', ')}>
                      <span className="font-semibold text-slate-800">{t('incompleteMentorsPanel.fieldsMissing', { count: missing.count })}</span>
                      {missing.preview.length > 0 && (
                        <span className="ml-1 text-slate-500">· {missing.preview.join(', ')}{missing.hiddenCount > 0 ? t('incompleteMentorsPanel.more', { hiddenCount: missing.hiddenCount }) : ''}</span>
                      )}
                    </td>
                    <td className="sticky right-0 bg-white py-3.5 px-4 text-right shadow-[-8px_0_8px_-8px_rgba(15,23,42,0.12)]">
                      <div className="flex items-center justify-end gap-1.5">
                        <button
                          type="button"
                          aria-label={t('incompleteMentorsPanel.edit', { fullName: mentor.fullName })}
                          title={t('incompleteMentorsPanel.editMentor')}
                          className="rounded-lg p-1.5 text-slate-400 transition-all hover:bg-primary-50 hover:text-primary-700"
                          onClick={() => setEditing(mentor)}
                        >
                          <Pencil className="h-4 w-4" />
                        </button>
                        <button
                          type="button"
                          aria-label={t('incompleteMentorsPanel.delete', { fullName: mentor.fullName })}
                          title={t('incompleteMentorsPanel.deleteMentor')}
                          className="rounded-lg p-1.5 text-slate-400 transition-all hover:bg-red-50 hover:text-red-500"
                          onClick={() => setDeleting(mentor)}
                        >
                          <Trash2 className="h-4 w-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {editing && (
        <IncompleteMentorEditModal
          mentor={editing}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); void load(); onChanged?.(); }}
        />
      )}
      <ConfirmDialog
        isOpen={deleting !== null}
        onClose={() => { if (!deleteBusy) setDeleting(null); }}
        onConfirm={confirmDelete}
        title={t('incompleteMentorsPanel.deleteMentor')}
        description={t('incompleteMentorsPanel.deleteConfirm', { name: deleting?.fullName ?? t('incompleteMentorsPanel.thisMentor') })}
        isSubmitting={deleteBusy}
      />

      {!error && totalPages > 1 && (
        <div className="border-t border-slate-100 px-6 py-3.5 bg-slate-50/50 flex items-center justify-between">
          <span className="text-xs text-slate-500">
            {t('classManagement.page')} <span className="font-semibold text-slate-900">{page}</span> {t('classManagement.of')} <span className="font-semibold text-slate-900">{totalPages}</span> ({data?.pagination.total ?? 0} {t('userManagement.items')}
          </span>
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" disabled={loading || page === 1} onClick={() => setPage(page - 1)}>
              <ArrowLeft className="w-4 h-4 mr-1" /> {t('userManagement.prev')}
            </Button>
            <Button variant="outline" size="sm" disabled={loading || page === totalPages} onClick={() => setPage(page + 1)}>
              {t('common:actions.next')} <ArrowRight className="w-4 h-4 ml-1" />
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
