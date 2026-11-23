import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import { Search, Sparkles } from 'lucide-react';
import { mentorAdminApi } from '../../api/mentorAdminApi';
import type { MentorSemesterClass } from '../../types/mentorAdmin';
import { parseApiError } from '../../utils/apiError';
import { filterSemesterClasses, missingSlots, totalMissingSlots } from '../../utils/semesterMentorClasses';
import AssignMentorsModal from '../class/AssignMentorsModal';
import Button from '../ui/Button';
import LoadingSkeleton from '../ui/LoadingSkeleton';
import Modal from '../ui/Modal';
import TemporaryMentorBadge from './TemporaryMentorBadge';
import { useTranslation } from 'react-i18next';

interface SemesterMentorManagerProps {
  isOpen: boolean;
  semesterId: string;
  semesterLabel: string;
  onClose: () => void;
  /** Called after any mentor change so the parent can refresh the semester data. */
  onChanged?: () => Promise<void> | void;
  /** Opens an AI proposal scoped to this class, for admin review before saving. */
  onAiPreview?: (item: MentorSemesterClass) => void;
}

/** Lets an admin pick any active class of the semester and edit its team mentors without opening the class page. */
export default function SemesterMentorManager({ isOpen, semesterId, semesterLabel, onClose, onChanged, onAiPreview }: SemesterMentorManagerProps) {
  const { t } = useTranslation('admin');
  const [classes, setClasses] = useState<MentorSemesterClass[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadFailed, setLoadFailed] = useState(false);
  const [search, setSearch] = useState('');
  const [subject, setSubject] = useState('ALL');
  const [onlyMissing, setOnlyMissing] = useState(false);
  const [selected, setSelected] = useState<MentorSemesterClass | null>(null);
  const mounted = useRef(true);

  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);

  const load = useCallback(async (quiet: boolean) => {
    if (!quiet) setLoading(true);
    setLoadFailed(false);
    try {
      const response = await mentorAdminApi.getSemesterClasses(semesterId);
      if (mounted.current) setClasses(response.data?.classes ?? []);
    } catch (error) {
      if (!mounted.current) return;
      setLoadFailed(true);
      toast.error(parseApiError(error, t('semesterMentorManager.failedToLoadTheClassesOf')).message);
    } finally {
      if (mounted.current) setLoading(false);
    }
  }, [semesterId]);

  // Every time the dialog opens it starts clean and reads the current numbers.
  useEffect(() => {
    if (!isOpen) return;
    setSearch('');
    setSubject('ALL');
    setOnlyMissing(false);
    setSelected(null);
    void load(false);
  }, [isOpen, load]);

  const subjects = useMemo(() => Array.from(new Set(classes.map(item => item.subjectCode))).sort(), [classes]);
  const visible = useMemo(() => filterSemesterClasses(classes, { search, subject, onlyMissing }), [classes, search, subject, onlyMissing]);
  const missingTotal = totalMissingSlots(classes);

  // The class dialog replaces the list; closing it returns to the list with fresh numbers.
  if (isOpen && selected) {
    return (
      <AssignMentorsModal
        classId={selected.classId}
        onClose={() => { setSelected(null); void load(true); }}
        onAssigned={async () => { await onChanged?.(); }}
      />
    );
  }

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={t('semesterMentorManager.manageMentors', { semesterLabel })} size="xl" submitText={t('common:actions.close')} onSubmit={onClose}>
      <div className="space-y-3">
        <p className="rounded-xl border border-blue-200 bg-blue-50 px-3 py-2 text-xs leading-5 text-blue-700">
          {t('semesterMentorManager.pickAClass')}
          {onAiPreview && ` ${t('semesterMentorManager.aiPreviewHint')}`}
        </p>

        <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
          <div className="relative min-w-0 flex-1">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <input
              type="search"
              value={search}
              onChange={event => setSearch(event.target.value)}
              placeholder={t('semesterMentorManager.searchClassSubjectOrLecturer')}
              aria-label={t('semesterMentorManager.searchClasses')}
              className="w-full rounded-xl border border-slate-200 bg-white py-2 pl-9 pr-3 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            />
          </div>
          <select
            value={subject}
            onChange={event => setSubject(event.target.value)}
            aria-label={t('semesterMentorManager.filterBySubject')}
            className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary"
          >
            <option value="ALL">{t('semesterMentorManager.allSubjects')}</option>
            {subjects.map(code => <option key={code} value={code}>{code}</option>)}
          </select>
          <label className="flex cursor-pointer items-center gap-2 whitespace-nowrap text-xs font-semibold text-slate-700">
            <input type="checkbox" checked={onlyMissing} onChange={event => setOnlyMissing(event.target.checked)} className="h-3.5 w-3.5 accent-primary" />
            {t('semesterMentorManager.onlyClassesMissingMentors')}
          </label>
        </div>

        {loading ? (
          <LoadingSkeleton variant="text" lines={5} />
        ) : loadFailed ? (
          <div className="rounded-xl bg-red-50 px-3 py-4 text-center text-sm text-red-700">
            <p>{t('semesterMentorManager.couldNotLoadTheClasses')}</p>
            <Button size="sm" variant="outline" className="mt-2" onClick={() => void load(false)}>{t('addSemesterStaffModal.tryAgain')}</Button>
          </div>
        ) : classes.length === 0 ? (
          <p className="py-8 text-center text-xs text-slate-500">{t('semesterMentorManager.thisSemesterHasNoActiveClasses')}</p>
        ) : (
          <>
            <p className="text-xs text-slate-500" aria-live="polite">
              <strong className="text-slate-700">{visible.length}</strong> {t('classManagement.of')} {classes.length} {t('classManagement.classes2')}
              {' · '}
              {missingTotal > 0 ? <strong className="text-amber-700">{t('semesterMentorManager.openMentorSlots', { count: missingTotal })}</strong> : <span className="text-green-700">{t('semesterMentorManager.everySlotHasAMentor')}</span>}
            </p>
            <div className="max-h-[48vh] space-y-1.5 overflow-y-auto rounded-xl border border-slate-100 bg-slate-50/50 p-2">
              {visible.length === 0 ? (
                <p className="py-6 text-center text-xs text-slate-500">{t('semesterMentorManager.noClassMatchesTheCurrentFilters')}</p>
              ) : visible.map(item => {
                const missing = missingSlots(item);
                return (
                  <div key={item.classId} className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200/60 bg-white p-2.5">
                    <div className="min-w-0">
                      <p className="flex flex-wrap items-center gap-1.5 text-sm font-semibold text-slate-800">
                        {item.classCode}
                        <span className="rounded-full bg-slate-100 px-2 py-0 text-overline font-semibold text-slate-600">{item.subjectCode}</span>
                        {item.temporarySlotCount > 0 && <TemporaryMentorBadge />}
                      </p>
                      <p className="truncate text-caption text-slate-500">
                        {item.lecturerName || t('semesterMentorManager.noLecturer')} · {t('semesterMentorManager.teamCount', { count: item.teamCount })}
                      </p>
                    </div>
                    <div className="flex shrink-0 items-center gap-2">
                      {item.teamCount === 0 ? (
                        <span className="text-caption text-slate-400">{t('semesterMentorManager.noTeams')}</span>
                      ) : missing > 0 ? (
                        <span className="rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-caption font-semibold text-amber-800" title={t('semesterMentorManager.enterpriseAndLecturerMentorSlotS', { missingEnterpriseCount: item.missingEnterpriseCount, missingAcademicCount: item.missingAcademicCount })}>
                          {t('semesterMentorManager.missing', { missing })}
                        </span>
                      ) : (
                        <span className="rounded-full border border-green-200 bg-green-50 px-2 py-0.5 text-caption font-semibold text-green-700">{t('semesterMentorManager.complete')}</span>
                      )}
                      {onAiPreview && (
                        <Button size="sm" variant="outline" icon={Sparkles} disabled={item.teamCount === 0 || missing === 0} onClick={() => onAiPreview(item)} aria-label={t('semesterMentorManager.aiPreviewFor', { classCode: item.classCode })}>
                          {t('semesterMentorManager.aiPreview')}
                        </Button>
                      )}
                      <Button size="sm" variant="outline" disabled={item.teamCount === 0} onClick={() => setSelected(item)}>{t('semesterMentorManager.manage')}</Button>
                    </div>
                  </div>
                );
              })}
            </div>
          </>
        )}
      </div>
    </Modal>
  );
}
