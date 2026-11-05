import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ArrowRight, CalendarDays, Flame, MessageSquare, Rocket, Search, Sparkles } from 'lucide-react';
import { Link, useSearchParams } from 'react-router-dom';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import ErrorState from '../../components/ui/ErrorState';
import Input from '../../components/ui/Input';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import { showcaseApi } from '../../api/showcaseApi';
import type { GetShowcasesRequest, ShowcaseCardDto } from '../../types/showcase';
import { SHOWCASE_STAGES, stageLabel } from '../../utils/showcase';
import { discoveryPages, discoveryRequest } from '../../utils/showcaseDiscovery';
import { parseApiError } from '../../utils/apiError';
import { cn } from '../../utils/cn';

const selectClass = 'h-10 w-full rounded-lg border border-slate-300 bg-white px-3.5 text-body text-slate-900 transition-colors hover:border-slate-400 focus:border-primary-600 focus:outline-none focus:ring-2 focus:ring-primary/30';
const stageTone = (stage: ShowcaseCardDto['developmentStage']) =>
  ({ Idea: 'neutral', Mvp: 'success', Beta: 'info', Launched: 'warning' } as const)[stage];

function ProjectMark({ project, size = 'card' }: { project: ShowcaseCardDto; size?: 'card' | 'compact' }) {
  const dimensions = size === 'card' ? 'h-16 w-16 rounded-2xl' : 'h-11 w-11 rounded-xl';
  return <div className={cn('flex shrink-0 items-center justify-center overflow-hidden border border-secondary-100 bg-secondary-50', dimensions)}>
    {project.logoUrl
      ? <img src={project.logoUrl} alt={`${project.projectName} logo`} className="h-full w-full object-contain p-1" />
      : <span className={cn('font-semibold tracking-tight text-secondary', size === 'card' ? 'text-heading' : 'text-body')}>{project.projectName.slice(0, 2).toUpperCase()}</span>}
  </div>;
}

function ProjectSignals({ project }: { project: ShowcaseCardDto }) {
  if (project.likeCount === 0 && project.commentCount === 0) return null;
  return <span className="inline-flex items-center gap-3 text-caption text-slate-500">
    {project.likeCount > 0 && <span className="inline-flex items-center gap-1"><Flame className="h-3.5 w-3.5" aria-hidden="true" />{project.likeCount} interested</span>}
    {project.commentCount > 0 && <span className="inline-flex items-center gap-1"><MessageSquare className="h-3.5 w-3.5" aria-hidden="true" />{project.commentCount} comments</span>}
  </span>;
}

function ProjectCard({ project }: { project: ShowcaseCardDto }) {
  return <article className="group relative overflow-hidden rounded-2xl border border-slate-200 bg-white p-5 shadow-card transition-all duration-300 hover:-translate-y-0.5 hover:border-slate-300 hover:shadow-elevated has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-primary/40">
    <div className="flex items-start gap-3">
      <ProjectMark project={project} />
      <div className="min-w-0 flex-1"><div className="flex flex-wrap items-center gap-2"><Badge size="sm" variant={stageTone(project.developmentStage)}>{stageLabel(project.developmentStage)}</Badge>{project.publishedAt && <span className="inline-flex items-center gap-1 text-caption text-slate-500"><CalendarDays className="h-3.5 w-3.5" aria-hidden="true" />{new Date(project.publishedAt).toLocaleDateString()}</span>}</div><h2 className="mt-2 line-clamp-1 text-subheading text-secondary">{project.projectName}</h2><p className="mt-1 line-clamp-1 text-body-sm text-slate-600">{project.teamName}</p></div>
    </div>
    <div className="mt-5 min-w-0 flex-1"><p className="line-clamp-3 text-body-sm leading-relaxed text-slate-600">{project.description}</p>{project.industries.length > 0 && <div className="mt-4 flex flex-wrap gap-1.5">{project.industries.slice(0, 3).map(industry => <Badge key={industry} size="sm" variant="neutral">{industry}</Badge>)}</div>}</div>
    <div className="mt-5 flex items-center justify-between gap-3 border-t border-slate-100 pt-4"><ProjectSignals project={project} /><Link to={`/showcase/${project.id}`} className="relative z-10 inline-flex shrink-0 items-center gap-1 text-body-sm font-semibold text-primary-700 hover:text-primary-800 focus-visible:outline-none">View project <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5" aria-hidden="true" /></Link></div>
  </article>;
}

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
  return <Input label="Search projects" icon={Search} value={value} maxLength={100} onChange={event => setValue(event.target.value)} placeholder="Search by project name or description..." />;
}

