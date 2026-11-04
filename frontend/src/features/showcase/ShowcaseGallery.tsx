import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Flame, LayoutGrid, List, MessageSquare, Search, Sparkles, Star, TrendingUp, Users } from 'lucide-react';
import { Link, useSearchParams } from 'react-router-dom';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Input from '../../components/ui/Input';
import Modal from '../../components/ui/Modal';
import PageHeader from '../../components/ui/PageHeader';
import StatCard from '../../components/ui/StatCard';
import ErrorState from '../../components/ui/ErrorState';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import { showcaseApi } from '../../api/showcaseApi';
import type { ShowcaseCardDto } from '../../types/showcase';
import { SHOWCASE_STAGES, stageLabel } from '../../utils/showcase';
import { discoveryPages, discoveryRequest } from '../../utils/showcaseDiscovery';
import { parseApiError } from '../../utils/apiError';
import { cn } from '../../utils/cn';

const selectClass = 'h-10 w-full rounded-lg border border-slate-300 bg-white px-3.5 text-body text-slate-900 transition-colors hover:border-slate-400 focus:border-primary-600 focus:outline-none focus:ring-2 focus:ring-primary/30';
const stageTone = (stage: ShowcaseCardDto['developmentStage']) =>
  ({ Idea: 'neutral', Mvp: 'success', Beta: 'info', Launched: 'warning' } as const)[stage];

function Logo({ project }: { project: ShowcaseCardDto }) {
  return project.logoUrl
    ? <img src={project.logoUrl} alt={`${project.projectName} logo`} className="h-full w-full object-contain" />
    : <span className="text-heading text-secondary-400" aria-hidden="true">{project.projectName.slice(0, 2).toUpperCase()}</span>;
}

function Metrics({ project }: { project: ShowcaseCardDto }) {
  return <span className="inline-flex items-center gap-3 text-caption text-slate-600">
    <span className="inline-flex items-center gap-1" aria-label={`${project.likeCount} interested`}><Flame className="h-4 w-4" aria-hidden="true" />{project.likeCount}</span>
    <span className="inline-flex items-center gap-1" aria-label={`${project.commentCount} comments`}><MessageSquare className="h-4 w-4" aria-hidden="true" />{project.commentCount}</span>
  </span>;
}

function ProjectItem({ project, list, onOpen }: { project: ShowcaseCardDto; list: boolean; onOpen: () => void }) {
  return <article className={cn('group relative flex overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-card transition-shadow hover:shadow-elevated has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-primary/40', list ? 'flex-col gap-4 p-4 sm:flex-row sm:items-center' : 'flex-col')}>
    <div className={cn('flex shrink-0 items-center justify-center bg-secondary-50', list ? 'h-16 w-16 rounded-xl' : 'h-36 p-4')}><Logo project={project} /></div>
    <div className={cn('flex min-w-0 flex-1 flex-col', list ? '' : 'p-4')}>
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="text-subheading text-secondary"><button type="button" onClick={onOpen} className="text-left outline-none after:absolute after:inset-0">{project.projectName}</button></h2>
        <Badge size="sm" variant={stageTone(project.developmentStage)}>{stageLabel(project.developmentStage)}</Badge>
      </div>
      <p className="mt-1.5 line-clamp-2 text-body-sm text-slate-600">{project.description}</p>
      <div className="mt-3 flex flex-wrap gap-1.5">{project.industries.map(industry => <Badge key={industry} size="sm" variant="neutral">{industry}</Badge>)}</div>
      <div className="mt-auto flex flex-wrap items-center justify-between gap-2 pt-4"><span className="text-caption text-slate-600">{project.teamName}</span><Metrics project={project} /></div>
      {project.publishedAt && <p className="mt-2 text-caption text-slate-500">Published {new Date(project.publishedAt).toLocaleDateString()}</p>}
    </div>
  </article>;
}

