import { useEffect, useRef, useState } from 'react';
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { Loader2, Search, SearchX, X } from 'lucide-react';
import { useLocation, useSearchParams } from 'react-router-dom';
import Button from '../../components/ui/Button';
import ErrorState from '../../components/ui/ErrorState';
import Input from '../../components/ui/Input';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import PageHeader from '../../components/ui/PageHeader';
import { showcaseApi } from '../../api/showcaseApi';
import { useAuthContext } from '../../context/AuthContext';
import { SHOWCASE_STAGES, stageLabel } from '../../utils/showcase';
import { discoveryRequest, mergeShowcasePages, SHOWCASE_LAUNCH_PERIODS } from '../../utils/showcaseDiscovery';
import { parseApiError } from '../../utils/apiError';
import ProjectSummaryRow from './components/ProjectSummaryRow';

const selectClass = 'h-11 w-full rounded-lg border border-slate-300 bg-white px-3 text-body-sm text-slate-900 transition-colors hover:border-slate-400 focus:border-primary-600 focus:outline-none focus:ring-2 focus:ring-primary/30 disabled:cursor-wait disabled:opacity-60 dark:border-slate-600 dark:bg-dark-surface dark:text-slate-100 dark:hover:border-slate-500';

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
  return <Input label="Search projects" icon={Search} value={value} maxLength={100} className="h-11 text-body-sm" onChange={event => setValue(event.target.value)} placeholder="Search by name or description..." />;
}