function NewlyPublished() {
  const request: GetShowcasesRequest = { sort: 'newest', page: 1, pageSize: 3 };
  const newest = useQuery({ queryKey: ['showcase', 'newly-published'], queryFn: ({ signal }) => showcaseApi.list(request, signal), staleTime: 0 });
  if (newest.isPending) return <div className="grid gap-4 md:grid-cols-3">{Array.from({ length: 3 }, (_, index) => <LoadingSkeleton key={index} variant="card" />)}</div>;
  if (newest.isError || newest.data.items.length === 0) return null;
  return <section aria-labelledby="newly-published-heading" className="mt-10"><div className="mb-5 flex flex-wrap items-end justify-between gap-3"><div><p className="text-overline font-semibold uppercase tracking-wider text-primary-700">Fresh from E-HUB</p><h2 id="newly-published-heading" className="mt-1 text-heading text-secondary">Newly published projects</h2><p className="mt-1 text-body text-slate-600"></p></div><a href="#explore-projects" className="inline-flex items-center gap-1 text-body-sm font-semibold text-primary-700 hover:underline">Explore all <ArrowRight className="h-4 w-4" aria-hidden="true" /></a></div><div className="grid gap-4 md:grid-cols-3">{newest.data.items.map(project => <ProjectCard key={project.id} project={project} />)}</div></section>;
}

