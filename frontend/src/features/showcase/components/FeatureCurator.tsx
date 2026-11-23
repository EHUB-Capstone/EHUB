import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Star } from 'lucide-react';
import toast from 'react-hot-toast';
import { showcaseApi } from '../../../api/showcaseApi';
import Button from '../../../components/ui/Button';
import ErrorState from '../../../components/ui/ErrorState';
import LoadingSkeleton from '../../../components/ui/LoadingSkeleton';
import type { ShowcaseStage } from '../../../types/showcase';
import { parseApiError } from '../../../utils/apiError';
import { filterCandidates, groupByClass, splitSelection } from '../../../utils/featureCandidates';
import { stageLabel } from '../../../utils/showcase';
import { useTranslation } from 'react-i18next';

const selectClass = 'h-10 min-w-0 rounded-lg border border-slate-300 bg-white px-3 text-body-sm text-slate-900 focus:border-primary-600 focus:outline-none focus:ring-2 focus:ring-primary/30 dark:border-slate-600 dark:bg-dark-surface dark:text-slate-100';

interface FeatureCuratorProps {
  isAdmin: boolean;
  /** Search and stage come from the gallery's own filters. */
  search?: string;
  stage?: ShowcaseStage;
}

/**
 * Bulk feature picker shown inside the Showcase page for lecturers and admins. The server returns only the
 * projects the caller may feature (a lecturer's own classes; everything for an admin), grouped here by class.
 */
