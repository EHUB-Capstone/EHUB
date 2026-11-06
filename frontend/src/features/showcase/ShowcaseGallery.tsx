import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Flame, LayoutGrid, Search, SearchX, Sparkles, Star, X, type LucideIcon } from 'lucide-react';
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
import { discoveryPages, discoveryRequest, isNewlyPublished } from '../../utils/showcaseDiscovery';
import { parseApiError } from '../../utils/apiError';
import { cn } from '../../utils/cn';
import ProjectRailItem from './components/ProjectRailItem';
import ProjectSummaryCard from './components/ProjectSummaryCard';

const selectClass = 'h-10 w-full rounded-lg border border-slate-300 bg-white px-3.5 text-body text-slate-900 transition-colors hover:border-slate-400 focus:border-primary-600 focus:outline-none focus:ring-2 focus:ring-primary/30 dark:border-slate-600 dark:bg-dark-surface dark:text-slate-100 dark:hover:border-slate-500';
function DiscoverySearch({ keyword }: { keyword: string }) {
  const [, setParams] = useSearchParams();
  const [value, setValue] = useState(keyword);
  useEffect(() => {
    if (value.trim() === keyword) return undefined;
    const timer = window.setTimeout(() => setParams(current => {
      if ((current.get('q') ?? '') !== keyword) return current;
      const next = new URLSearchParams(current);
      if (value.trim()) next.set('q', value.trim()); else next.delete('q');
      next.delete('page');
      return next;
    }, { replace: true }), 300);
    return () => window.clearTimeout(timer);
  }, [value, keyword, setParams]);
  return <Input aria-label="Search projects" icon={Search} value={value} maxLength={100} onChange={event => setValue(event.target.value)} placeholder="Search projects by name or description..." />;
}

function RailTitle({ children }: { children: React.ReactNode }) {
  return <h2 className="mb-2 px-2 text-overline uppercase text-slate-500 dark:text-slate-400">{children}</h2>;
}

function RailButton({ icon: Icon, active, onClick, children }: { icon?: LucideIcon; active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      aria-pressed={active}
      onClick={onClick}
      className={cn(
        'flex min-h-10 w-full items-center gap-2.5 rounded-lg px-3 text-left text-body-sm font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40',
        active ? 'bg-primary-50 text-primary-700 dark:bg-primary-900/30 dark:text-primary-300' : 'text-slate-600 hover:bg-slate-100 hover:text-secondary dark:text-slate-300 dark:hover:bg-secondary-700 dark:hover:text-white',
      )}
    >
      {Icon && <Icon className="h-4 w-4 shrink-0" aria-hidden="true" />}
      {children}
    </button>
  );
}

function RailCard({ id, title, children }: { id: string; title: string; children: React.ReactNode }) {
  return (
    <section aria-labelledby={id} className="rounded-2xl border border-slate-200 bg-white p-3 shadow-card dark:border-slate-700 dark:bg-dark-surface">
      <h2 id={id} className="mb-1 px-2 pt-1 text-subheading text-secondary dark:text-white">{title}</h2>
      {children}
    </section>
  );
}

function RailList({ query, items, backTo, emptyHidden = true, loadingLabel, errorTitle }: {
  query: ReturnType<typeof useQuery<Awaited<ReturnType<typeof showcaseApi.list>>>>;
  /** Allows a curated rail to apply its own display rule without changing the discovery API. */
  items?: ShowcaseCardDto[];
  backTo: string;
  emptyHidden?: boolean;
  loadingLabel: string;
  errorTitle: string;
}) {
  if (query.isPending) return <div className="space-y-2 p-2" role="status" aria-label={loadingLabel}>{Array.from({ length: 3 }, (_, index) => <div key={index} className="h-11 animate-pulse rounded-xl bg-slate-100 dark:bg-secondary-700" />)}</div>;
  if (query.isError) return <div className="p-2" role="alert"><p className="text-body-sm text-slate-600 dark:text-slate-300">{errorTitle}</p><Button variant="outline" size="sm" className="mt-2" onClick={() => { void query.refetch(); }}>Retry</Button></div>;
  const visibleItems = items ?? query.data.items;
  if (visibleItems.length === 0) return emptyHidden ? <p className="px-2 pb-2 text-body-sm text-slate-500 dark:text-slate-400">Nothing here yet.</p> : null;
  return <ul className="space-y-0.5">{visibleItems.map(project => <ProjectRailItem key={project.id} project={project} backTo={backTo} backLabel="Project Showcase" />)}</ul>;
}