export default function ShowcaseGallery(): React.ReactElement {
  const [params, setParams] = useSearchParams();
  const request = discoveryRequest(params);
  const keyword = params.get('q') ?? '';
  const [searchReset, setSearchReset] = useState(0);
  const gallery = useQuery({ queryKey: ['showcase', 'discovery', request], queryFn: ({ signal }) => showcaseApi.list(request, signal), staleTime: 0 });
  const options = useQuery({ queryKey: ['showcase', 'filter-options'], queryFn: ({ signal }) => showcaseApi.getFilterOptions(signal), staleTime: 0 });
  const updateParam = (key: string, value: string) => setParams(current => { const next = new URLSearchParams(current); if (value) next.set(key, value); else next.delete(key); if (key !== 'page') next.delete('page'); return next; });
  const clearFilters = () => { setSearchReset(current => current + 1); setParams(current => { const next = new URLSearchParams(current); for (const key of ['q', 'stage', 'industry', 'semesterId', 'page']) next.delete(key); return next; }); };
  const items = gallery.data?.items ?? [];
  const totalPages = gallery.data?.totalPages ?? 0;
  const page = request.page ?? 1;
  const hasFilters = Boolean(request.search || request.stage || request.industry || request.semesterId);
  const selectOptions = (label: string, key: string, value: string, choices: ReadonlyArray<{ value: string; label: string }>) => <label><span className="mb-1.5 block text-body-sm font-semibold text-secondary">{label}</span><select className={selectClass} value={value} onChange={event => updateParam(key, event.target.value)} disabled={options.isPending && key !== 'stage'}><option value="">All</option>{value && !choices.some(choice => choice.value === value) && <option value={value}>{key === 'semesterId' ? 'Selected semester' : value}</option>}{choices.map(choice => <option key={choice.value} value={choice.value}>{choice.label}</option>)}</select></label>;

  return <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 sm:py-8 lg:px-8">
    <section className="relative overflow-hidden rounded-3xl bg-secondary px-6 py-10 text-white shadow-elevated sm:px-10 sm:py-14"><div aria-hidden="true" className="absolute -right-10 -top-10 h-48 w-48 rounded-full border-[24px] border-white/10" /><div aria-hidden="true" className="absolute bottom-0 right-28 h-20 w-20 rounded-t-full bg-primary/90" /><div className="relative max-w-3xl"><div className="inline-flex items-center gap-2 rounded-full border border-white/20 bg-white/10 px-3 py-1 text-caption font-semibold"><Rocket className="h-3.5 w-3.5" aria-hidden="true" />STUDENT STARTUP SHOWCASE</div><h1 className="mt-5 text-display tracking-tight">Discover what students are building next.</h1><p className="mt-4 max-w-2xl text-body-lg leading-relaxed text-white/80">Explore approved student projects, from early ideas to launched products, and learn from the people building them.</p><a href="#explore-projects" className="mt-7 inline-flex h-11 items-center gap-2 rounded-lg bg-primary px-5 text-body font-semibold text-white shadow-sm transition-colors hover:bg-primary-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white focus-visible:ring-offset-2 focus-visible:ring-offset-secondary">Explore projects <ArrowRight className="h-4 w-4" aria-hidden="true" /></a></div></section>
    <NewlyPublished />
    <section id="explore-projects" aria-labelledby="explore-projects-heading" className="scroll-mt-6 mt-12 border-t border-slate-200 pt-10"><div className="flex flex-wrap items-end justify-between gap-3"><div><p className="text-overline font-semibold uppercase tracking-wider text-primary-700">Project directory</p><h2 id="explore-projects-heading" className="mt-1 text-heading text-secondary">Explore all projects</h2><p className="mt-1 text-body text-slate-600"></p></div><div className="inline-flex items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-body-sm text-slate-600"><Sparkles className="h-4 w-4 text-primary-700" aria-hidden="true" /><span>{gallery.isSuccess ? `${gallery.data.totalItems} published projects` : 'Published projects'}</span></div></div>
      <div className="mt-6 grid gap-4 rounded-2xl border border-slate-200 bg-slate-50 p-4 sm:grid-cols-2 lg:grid-cols-[minmax(0,2fr)_repeat(3,minmax(0,1fr))] lg:p-5"><div className="sm:col-span-2 lg:col-span-1"><DiscoverySearch key={`${keyword}-${searchReset}`} keyword={keyword} /></div>{selectOptions('Stage', 'stage', request.stage ?? '', SHOWCASE_STAGES)}{selectOptions('Industry', 'industry', request.industry ?? '', (options.data?.industries ?? []).map(value => ({ value, label: value })))}{selectOptions('Semester', 'semesterId', request.semesterId ?? '', (options.data?.semesters ?? []).map(semester => ({ value: semester.id, label: semester.code || semester.name })))}</div>
      {options.isError && <div className="mt-3 flex flex-wrap items-center gap-2 text-body-sm text-slate-600" role="alert"><span>Could not load filter options.</span><Button variant="outline" size="sm" onClick={() => { void options.refetch(); }}>Retry filters</Button></div>}
      <div className="mt-5 flex items-center gap-2"><p aria-live="polite" className="text-body-sm font-semibold text-secondary">{gallery.isSuccess ? `${gallery.data.totalItems} ${gallery.data.totalItems === 1 ? 'project' : 'projects'}` : gallery.isPending ? 'Loading projects…' : 'Projects unavailable'}</p>{hasFilters && <Button variant="ghost-primary" size="sm" onClick={clearFilters}>Clear filters</Button>}</div>
      {gallery.isPending ? <section className="mt-5 grid gap-5 sm:grid-cols-2 lg:grid-cols-3" role="status" aria-label="Loading projects">{Array.from({ length: 6 }, (_, index) => <LoadingSkeleton key={index} variant="card" />)}</section> : gallery.isError ? <div className="mt-5"><ErrorState title="Could not load projects" message={parseApiError(gallery.error, 'Please try loading projects again.').message} onRetry={() => { void gallery.refetch(); }} /></div> : items.length > 0 ? <section aria-label="Published projects" className="mt-5 grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">{items.map(project => <ProjectCard key={project.id} project={project} />)}</section> : <section className="mt-5 rounded-2xl border border-slate-200 bg-white p-10 text-center shadow-card"><h3 className="text-subheading text-secondary">{page > 1 ? 'No projects on this page' : hasFilters ? 'No matching projects found' : 'No published projects yet'}</h3><p className="mt-1 text-body-sm text-slate-600">{hasFilters ? 'Try another search or clear the filters.' : 'Approved showcases will appear here.'}</p>{page > 1 ? <Button variant="outline" size="sm" className="mt-3" onClick={() => updateParam('page', '1')}>Back to first page</Button> : hasFilters && <Button variant="ghost-primary" size="sm" onClick={clearFilters}>Clear filters</Button>}</section>}
      {gallery.isSuccess && totalPages > 1 && page <= totalPages && <nav aria-label="Pagination" className="mt-8 flex flex-wrap items-center justify-center gap-2"><Button variant="outline" size="sm" disabled={page === 1} onClick={() => updateParam('page', String(page - 1))}>Previous</Button>{discoveryPages(page, totalPages).map(number => <Button key={number} variant={number === page ? 'primary' : 'outline'} size="sm" aria-current={number === page ? 'page' : undefined} onClick={() => updateParam('page', String(number))}>{number}</Button>)}<span className="text-caption text-slate-600">Page {page} of {totalPages}</span><Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => updateParam('page', String(page + 1))}>Next</Button></nav>}
    </section>
  </main>;
}
