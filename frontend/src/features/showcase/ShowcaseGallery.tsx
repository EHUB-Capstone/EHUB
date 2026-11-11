import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Search, SearchX, Sparkles, X } from 'lucide-react';
import { useLocation, useSearchParams } from 'react-router-dom';
import Button from '../../components/ui/Button';
import ErrorState from '../../components/ui/ErrorState';
import Input from '../../components/ui/Input';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import PageHeader from '../../components/ui/PageHeader';
import { showcaseApi } from '../../api/showcaseApi';
import { useAuthContext } from '../../context/AuthContext';
import type { GetShowcasesRequest, ShowcaseCardDto } from '../../types/showcase';
import { SHOWCASE_STAGES, stageLabel } from '../../utils/showcase';
import { discoveryRequest, SHOWCASE_DISCOVER_OPTIONS } from '../../utils/showcaseDiscovery';
import { parseApiError } from '../../utils/apiError';
import ProjectSummaryRow from './components/ProjectSummaryRow';

const selectClass = 'h-11 w-full rounded-lg border border-slate-300 bg-white px-3 text-body-sm text-slate-900 transition-colors hover:border-slate-400 focus:border-primary-600 focus:outline-none focus:ring-2 focus:ring-primary/30 disabled:cursor-wait disabled:opacity-60 dark:border-slate-600 dark:bg-dark-surface dark:text-slate-100 dark:hover:border-slate-500';
const sectionLimits = { featured: 12, topThisWeek: 6, thisWeek: 12, lastWeek: 12 } as const;
type DiscoverKind = keyof typeof sectionLimits;

function DiscoverySearch({ keyword }: { keyword: string }) {
  const [, setParams] = useSearchParams();
  const [value, setValue] = useState(keyword);
  useEffect(() => {
    if (value.trim() === keyword) return undefined;
    const timer = window.setTimeout(() => setParams(current => {
      if ((current.get('q') ?? '') !== keyword) return current;
      const next = new URLSearchParams(current);
      if (value.trim()) next.set('q', value.trim()); else next.delete('q');
      return next;
    }, { replace: true }), 300);
    return () => window.clearTimeout(timer);
  }, [value, keyword, setParams]);
  return <Input label="Search projects" icon={Search} value={value} maxLength={100} className="h-11 text-body-sm" onChange={event => setValue(event.target.value)} placeholder="Search by name or description..." />;
}

function requestFor(kind: DiscoverKind, base: Omit<GetShowcasesRequest, 'page' | 'pageSize' | 'featured' | 'sort' | 'period'>, pageSize: number): GetShowcasesRequest {
  if (kind === 'featured') return { ...base, featured: true, sort: 'newest', page: 1, pageSize };
  if (kind === 'topThisWeek') return { ...base, sort: 'trending', period: 'topThisWeek', page: 1, pageSize };
  return { ...base, sort: 'newest', period: kind, page: 1, pageSize };
}

function ProjectSection({ kind, title, projects, total, loading, error, backTo, expanded, onSeeAll }: { kind: DiscoverKind; title: string; projects: ShowcaseCardDto[]; total: number; loading: boolean; error: unknown; backTo: string; expanded: boolean; onSeeAll: () => void }) {
  const limit = sectionLimits[kind];
  return <section aria-labelledby={`${kind}-heading`} className="space-y-3">
    <div className="flex flex-wrap items-center justify-between gap-3"><div><h2 id={`${kind}-heading`} className="text-heading text-secondary dark:text-white">{title}</h2>{!loading && <p className="text-body-sm text-slate-500 dark:text-slate-400">{total} {total === 1 ? 'product' : 'products'}</p>}</div>{!expanded && total > limit && <Button variant="ghost-primary" size="sm" onClick={onSeeAll}>See all of {title}</Button>}</div>
    {loading ? <div className="space-y-3" role="status" aria-label={`Loading ${title}`}>{Array.from({ length: Math.min(limit, 3) }, (_, index) => <LoadingSkeleton key={index} variant="card" className="h-28" />)}</div>
      : error ? <ErrorState title={`Could not load ${title.toLowerCase()}`} message={parseApiError(error, 'Please try again.').message} />
        : projects.length > 0 ? <ol aria-label={title} className="divide-y divide-slate-200 overflow-hidden rounded-2xl border border-slate-200 bg-white dark:divide-slate-700 dark:border-slate-700 dark:bg-dark-surface">{projects.map((project, index) => <ProjectSummaryRow key={project.id} project={project} backTo={backTo} index={index + 1} />)}</ol>
          : <p className="rounded-2xl border border-dashed border-slate-300 bg-slate-50 px-5 py-8 text-body-sm text-slate-600 dark:border-slate-600 dark:bg-secondary-800 dark:text-slate-300">No products in this category yet.</p>}
  </section>;
}