export default function ShowcaseGallery(): React.ReactElement {
  const [params, setParams] = useSearchParams();
  const { user } = useAuthContext();
  const location = useLocation();
  const backTo = `${location.pathname}${location.search}`;
  const request = discoveryRequest(params);
  const keyword = params.get('q') ?? '';
  const [searchReset, setSearchReset] = useState(0);
  const gallery = useQuery({ queryKey: ['showcase', 'discovery', user?.id, user?.role, request], queryFn: ({ signal }) => showcaseApi.list(request, signal), staleTime: 0 });
  const options = useQuery({ queryKey: ['showcase', 'filter-options'], queryFn: ({ signal }) => showcaseApi.getFilterOptions(signal), staleTime: 0 });
  const featuredRequest: GetShowcasesRequest = { featured: true, sort: 'newest', page: 1, pageSize: 3 };
  const newestRequest: GetShowcasesRequest = { sort: 'newest', page: 1, pageSize: 3 };
  const featured = useQuery({ queryKey: ['showcase', 'featured'], queryFn: ({ signal }) => showcaseApi.list(featuredRequest, signal), staleTime: 0 });
  const newest = useQuery({ queryKey: ['showcase', 'newly-published'], queryFn: ({ signal }) => showcaseApi.list(newestRequest, signal), staleTime: 0 });
  const newlyPublished = (newest.data?.items ?? []).filter(project => isNewlyPublished(project.publishedAt));

  const updateParam = (key: string, value: string) => setParams(current => {
    const next = new URLSearchParams(current);
    if (value) next.set(key, value); else next.delete(key);
    if (key === 'sort') next.delete('tab');
    if (key !== 'page') next.delete('page');
    return next;
  });
  const clearFilters = () => { setSearchReset(current => current + 1); setParams(current => { const next = new URLSearchParams(current); for (const key of ['q', 'stage', 'industry', 'semesterId', 'featured', 'sort', 'page']) next.delete(key); return next; }); };
  const removeFilter = (key: string) => { if (key === 'q') setSearchReset(current => current + 1); updateParam(key, ''); };

  const items = gallery.data?.items ?? [];
  const totalPages = gallery.data?.totalPages ?? 0;
  const page = request.page ?? 1;
  const isFeaturedOnly = request.featured === true;
  const isPopular = request.sort === 'trending';
  const hasFilters = Boolean(request.search || request.stage || request.industry || request.semesterId || request.featured !== undefined || isPopular);
  const semesterName = (id: string) => { const semester = options.data?.semesters.find(item => item.id === id); return semester ? (semester.code || semester.name) : 'Selected semester'; };
  const activeFilters = [
    request.search && { key: 'q', label: `“${request.search}”` },
    request.stage && { key: 'stage', label: `Stage: ${stageLabel(request.stage)}` },
    request.industry && { key: 'industry', label: `Industry: ${request.industry}` },
    request.semesterId && { key: 'semesterId', label: `Semester: ${semesterName(request.semesterId)}` },
    isFeaturedOnly && { key: 'featured', label: 'Faculty selected' },
    isPopular && { key: 'sort', label: 'Most popular' },
  ].filter((filter): filter is { key: string; label: string } => Boolean(filter));

  const selectOptions = (label: string, key: string, value: string, choices: ReadonlyArray<{ value: string; label: string }>) => (
    <label className="block">
      <span className="mb-1.5 block text-body-sm font-semibold text-secondary">{label}</span>
      <select className={selectClass} value={value} onChange={event => updateParam(key, event.target.value)} disabled={options.isPending}>
        <option value="">All</option>
        {value && !choices.some(choice => choice.value === value) && <option value={value}>{key === 'semesterId' ? 'Selected semester' : value}</option>}
        {choices.map(choice => <option key={choice.value} value={choice.value}>{choice.label}</option>)}
      </select>
    </label>
  );

  return (
    <main className="mx-auto max-w-[88rem] px-4 py-6 sm:px-6 sm:py-8 lg:px-8">
      <PageHeader title="Project Showcase" subtitle="Discover what student teams are building, from early ideas to launched products." />

      <div className="grid gap-6 lg:grid-cols-[15rem_minmax(0,1fr)] xl:grid-cols-[15rem_minmax(0,1fr)_19rem]">
        {/* Left rail: browse and filter, like the shortcuts column of a feed. */}
        <aside aria-label="Browse and filter" className="space-y-4 lg:sticky lg:top-6 lg:self-start">
          <section className="rounded-2xl border border-slate-200 bg-white p-3 shadow-card dark:border-slate-700 dark:bg-dark-surface">
            <RailTitle>Discover</RailTitle>
            <div className="space-y-0.5">
              <RailButton icon={LayoutGrid} active={!isFeaturedOnly && !isPopular} onClick={() => setParams(current => { const next = new URLSearchParams(current); for (const key of ['featured', 'sort', 'page']) next.delete(key); return next; })}>All projects</RailButton>
              <RailButton icon={Star} active={isFeaturedOnly} onClick={() => updateParam('featured', isFeaturedOnly ? '' : 'true')}>Faculty selected</RailButton>
              <RailButton icon={Flame} active={isPopular} onClick={() => updateParam('sort', isPopular ? '' : 'trending')}>Most popular</RailButton>
            </div>
          </section>

          <section className="rounded-2xl border border-slate-200 bg-white p-3 shadow-card dark:border-slate-700 dark:bg-dark-surface">
            <RailTitle>Stage</RailTitle>
            <div className="space-y-0.5">
              <RailButton active={!request.stage} onClick={() => updateParam('stage', '')}>Any stage</RailButton>
              {SHOWCASE_STAGES.map(stage => <RailButton key={stage.value} active={request.stage === stage.value} onClick={() => updateParam('stage', request.stage === stage.value ? '' : stage.value)}>{stage.label}</RailButton>)}
            </div>
          </section>

          <section className="grid gap-4 rounded-2xl border border-slate-200 bg-white p-4 shadow-card dark:border-slate-700 dark:bg-dark-surface sm:grid-cols-2 lg:grid-cols-1">
            {selectOptions('Industry', 'industry', request.industry ?? '', (options.data?.industries ?? []).map(value => ({ value, label: value })))}
            {selectOptions('Semester', 'semesterId', request.semesterId ?? '', (options.data?.semesters ?? []).map(semester => ({ value: semester.id, label: semester.code || semester.name })))}
            {options.isError && <div className="sm:col-span-2 lg:col-span-1" role="alert"><p className="text-body-sm text-slate-600">Could not load filter options.</p><Button variant="outline" size="sm" className="mt-2" onClick={() => { void options.refetch(); }}>Retry filters</Button></div>}
          </section>
        </aside>

        {/* Center: search composer, active filters and the feed. */}
        <section aria-labelledby="feed-heading" className="min-w-0">
          <h2 id="feed-heading" className="sr-only">Published projects</h2>
          <div className="mx-auto w-full max-w-2xl space-y-4">
            <div className="flex items-center gap-3 rounded-2xl border border-slate-200 bg-white p-3 shadow-card dark:border-slate-700 dark:bg-dark-surface">
              <span className="hidden h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary-50 text-primary-700 sm:flex" aria-hidden="true"><Sparkles className="h-5 w-5" /></span>
              <div className="min-w-0 flex-1"><DiscoverySearch key={`${keyword}-${searchReset}`} keyword={keyword} /></div>
            </div>

            <div className="flex flex-wrap items-center gap-2 px-1">
              <p aria-live="polite" className="mr-1 text-body-sm font-semibold text-secondary">{gallery.isSuccess ? `${gallery.data.totalItems} ${gallery.data.totalItems === 1 ? 'project' : 'projects'}` : gallery.isPending ? 'Loading projects…' : 'Projects unavailable'}</p>
              {activeFilters.map(filter => (
                <span key={filter.key} className="inline-flex items-center gap-1 rounded-full border border-primary-200 bg-primary-50 py-0.5 pl-3 pr-1 text-caption font-semibold text-primary-800 dark:border-primary-700 dark:bg-primary-900/30 dark:text-primary-200">
                  {filter.label}
                  <button type="button" aria-label={`Remove filter ${filter.label}`} onClick={() => removeFilter(filter.key)} className="flex h-5 w-5 items-center justify-center rounded-full hover:bg-primary-100 dark:hover:bg-primary-800"><X className="h-3 w-3" aria-hidden="true" /></button>
                </span>
              ))}
              {hasFilters && <Button variant="ghost-primary" size="sm" onClick={clearFilters}>Clear filters</Button>}
            </div>

            {gallery.isPending
              ? <div className="space-y-4" role="status" aria-label="Loading projects">{Array.from({ length: 3 }, (_, index) => <LoadingSkeleton key={index} variant="card" className="h-72" />)}</div>
              : gallery.isError
                ? <ErrorState title="Could not load projects" message={parseApiError(gallery.error, 'Please try loading projects again.').message} onRetry={() => { void gallery.refetch(); }} />
                : items.length > 0
                  ? <div aria-label="Published projects" className="space-y-5">{items.map(project => <ProjectSummaryCard key={`${project.id}:${user?.id}:${user?.role}`} project={project} backTo={backTo} backLabel="Project Showcase" />)}</div>
                  : (
                    <div className="flex flex-col items-center rounded-2xl border border-dashed border-slate-300 bg-slate-50 px-6 py-14 text-center dark:border-slate-600 dark:bg-secondary-800">
                      <span className="flex h-12 w-12 items-center justify-center rounded-full bg-primary-50 text-primary-700"><SearchX className="h-6 w-6" aria-hidden="true" /></span>
                      <h3 className="mt-4 text-subheading text-secondary">{page > 1 ? 'No projects on this page' : hasFilters ? 'No matching projects found' : 'No published projects yet'}</h3>
                      <p className="mt-1 text-body-sm text-slate-600 dark:text-slate-300">{hasFilters ? 'Try another search or clear the filters.' : 'Approved showcases will appear here.'}</p>
                      {page > 1 ? <Button variant="outline" size="sm" className="mt-4" onClick={() => updateParam('page', '1')}>Back to first page</Button> : hasFilters && <Button variant="ghost-primary" size="sm" className="mt-3" onClick={clearFilters}>Clear filters</Button>}
                    </div>
                  )}

            {gallery.isSuccess && totalPages > 1 && page <= totalPages && (
              <nav aria-label="Pagination" className="flex flex-wrap items-center justify-center gap-2 pt-2">
                <Button variant="outline" size="sm" disabled={page === 1} onClick={() => updateParam('page', String(page - 1))}>Previous</Button>
                {discoveryPages(page, totalPages).map(number => <Button key={number} variant={number === page ? 'primary' : 'outline'} size="sm" aria-current={number === page ? 'page' : undefined} onClick={() => updateParam('page', String(number))}>{number}</Button>)}
                <span className="text-caption text-slate-600 dark:text-slate-300">Page {page} of {totalPages}</span>
                <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => updateParam('page', String(page + 1))}>Next</Button>
              </nav>
            )}
          </div>
        </section>

        {/* Right rail: curated highlights (stacks under the feed on smaller screens). */}
        <aside aria-label="Highlights" className="space-y-4 lg:col-span-2 xl:col-span-1 xl:sticky xl:top-6 xl:self-start">
          {!(featured.isSuccess && featured.data.items.length === 0) && (
            <RailCard id="featured-projects-heading" title="Featured by faculty">
              <RailList query={featured} backTo={backTo} loadingLabel="Loading featured projects" errorTitle="Could not load featured projects." />
            </RailCard>
          )}
          {!(newest.isSuccess && newlyPublished.length === 0) && (
            <RailCard id="newly-published-heading" title="Newly published">
              <RailList query={newest} items={newlyPublished} backTo={backTo} loadingLabel="Loading newly published projects" errorTitle="Could not load newly published projects." />
            </RailCard>
          )}
        </aside>
      </div>
    </main>
  );
}