export default function ShowcaseGallery(): React.ReactElement {
  const [params, setParams] = useSearchParams();
  const { user } = useAuthContext();
  const location = useLocation();
  const backTo = `${location.pathname}${location.search}`;
  const request = { ...discoveryRequest(params), page: 1 };
  const keyword = params.get('q') ?? '';
  const [searchReset, setSearchReset] = useState(0);
  const gallery = useInfiniteQuery({
    queryKey: ['showcase', 'discovery', user?.id, user?.role, 'infinite', request],
    queryFn: ({ pageParam, signal }) => showcaseApi.list({ ...request, page: pageParam }, signal),
    initialPageParam: 1,
    getNextPageParam: last => last.items.length > 0 && last.pageIndex < last.totalPages ? last.pageIndex + 1 : undefined,
    staleTime: 0,
  });
  const loadMoreTarget = useRef<HTMLDivElement>(null);
  const { fetchNextPage, hasNextPage, isFetching, isError } = gallery;
  useEffect(() => {
    const target = loadMoreTarget.current;
    if (!target || !hasNextPage || isFetching || isError || typeof IntersectionObserver === 'undefined') return;
    const observer = new IntersectionObserver(entries => {
      if (entries.some(entry => entry.isIntersecting)) void fetchNextPage({ cancelRefetch: false });
    }, { rootMargin: '0px 0px 240px 0px' });
    observer.observe(target);
    return () => observer.disconnect();
  }, [fetchNextPage, hasNextPage, isFetching, isError]);
  const options = useQuery({
    queryKey: ['showcase', 'filter-options'],
    queryFn: ({ signal }) => showcaseApi.getFilterOptions(signal),
    staleTime: 0,
  });

  const updateParam = (key: string, value: string) => setParams(current => {
    const next = new URLSearchParams(current);
    if (value) next.set(key, value); else next.delete(key);
    if (key === 'sort') next.delete('tab');
    if (key !== 'page') next.delete('page');
    return next;
  });
  const clearFilters = () => {
    setSearchReset(current => current + 1);
    setParams(current => {
      const next = new URLSearchParams(current);
      for (const key of ['q', 'stage', 'industry', 'semesterId', 'featured', 'sort', 'period', 'tab', 'page']) next.delete(key);
      return next;
    });
  };
  const selectDiscover = (value: string) => setParams(current => {
    const next = new URLSearchParams(current);
    for (const key of ['featured', 'sort', 'tab', 'page']) next.delete(key);
    if (value === 'featured') next.set('featured', 'true');
    if (value === 'popular') next.set('sort', 'trending');
    return next;
  });
  const removeFilter = (key: string) => {
    if (key === 'q') setSearchReset(current => current + 1);
    updateParam(key, '');
  };

  const items = mergeShowcasePages(gallery.data?.pages ?? []);
  const totalItems = gallery.data?.pages.at(-1)?.totalItems ?? 0;
  const isFeaturedOnly = request.featured === true;
  const isPopular = request.sort === 'trending';
  const hasFilters = Boolean(request.search || request.stage || request.industry || request.semesterId || request.featured !== undefined || isPopular || request.period);
  const semesterName = (id: string) => {
    const semester = options.data?.semesters.find(item => item.id === id);
    return semester ? (semester.code || semester.name) : 'Selected semester';
  };
  const activeFilters = [
    request.search && { key: 'q', label: `“${request.search}”` },
    request.stage && { key: 'stage', label: stageLabel(request.stage) },
    request.industry && { key: 'industry', label: request.industry },
    request.semesterId && { key: 'semesterId', label: semesterName(request.semesterId) },
    request.featured !== undefined && { key: 'featured', label: isFeaturedOnly ? 'Faculty selected' : 'Non-featured' },
    isPopular && { key: 'sort', label: 'Most popular' },
    request.period && { key: 'period', label: SHOWCASE_LAUNCH_PERIODS.find(period => period.value === request.period)!.label },
  ].filter((filter): filter is { key: string; label: string } => Boolean(filter));

  const selectOptions = (
    label: string, key: string, value: string,
    choices: ReadonlyArray<{ value: string; label: string }>, disabled = options.isPending,
  ) => (
    <label className="min-w-0">
      <span className="mb-1.5 block text-body-sm font-semibold text-secondary dark:text-slate-100">{label}</span>
      <select className={selectClass} value={value} onChange={event => updateParam(key, event.target.value)} disabled={disabled}>
        <option value="">{key === 'industry' ? 'All industries' : key === 'period' ? 'Any time' : `All ${label.toLowerCase()}s`}</option>
        {value && !choices.some(choice => choice.value === value) && <option value={value}>{key === 'semesterId' ? 'Selected semester' : value}</option>}
        {choices.map(choice => <option key={choice.value} value={choice.value}>{choice.label}</option>)}
      </select>
    </label>
  );

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <PageHeader title="Project Showcase" subtitle="Discover what student teams are building, from early ideas to launched products." className="mb-0" />

      <section aria-label="Search and filter projects" className="sticky top-16 z-20 rounded-2xl border border-slate-200 bg-white p-3 shadow-card sm:p-4 dark:border-slate-700 dark:bg-dark-surface">
        <div className="grid grid-cols-2 items-end gap-3 sm:grid-cols-3 xl:grid-cols-5">
          <div className="col-span-2 min-w-0 sm:col-span-3 xl:col-span-5">
            <DiscoverySearch key={`${keyword}-${searchReset}`} keyword={keyword} />
          </div>
          <label className="min-w-0">
            <span className="mb-1.5 block text-body-sm font-semibold text-secondary dark:text-slate-100">Discover</span>
            <select className={selectClass} value={isFeaturedOnly ? 'featured' : isPopular ? 'popular' : ''} onChange={event => selectDiscover(event.target.value)}>
              <option value="">All projects</option>
              <option value="featured">Faculty selected</option>
              <option value="popular">Most popular</option>
            </select>
          </label>
          {selectOptions('Stage', 'stage', request.stage ?? '', SHOWCASE_STAGES, false)}
          {selectOptions('Industry', 'industry', request.industry ?? '', (options.data?.industries ?? []).map(value => ({ value, label: value })))}
          {selectOptions('Semester', 'semesterId', request.semesterId ?? '', (options.data?.semesters ?? []).map(item => ({ value: item.id, label: item.code || item.name })))}
          <div className="col-span-2 sm:col-span-1">
            {selectOptions('Launch period', 'period', request.period ?? '', SHOWCASE_LAUNCH_PERIODS, false)}
          </div>
        </div>
        {request.period && <p className="mt-2 text-caption text-slate-500 dark:text-slate-400">Weeks start on Monday, UTC. Top launches uses the Most popular score.</p>}
      </section>

      {options.isError && (
        <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 bg-white p-3 text-body-sm text-slate-600 dark:border-slate-700 dark:bg-dark-surface dark:text-slate-300">
          <p>Could not load industry and semester filters.</p>
          <Button variant="outline" size="sm" className="min-h-9" onClick={() => { void options.refetch(); }}>Retry filters</Button>
        </div>
      )}

      <section aria-labelledby="projects-heading" className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 id="projects-heading" aria-live="polite" className="text-subheading font-semibold text-secondary dark:text-white">
            {gallery.data ? `${totalItems} ${totalItems === 1 ? 'project' : 'projects'}` : gallery.isPending ? 'Loading projects…' : 'Projects unavailable'}
          </h2>
          {hasFilters && <Button variant="ghost-primary" size="sm" className="min-h-9" onClick={clearFilters}>Clear filters</Button>}
        </div>
        {activeFilters.length > 0 && (
          <div aria-label="Active filters" className="flex flex-wrap gap-2">
            {activeFilters.map(filter => (
              <span key={filter.key} className="inline-flex items-center rounded-full border border-primary-200 bg-primary-50 pl-3 text-caption font-semibold text-primary-800 dark:border-primary-700 dark:bg-primary-900/30 dark:text-primary-200">
                {filter.label}
                <button type="button" aria-label={`Remove filter ${filter.label}`} onClick={() => removeFilter(filter.key)} className="ml-1 flex h-9 w-9 items-center justify-center rounded-full hover:bg-primary-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:hover:bg-primary-800">
                  <X className="h-3.5 w-3.5" aria-hidden="true" />
                </button>
              </span>
            ))}
          </div>
        )}

        {gallery.isPending
          ? <div className="space-y-3" role="status" aria-label="Loading projects">{Array.from({ length: 4 }, (_, index) => <LoadingSkeleton key={index} variant="card" className="h-28" />)}</div>
          : gallery.isError && !gallery.data
            ? <ErrorState title="Could not load projects" message={parseApiError(gallery.error, 'Please try loading projects again.').message} onRetry={() => { void gallery.refetch(); }} />
            : items.length > 0
              ? (
                <ul aria-label="Published projects" className="divide-y divide-slate-200 overflow-hidden rounded-2xl border border-slate-200 bg-white dark:divide-slate-700 dark:border-slate-700 dark:bg-dark-surface">
                  {items.map(project => <ProjectSummaryRow key={`${project.id}:${user?.id}:${user?.role}`} project={project} backTo={backTo} />)}
                </ul>
              )
              : (
                <div className="flex flex-col items-center rounded-2xl border border-dashed border-slate-300 bg-slate-50 px-6 py-14 text-center dark:border-slate-600 dark:bg-secondary-800">
                  <SearchX className="h-8 w-8 text-primary-700 dark:text-primary-300" aria-hidden="true" />
                  <h3 className="mt-4 text-subheading text-secondary dark:text-white">{hasFilters ? 'No matching projects found' : 'No published projects yet'}</h3>
                  <p className="mt-1 text-body-sm text-slate-600 dark:text-slate-300">{hasFilters ? 'Try another search or clear the filters.' : 'Approved showcases will appear here.'}</p>
                  {hasFilters && <Button variant="ghost-primary" size="sm" className="mt-3" onClick={clearFilters}>Clear filters</Button>}
                </div>
              )}

        {gallery.data && (
          <div ref={loadMoreTarget} className="flex min-h-16 flex-col items-center justify-center gap-3 py-4 text-body-sm text-slate-500 dark:text-slate-400" aria-live="polite">
            {gallery.isError ? (
              <>
                <p role="alert">{gallery.isFetchNextPageError ? 'Could not load more projects. Your loaded projects are still here.' : 'Could not refresh projects. Please try again.'}</p>
                <Button variant="outline" isLoading={isFetching} onClick={() => { void (gallery.isFetchNextPageError ? fetchNextPage({ cancelRefetch: false }) : gallery.refetch()); }}>Retry</Button>
              </>
            ) : gallery.isFetchingNextPage ? (
              <span role="status" className="inline-flex items-center gap-2"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />Loading more projects…</span>
            ) : hasNextPage ? (
              <Button variant="outline" disabled={isFetching} onClick={() => { void fetchNextPage({ cancelRefetch: false }); }}>Load more projects</Button>
            ) : items.length > 0 && <p>All available projects loaded · {items.length} projects</p>}
          </div>
        )}
      </section>
    </div>
  );
}