export default function ShowcaseGallery(): React.ReactElement {
  const [params, setParams] = useSearchParams();
  const { user } = useAuthContext();
  const location = useLocation();
  const backTo = `${location.pathname}${location.search}`;
  const request = discoveryRequest(params);
  const keyword = params.get('q') ?? '';
  const selected = SHOWCASE_DISCOVER_OPTIONS.find(option => option.value === params.get('discover'))?.value as DiscoverKind | undefined;
  const [searchReset, setSearchReset] = useState(0);
  const base: Omit<GetShowcasesRequest, 'page' | 'pageSize' | 'featured' | 'sort' | 'period'> = { search: request.search, stage: request.stage, industry: request.industry, semesterId: request.semesterId };
  const pageSize = (kind: DiscoverKind) => selected === kind ? 50 : sectionLimits[kind];
  const featured = useQuery({ queryKey: ['showcase', 'discovery', user?.id, user?.role, 'featured', base, pageSize('featured')], queryFn: ({ signal }) => showcaseApi.list(requestFor('featured', base, pageSize('featured')), signal), staleTime: 0 });
  const topThisWeek = useQuery({ queryKey: ['showcase', 'discovery', user?.id, user?.role, 'topThisWeek', base, pageSize('topThisWeek')], queryFn: ({ signal }) => showcaseApi.list(requestFor('topThisWeek', base, pageSize('topThisWeek')), signal), staleTime: 0 });
  const thisWeek = useQuery({ queryKey: ['showcase', 'discovery', user?.id, user?.role, 'thisWeek', base, pageSize('thisWeek')], queryFn: ({ signal }) => showcaseApi.list(requestFor('thisWeek', base, pageSize('thisWeek')), signal), staleTime: 0 });
  const lastWeek = useQuery({ queryKey: ['showcase', 'discovery', user?.id, user?.role, 'lastWeek', base, pageSize('lastWeek')], queryFn: ({ signal }) => showcaseApi.list(requestFor('lastWeek', base, pageSize('lastWeek')), signal), staleTime: 0 });
  const options = useQuery({ queryKey: ['showcase', 'filter-options'], queryFn: ({ signal }) => showcaseApi.getFilterOptions(signal), staleTime: 0 });
  // A project promoted into the launch rail is not repeated in either chronological rail.
  // Only the visible Top entries are excluded, so projects beyond its six-item limit remain discoverable.
  const topProjects = topThisWeek.data?.items ?? [];
  const topProjectIds = new Set(topProjects.map(project => project.id));
  const removeTopProjects = (projects: ShowcaseCardDto[]) => projects.filter(project => !topProjectIds.has(project.id));
  const sections = [
    { kind: 'featured' as const, title: 'Featured products', query: featured, projects: featured.data?.items ?? [], total: featured.data?.totalItems ?? 0 },
    { kind: 'topThisWeek' as const, title: 'Top products launching', query: topThisWeek, projects: topProjects, total: topThisWeek.data?.totalItems ?? 0 },
    { kind: 'thisWeek' as const, title: "This week's products", query: thisWeek, projects: removeTopProjects(thisWeek.data?.items ?? []), total: Math.max(0, (thisWeek.data?.totalItems ?? 0) - (thisWeek.data?.items.filter(project => topProjectIds.has(project.id)).length ?? 0)) },
    { kind: 'lastWeek' as const, title: "Last week's products", query: lastWeek, projects: removeTopProjects(lastWeek.data?.items ?? []), total: Math.max(0, (lastWeek.data?.totalItems ?? 0) - (lastWeek.data?.items.filter(project => topProjectIds.has(project.id)).length ?? 0)) },
  ];
  const availableSections = sections.filter(section => section.query.isPending || section.projects.length > 0);
  const visibleSelected = selected;
  const visibleSections = availableSections.filter(section => !visibleSelected || section.kind === visibleSelected);
  const activeFilters = [request.search && { key: 'q', label: `“${request.search}”` }, request.stage && { key: 'stage', label: stageLabel(request.stage) }, request.industry && { key: 'industry', label: request.industry }, request.semesterId && { key: 'semesterId', label: options.data?.semesters.find(item => item.id === request.semesterId)?.code || 'Selected semester' }].filter((filter): filter is { key: string; label: string } => Boolean(filter));
  const updateParam = (key: string, value: string) => setParams(current => { const next = new URLSearchParams(current); if (value) next.set(key, value); else next.delete(key); return next; });
  const clearFilters = () => { setSearchReset(value => value + 1); setParams(current => { const next = new URLSearchParams(current); for (const key of ['q', 'stage', 'industry', 'semesterId', 'discover', 'period', 'page']) next.delete(key); return next; }); };
  const selectOptions = (label: string, key: string, value: string, choices: ReadonlyArray<{ value: string; label: string }>, disabled = options.isPending) => <label className="min-w-0"><span className="mb-1.5 block text-body-sm font-semibold text-secondary dark:text-slate-100">{label}</span><select className={selectClass} value={value} onChange={event => updateParam(key, event.target.value)} disabled={disabled}><option value="">{key === 'industry' ? 'All industries' : `All ${label.toLowerCase()}s`}</option>{choices.map(choice => <option key={choice.value} value={choice.value}>{choice.label}</option>)}</select></label>;
  return <main className="mx-auto max-w-[88rem] px-4 py-6 sm:px-6 sm:py-8 lg:px-8">
    <PageHeader title="Project Showcase" subtitle="Discover what student teams are building, from early ideas to launched products." className="mb-6" />
    <section aria-label="Search and filter projects" className="sticky top-16 z-20 rounded-2xl border border-slate-200 bg-white p-3 shadow-card sm:p-4 dark:border-slate-700 dark:bg-dark-surface"><div className="grid grid-cols-1 items-end gap-3 sm:grid-cols-2 lg:grid-cols-4"><div className="min-w-0 sm:col-span-2 lg:col-span-4"><div className="flex items-center gap-3"><span className="hidden h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary-50 text-primary-700 sm:flex" aria-hidden="true"><Sparkles className="h-5 w-5" /></span><div className="min-w-0 flex-1"><DiscoverySearch key={`${keyword}-${searchReset}`} keyword={keyword} /></div></div></div><label className="min-w-0"><span className="mb-1.5 block text-body-sm font-semibold text-secondary dark:text-slate-100">Discover</span><select className={selectClass} value={visibleSelected ?? ''} onChange={event => updateParam('discover', event.target.value)}><option value="">All product categories</option>{SHOWCASE_DISCOVER_OPTIONS.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}</select></label>{selectOptions('Stage', 'stage', request.stage ?? '', SHOWCASE_STAGES, false)}{selectOptions('Industry', 'industry', request.industry ?? '', (options.data?.industries ?? []).map(value => ({ value, label: value })))}{selectOptions('Semester', 'semesterId', request.semesterId ?? '', (options.data?.semesters ?? []).map(item => ({ value: item.id, label: item.code || item.name })))}</div></section>
    {options.isError && <div role="alert" className="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 bg-white p-3 text-body-sm text-slate-600 dark:border-slate-700 dark:bg-dark-surface dark:text-slate-300"><p>Could not load industry and semester filters.</p><Button variant="outline" size="sm" onClick={() => { void options.refetch(); }}>Retry filters</Button></div>}
    <div className="mt-6"><section className="space-y-8">{activeFilters.length > 0 && <div className="flex flex-wrap items-center gap-2">{activeFilters.map(filter => <span key={filter.key} className="inline-flex items-center rounded-full border border-primary-200 bg-primary-50 pl-3 text-caption font-semibold text-primary-800 dark:border-primary-700 dark:bg-primary-900/30 dark:text-primary-200">{filter.label}<button type="button" aria-label={`Remove filter ${filter.label}`} onClick={() => updateParam(filter.key, '')} className="ml-1 flex h-9 w-9 items-center justify-center rounded-full hover:bg-primary-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:hover:bg-primary-800"><X className="h-3.5 w-3.5" aria-hidden="true" /></button></span>)}<Button variant="ghost-primary" size="sm" onClick={clearFilters}>Clear filters</Button></div>}{visibleSections.map(section => <ProjectSection key={section.kind} kind={section.kind} title={section.title} projects={section.projects} total={section.total} loading={section.query.isPending} error={section.query.error} backTo={backTo} expanded={visibleSelected === section.kind} onSeeAll={() => updateParam('discover', section.kind)} />)}{!sections.some(section => section.query.isPending) && visibleSections.length === 0 && <div className="flex flex-col items-center rounded-2xl border border-dashed border-slate-300 bg-slate-50 px-6 py-14 text-center dark:border-slate-600 dark:bg-secondary-800"><SearchX className="h-8 w-8 text-primary-700 dark:text-primary-300" aria-hidden="true" /><h2 className="mt-4 text-subheading text-secondary dark:text-white">No matching products found</h2><p className="mt-1 text-body-sm text-slate-600 dark:text-slate-300">Try another search or clear the filters.</p></div>}</section></div>
  </main>;
}