export default function FeatureCurator({ isAdmin, search, stage }: FeatureCuratorProps): React.ReactElement {
  const { t } = useTranslation('showcase');
  const queryClient = useQueryClient();
  const candidates = useQuery({ queryKey: ['showcase', 'feature-candidates'], queryFn: ({ signal }) => showcaseApi.getFeatureCandidates(signal), staleTime: 0 });
  const [selected, setSelected] = useState<ReadonlySet<string>>(new Set());
  const [lecturer, setLecturer] = useState('');
  const [classCode, setClassCode] = useState('');
  const [isSaving, setIsSaving] = useState(false);

  const all = useMemo(() => candidates.data ?? [], [candidates.data]);
  const lecturers = useMemo(() => [...new Set(all.map(item => item.lecturerName).filter(Boolean))].sort((a, b) => a.localeCompare(b)), [all]);
  const classes = useMemo(() => [...new Set(all.filter(item => !lecturer || item.lecturerName === lecturer).map(item => item.classCode))], [all, lecturer]);
  const visible = useMemo(() => filterCandidates(all, { search, stage, lecturer: lecturer || undefined, classCode: classCode || undefined }), [all, search, stage, lecturer, classCode]);
  const groups = useMemo(() => groupByClass(visible), [visible]);
  const { toFeature, toUnfeature } = splitSelection(all, selected);

  const toggle = (ids: string[], on: boolean) => setSelected(current => {
    const next = new Set(current);
    for (const id of ids) { if (on) next.add(id); else next.delete(id); }
    return next;
  });

  const apply = async (ids: string[], featured: boolean) => {
    setIsSaving(true);
    try {
      const result = await showcaseApi.featureMany(ids, featured);
      await queryClient.invalidateQueries({ queryKey: ['showcase'] });
      const skipped = result.skipped > 0 ? ` ${result.skipped} could not be changed.` : '';
      toast.success(`${result.updated} ${result.updated === 1 ? 'project' : 'projects'} ${featured ? 'featured' : 'unfeatured'}.${skipped}`);
      setSelected(new Set());
    } catch (error) {
      toast.error(parseApiError(error, t('showcaseFeaturedToggle.unableToUpdateTheFeaturedSelection')).message);
    } finally {
      setIsSaving(false);
    }
  };

  if (candidates.isPending) return <div className="space-y-3" role="status" aria-label={t('featureCurator.loadingProjectsToFeature')}>{Array.from({ length: 3 }, (_, index) => <LoadingSkeleton key={index} variant="card" className="h-28" />)}</div>;
  if (candidates.isError) return <ErrorState title={t('featureCurator.couldNotLoadProjects')} message={parseApiError(candidates.error, t('featureCurator.pleaseTryAgain')).message} />;

  return <section aria-label={t('featureCurator.selectFeaturedProjects')} className="space-y-5">
    <div className="flex flex-wrap items-center gap-2 rounded-2xl border border-slate-200 bg-white p-3 dark:border-slate-700 dark:bg-dark-surface">
      <p className="mr-auto text-body-sm text-slate-600 dark:text-slate-300">{t(isAdmin ? 'featureCurator.hintAdmin' : 'featureCurator.hintLecturer')}</p>
      {isAdmin && <select aria-label={t('featureCurator.lecturer')} className={selectClass} value={lecturer} onChange={event => { setLecturer(event.target.value); setClassCode(''); }}><option value="">{t('featureCurator.allLecturers')}</option>{lecturers.map(name => <option key={name} value={name}>{name}</option>)}</select>}
      <select aria-label={t('showcaseReferenceData.class')} className={selectClass} value={classCode} onChange={event => setClassCode(event.target.value)}><option value="">{isAdmin ? t('featureCurator.allClasses') : t('featureCurator.allMyClasses')}</option>{classes.map(code => <option key={code} value={code}>{code}</option>)}</select>
    </div>

    {groups.length === 0
      ? <p className="rounded-2xl border border-dashed border-slate-300 bg-slate-50 px-5 py-8 text-body-sm text-slate-600 dark:border-slate-600 dark:bg-secondary-800 dark:text-slate-300">{t('featureCurator.noPublishedProjectsMatchTheseFilters')}</p>
      : groups.map(group => {
        const ids = group.items.map(item => item.id);
        const allChecked = ids.every(id => selected.has(id));
        const notFeatured = group.items.filter(item => !item.isFeatured).map(item => item.id);
        return <section key={group.classCode} aria-label={t('featureCurator.class', { classCode: group.classCode })}>
          <div className="flex flex-wrap items-center justify-between gap-2 rounded-t-2xl border border-b-0 border-slate-200 bg-white px-4 py-3 dark:border-slate-700 dark:bg-dark-surface">
            <label className="flex cursor-pointer items-center gap-3 text-subheading font-bold text-secondary dark:text-white">
              <input type="checkbox" className="h-5 w-5 accent-primary" checked={allChecked} onChange={event => toggle(ids, event.target.checked)} aria-label={t('featureCurator.selectAllProjectsOf', { classCode: group.classCode })} />
              {group.classCode}
            </label>
            <div className="flex flex-wrap items-center gap-3 text-body-sm text-slate-500 dark:text-slate-400">
              <span>{group.lecturerName || t('featureCurator.noLecturer')} · {t('featureCurator.featuredCount', { featured: group.featuredCount, total: group.items.length })}</span>
              {notFeatured.length > 0 && <Button variant="outline" size="sm" onClick={() => toggle(notFeatured, true)}>{t('featureCurator.selectNotFeatured')}</Button>}
            </div>
          </div>
          <ul className="divide-y divide-slate-200 overflow-hidden rounded-b-2xl border border-slate-200 bg-white dark:divide-slate-700 dark:border-slate-700 dark:bg-dark-surface">
            {group.items.map(item => {
              const checked = selected.has(item.id);
              return <li key={item.id} className={`flex items-center gap-3 border-l-4 px-4 py-3 ${checked ? 'border-l-primary bg-primary-50 dark:bg-primary-900/20' : 'border-l-transparent'}`}>
                <input id={`cand-${item.id}`} type="checkbox" className="h-5 w-5 shrink-0 accent-primary" checked={checked} onChange={event => toggle([item.id], event.target.checked)} />
                <label htmlFor={`cand-${item.id}`} className="flex min-w-0 flex-1 cursor-pointer items-center gap-3">
                  <span className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-slate-200 bg-white text-body-sm font-semibold text-secondary dark:border-slate-600 dark:bg-dark-surface dark:text-slate-100">
                    {item.logoUrl ? <img src={item.logoUrl} alt="" className="h-full w-full object-contain p-1" loading="lazy" /> : item.projectName.slice(0, 2).toUpperCase()}
                  </span>
                  <span className="min-w-0">
                    <span className="flex flex-wrap items-center gap-2 font-semibold text-secondary dark:text-white"><span className="truncate">{item.projectName}</span>
                      {item.isFeatured && <span className="inline-flex items-center gap-1 rounded-full border border-primary-200 bg-primary-50 px-2 py-0.5 text-caption font-semibold text-primary-800 dark:border-primary-700 dark:bg-primary-900/30 dark:text-primary-200"><Star className="h-3 w-3" aria-hidden="true" />{t('featureCurator.featured')}</span>}
                    </span>
                    <span className="block truncate text-body-sm text-slate-500 dark:text-slate-400">{item.teamName} · {stageLabel(item.developmentStage)}</span>
                  </span>
                </label>
              </li>;
            })}
          </ul>
        </section>;
      })}

    {selected.size > 0 && <div role="region" aria-label={t('featureCurator.selectionActions')} className="sticky bottom-4 z-20 flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-secondary px-4 py-3 text-white shadow-lg">
      <span className="text-body-sm"><b>{selected.size}</b> {t('featureCurator.selected')}</span>
      <span className="flex flex-wrap gap-2">
        <Button variant="outline" size="sm" className="!border-white/40 !bg-transparent !text-white hover:!bg-white/10" disabled={isSaving} onClick={() => setSelected(new Set())}>{t('common:actions.clear')}</Button>
        {toFeature.length > 0 && <Button variant="primary" size="sm" icon={Star} isLoading={isSaving} onClick={() => { void apply(toFeature, true); }}>{t('featureCurator.feature', { length: toFeature.length })}</Button>}
        {toUnfeature.length > 0 && <Button variant="outline" size="sm" className="!border-white/40 !bg-transparent !text-white hover:!bg-white/10" disabled={isSaving} onClick={() => { void apply(toUnfeature, false); }}>{t('featureCurator.unfeature', { length: toUnfeature.length })}</Button>}
      </span>
    </div>}
  </section>;
}