function ProjectDetails({ id }: { id: string }) {
  const detail = useQuery({ queryKey: ['showcase', 'detail', id], queryFn: ({ signal }) => showcaseApi.get(id, signal), staleTime: 0 });
  if (detail.isPending) return <div role="status" aria-label="Loading project details"><LoadingSkeleton lines={6} /></div>;
  if (detail.isError) return <ErrorState title="Could not load project" message={parseApiError(detail.error, 'Unable to load this project.').message} onRetry={() => { void detail.refetch(); }} />;
  const { showcase, teamName, members } = detail.data;
  const { profile } = showcase;
  return <div className="space-y-5">
    <div><h2 className="text-heading text-secondary">{profile.projectName}</h2><p className="mt-2 text-body text-slate-600">{profile.description}</p></div>
    <Badge variant={stageTone(showcase.developmentStage)}>{stageLabel(showcase.developmentStage)}</Badge>
    {showcase.media.length > 0 && <div className="grid gap-3 sm:grid-cols-2">{showcase.media.filter(media => media.mediaType === 'Image').map(media => <img key={media.id} src={media.url} alt={media.caption || profile.projectName} className="w-full rounded-xl object-contain" />)}</div>}
    {([['Problem', profile.problem], ['Target users', profile.targetUsers], ['Solution', profile.solution], ['Highlights', showcase.highlights]] as const).map(([label, value]) => value && <section key={label}><h3 className="text-body font-semibold text-secondary">{label}</h3><p className="mt-1 whitespace-pre-line text-body-sm text-slate-600">{value}</p></section>)}
    <section><h3 className="flex items-center gap-2 text-body font-semibold text-secondary"><Users className="h-4 w-4" aria-hidden="true" />{teamName}</h3>
      {members.length > 0 && <ul className="mt-2 space-y-1">{members.map((member, index) => <li key={`${member.fullName}-${index}`} className="text-body-sm text-slate-600">{member.fullName} · {member.role}</li>)}</ul>}
    </section>
    {showcase.links.length > 0 && <div className="flex flex-wrap gap-3">{showcase.links.map(link => <a key={link.id} href={link.url} target="_blank" rel="noopener noreferrer" className="text-body-sm font-semibold text-primary-700 underline">{link.label}</a>)}</div>}
    <Link to={`/showcase/${id}`} className="inline-flex text-body-sm font-semibold text-primary-700 underline">Open full project page</Link>
  </div>;
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

export default function ShowcaseGallery(): React.ReactElement {
  const [params, setParams] = useSearchParams();
  const request = discoveryRequest(params);
  const keyword = params.get('q') ?? '';
  const [searchReset, setSearchReset] = useState(0);
  const [openId, setOpenId] = useState<string | null>(null);
  const list = params.get('view') === 'list';
  const tab = params.get('tab') === 'new' ? 'new' : 'all';
  const gallery = useQuery({ queryKey: ['showcase', 'discovery', request], queryFn: ({ signal }) => showcaseApi.list(request, signal), staleTime: 0 });
  const options = useQuery({ queryKey: ['showcase', 'filter-options'], queryFn: ({ signal }) => showcaseApi.getFilterOptions(signal), staleTime: 0 });

  const updateParam = (key: string, value: string) => setParams(current => {
    const next = new URLSearchParams(current);
    if (value) next.set(key, value); else next.delete(key);
    if (key !== 'page' && key !== 'view') next.delete('page');
    if (key === 'sort') next.delete('tab');
    if (key === 'tab' && value === 'new') next.set('sort', 'newest');
    return next;
  });
  const clearFilters = () => {
    setSearchReset(current => current + 1);
    setParams(current => {
      const next = new URLSearchParams(current);
      for (const key of ['q', 'stage', 'industry', 'semesterId', 'semester', 'page']) next.delete(key);
      return next;
    });
  };
  const items = gallery.data?.items ?? [];
  const totalPages = gallery.data?.totalPages ?? 0;
  const page = request.page ?? 1;
  const hasFilters = Boolean(request.search || request.stage || request.industry || request.semesterId);
  const selectOptions = (label: string, key: string, value: string, choices: ReadonlyArray<{ value: string; label: string }>) => <label>
    <span className="mb-1.5 block text-body-sm font-semibold text-secondary">{label}</span>
    <select className={selectClass} value={value} onChange={event => updateParam(key, event.target.value)} disabled={options.isPending && key !== 'stage'}>
      <option value="">All</option>
      {value && !choices.some(choice => choice.value === value) && <option value={value}>{key === 'semesterId' ? 'Selected semester' : value}</option>}
      {choices.map(choice => <option key={choice.value} value={choice.value}>{choice.label}</option>)}
    </select>
  </label>;

  return <main className="mx-auto max-w-[96rem] px-4 py-6 sm:px-6 sm:py-8 lg:px-8">
    <PageHeader title="Project Showcase" subtitle="Discover standout student startup projects." />
    <section aria-label="Project summary" className="mb-6 grid grid-cols-2 gap-4 lg:grid-cols-4">
      <StatCard title="Featured · Coming soon" value="—" icon={Star} color="primary" />
      <StatCard title="Newest" value="—" icon={Sparkles} color="info" selected={tab === 'new'} onSelect={() => updateParam('tab', 'new')} />
      <StatCard title="Trending · Coming soon" value="—" icon={TrendingUp} color="warning" />
      <StatCard title="Matching projects" value={gallery.isSuccess ? gallery.data.totalItems : '—'} icon={LayoutGrid} color="navy" selected={tab === 'all'} onSelect={() => updateParam('tab', '')} />
    </section>
    <section aria-label="Search and filter projects" className="grid gap-4 sm:grid-cols-2 lg:grid-cols-[minmax(0,2fr)_repeat(3,minmax(0,1fr))]">
      <div className="sm:col-span-2 lg:col-span-1"><DiscoverySearch key={`${keyword}-${searchReset}`} keyword={keyword} /></div>
      {selectOptions('Stage', 'stage', request.stage ?? '', SHOWCASE_STAGES)}
      {selectOptions('Industry', 'industry', request.industry ?? '', (options.data?.industries ?? []).map(value => ({ value, label: value })))}
      {selectOptions('Semester', 'semesterId', request.semesterId ?? '', (options.data?.semesters ?? []).map(semester => ({ value: semester.id, label: semester.code || semester.name })))}
    </section>
    {options.isError && <div className="mt-3 flex flex-wrap items-center gap-2 text-body-sm text-slate-600" role="alert"><span>Could not load filter options.</span><Button variant="outline" size="sm" onClick={() => { void options.refetch(); }}>Retry filters</Button></div>}
    <nav aria-label="Project type" className="mt-8 flex gap-6 border-b border-slate-200">
      {(['featured', 'new', 'trending', 'all'] as const).map(value => {
        const disabled = value === 'featured' || value === 'trending';
        return <button key={value} type="button" disabled={disabled} title={disabled ? 'Coming soon' : undefined} onClick={() => updateParam('tab', value === 'all' ? '' : value)} aria-current={tab === value ? 'page' : undefined} className={cn('border-b-2 px-1 pb-3 text-body-sm font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-50', tab === value ? 'border-primary text-primary-700' : 'border-transparent text-slate-500 hover:text-secondary')}>{value === 'new' ? 'New' : value.charAt(0).toUpperCase() + value.slice(1)}</button>;
      })}
    </nav>
    <div className="mt-5 flex flex-wrap items-center justify-between gap-3">
      <div className="flex items-center gap-2"><p aria-live="polite" className="text-body-sm font-semibold text-secondary">{gallery.isSuccess ? `${gallery.data.totalItems} projects` : gallery.isPending ? 'Loading projects…' : 'Projects unavailable'}</p>{hasFilters && <Button variant="ghost-primary" size="sm" onClick={clearFilters}>Clear filters</Button>}</div>
      <div className="flex items-center gap-3">
        <label className="flex items-center gap-2 text-body-sm text-slate-600"><span>Sort by</span><select value={request.sort} onChange={event => updateParam('sort', event.target.value)} className={cn(selectClass, 'w-auto')}><option value="newest">Newest published</option><option value="popular">Most popular</option></select></label>
        <div role="group" aria-label="Layout" className="flex overflow-hidden rounded-lg border border-slate-300 bg-white">{([['grid', LayoutGrid], ['list', List]] as const).map(([value, Icon]) => <button key={value} type="button" aria-label={`${value} view`} aria-pressed={list === (value === 'list')} onClick={() => updateParam('view', value)} className={cn('flex h-10 w-10 items-center justify-center', list === (value === 'list') ? 'bg-primary-50 text-primary-700' : 'text-slate-500 hover:bg-slate-50')}><Icon className="h-4 w-4" aria-hidden="true" /></button>)}</div>
      </div>
    </div>
    {gallery.isPending ? <section className="mt-5 grid gap-5 sm:grid-cols-2 lg:grid-cols-4" role="status" aria-label="Loading projects">{Array.from({ length: 4 }, (_, index) => <LoadingSkeleton key={index} variant="card" />)}</section>
      : gallery.isError ? <div className="mt-5"><ErrorState title="Could not load projects" message={parseApiError(gallery.error, 'Please try loading projects again.').message} onRetry={() => { void gallery.refetch(); }} /></div>
      : items.length > 0 ? <section aria-label="Published projects" className={cn('mt-5', list ? 'flex flex-col gap-3' : 'grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 2xl:grid-cols-5')}>{items.map(project => <ProjectItem key={project.id} project={project} list={list} onOpen={() => setOpenId(project.id)} />)}</section>
      : <section className="mt-5 rounded-2xl border border-slate-200 bg-white p-10 text-center shadow-card"><h2 className="text-subheading text-secondary">{page > 1 ? 'No projects on this page' : hasFilters ? 'No matching projects found' : 'No published projects yet'}</h2><p className="mt-1 text-body-sm text-slate-600">{hasFilters ? 'Try another search or clear the filters.' : 'Approved showcases will appear here.'}</p>{page > 1 ? <Button variant="outline" size="sm" className="mt-3" onClick={() => updateParam('page', '1')}>Back to first page</Button> : hasFilters && <Button variant="ghost-primary" size="sm" className="mt-3" onClick={clearFilters}>Clear filters</Button>}</section>}
    {gallery.isSuccess && totalPages > 1 && page <= totalPages && <nav aria-label="Pagination" className="mt-8 flex flex-wrap items-center justify-center gap-2">
      <Button variant="outline" size="sm" disabled={page === 1} onClick={() => updateParam('page', String(page - 1))}>Previous</Button>
      {discoveryPages(page, totalPages).map(number => <Button key={number} variant={number === page ? 'primary' : 'outline'} size="sm" aria-current={number === page ? 'page' : undefined} onClick={() => updateParam('page', String(number))}>{number}</Button>)}
      <span className="text-caption text-slate-600">Page {page} of {totalPages}</span><Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => updateParam('page', String(page + 1))}>Next</Button>
    </nav>}
    <Modal isOpen={openId !== null} onClose={() => setOpenId(null)} title="Project details" size="lg">{openId && <ProjectDetails key={openId} id={openId} />}</Modal>
  </main>;
}
