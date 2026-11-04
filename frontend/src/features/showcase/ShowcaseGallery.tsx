import { useEffect, useMemo, useState } from 'react';
import { Flame, LayoutGrid, List, Search, Sparkles, Star, TrendingUp, Users, X } from 'lucide-react';
import { useSearchParams } from 'react-router-dom';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Input from '../../components/ui/Input';
import Modal from '../../components/ui/Modal';
import PageHeader from '../../components/ui/PageHeader';
import StatCard from '../../components/ui/StatCard';
import { cn } from '../../utils/cn';

type ShowcaseTab = 'featured' | 'new' | 'trending' | 'all';
type ShowcaseSort = 'popular' | 'newest' | 'name';
type ShowcaseView = 'grid' | 'list';

interface ShowcaseProject {
  id: string;
  name: string;
  initial: string;
  stage: 'MVP' | 'Prototype' | 'Launch' | 'Idea';
  industry: string;
  semester: string;
  tagline: string;
  tags: string[];
  members: string[];
  interest: number;
  isFeatured?: boolean;
}

const projects: ShowcaseProject[] = [
  { id: 'heal-link', name: 'HealLink', initial: 'He', stage: 'MVP', industry: 'HealthTech', semester: 'FA2026', tagline: 'Appointment booking and health records platform for students.', tags: ['React', 'ASP.NET', 'PostgreSQL'], members: ['An Nguyen', 'Binh Tran', 'Chi Le', 'Dung Pham'], interest: 238, isFeatured: true },
  { id: 'eduflow', name: 'EduFlow', initial: 'Ed', stage: 'Prototype', industry: 'EdTech', semester: 'FA2026', tagline: 'AI-powered learning and coding assistant for university classes.', tags: ['AI', 'Python', 'React'], members: ['Em Vo', 'Phuc Do', 'Giang Ho'], interest: 98, isFeatured: true },
  { id: 'green-cart', name: 'GreenCart', initial: 'Gr', stage: 'Launch', industry: 'AgriTech', semester: 'SP2026', tagline: 'Clean-produce marketplace connecting local farmers with students.', tags: ['Marketplace', 'Flutter'], members: ['Hoa Bui', 'Khoa Dang', 'Lan Ngo'], interest: 87 },
  { id: 'paynest', name: 'PayNest', initial: 'Pa', stage: 'MVP', industry: 'FinTech', semester: 'FA2026', tagline: 'Group expense-splitting wallet for housemates and clubs.', tags: ['Payments', 'React', 'Node.js'], members: ['Minh Truong', 'Nam Ly'], interest: 74 },
  { id: 'medtrack', name: 'MedTrack', initial: 'Me', stage: 'Idea', industry: 'HealthTech', semester: 'SU2026', tagline: 'Medication reminders and health tracking for the elderly.', tags: ['Mobile', 'IoT'], members: ['Oanh Mai', 'Quan Dinh', 'Son Vu'], interest: 51 },
  { id: 'campusride', name: 'CampusRide', initial: 'Ca', stage: 'Prototype', industry: 'Mobility', semester: 'FA2026', tagline: 'Ride sharing around campus and dormitories.', tags: ['Maps', 'Flutter', 'Realtime'], members: ['Thanh Cao', 'Uyen Ta', 'Viet Lam', 'Xuan Ha', 'Yen Phan'], interest: 52 },
];

const tabs: { value: ShowcaseTab; label: string }[] = [
  { value: 'featured', label: 'Featured' }, { value: 'new', label: 'New' },
  { value: 'trending', label: 'Trending' }, { value: 'all', label: 'All' },
];

const sortOptions: { value: ShowcaseSort; label: string }[] = [
  { value: 'popular', label: 'Most popular' },
  { value: 'newest', label: 'Newest semester' },
  { value: 'name', label: 'Name (A–Z)' },
];

const NEW_SEMESTER = 'FA2026';
const TRENDING_MIN_INTEREST = 70;
const PAGE_SIZE = 8;
const MAX_AVATARS = 3;

const stageTone = (stage: ShowcaseProject['stage']) => ({ MVP: 'success', Prototype: 'info', Launch: 'warning', Idea: 'neutral' })[stage];

// Semester codes look like FA2026 / SU2026 / SP2026; rank by year, then term within the year.
const semesterRank = (semester: string) => {
  const term = { SP: 1, SU: 2, FA: 3 }[semester.slice(0, 2) as 'SP' | 'SU' | 'FA'] ?? 0;
  return Number(semester.slice(2)) * 10 + term;
};

const initialsOf = (name: string) => name.split(' ').map(part => part[0]).join('').slice(0, 2).toUpperCase();

const selectClass = 'h-10 w-full rounded-lg border border-slate-300 bg-white px-3.5 text-body text-slate-900 transition-colors hover:border-slate-400 focus:border-primary-600 focus:outline-none focus:ring-2 focus:ring-primary/30';

interface FilterSelectProps {
  label: string;
  value: string;
  options: string[];
  onChange: (value: string) => void;
}

function FilterSelect({ label, value, options, onChange }: FilterSelectProps) {
  return (
    <label>
      <span className="mb-1.5 block text-body-sm font-semibold text-secondary">{label}</span>
      <select value={value} onChange={event => onChange(event.target.value)} className={selectClass}>
        <option value="">All</option>
        {options.map(option => <option key={option}>{option}</option>)}
      </select>
    </label>
  );
}

function TeamAvatars({ members }: { members: string[] }) {
  const shown = members.slice(0, MAX_AVATARS);
  const extra = members.length - shown.length;
  return (
    <span className="inline-flex items-center gap-2 text-caption text-slate-600">
      <span className="flex -space-x-2" aria-hidden="true">
        {shown.map(member => (
          <span key={member} className="flex h-7 w-7 items-center justify-center rounded-full border-2 border-white bg-secondary-100 text-overline text-secondary">{initialsOf(member)}</span>
        ))}
        {extra > 0 && <span className="flex h-7 w-7 items-center justify-center rounded-full border-2 border-white bg-slate-100 text-overline text-slate-600">+{extra}</span>}
      </span>
      <span>{members.length} members</span>
    </span>
  );
}

interface ProjectItemProps {
  project: ShowcaseProject;
  interest: number;
  isInterested: boolean;
  onToggleInterest: () => void;
  onOpen: () => void;
}

function InterestButton({ interest, isInterested, onToggleInterest, name }: Pick<ProjectItemProps, 'interest' | 'isInterested' | 'onToggleInterest'> & { name: string }) {
  return (
    <Button
      size="sm"
      variant={isInterested ? 'ghost-primary' : 'outline'}
      icon={Flame}
      aria-pressed={isInterested}
      aria-label={`${isInterested ? 'Remove interest in' : 'Show interest in'} ${name}`}
      className="relative z-10"
      onClick={onToggleInterest}
    >
      {interest}
    </Button>
  );
}

// Behance / Devpost style: cover-first card with a hover overlay, team and interest in the footer.
function ProjectCard({ project, interest, isInterested, onToggleInterest, onOpen }: ProjectItemProps) {
  return (
    <article className="group relative flex flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-card transition-all duration-200 hover:-translate-y-0.5 hover:shadow-elevated has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-primary/40">
      <div className="relative flex h-36 items-center justify-center bg-secondary-50">
        <span className="text-display text-secondary-400">{project.initial}</span>
        <div className="absolute left-3 top-3 flex gap-1.5">
          <Badge size="sm" variant={stageTone(project.stage)}>{project.stage}</Badge>
        </div>
        {project.isFeatured && <Badge size="sm" variant="brand" className="absolute right-3 top-3"><Star className="h-3 w-3" aria-hidden="true" />Featured</Badge>}
        <span aria-hidden="true" className="pointer-events-none absolute inset-0 flex items-center justify-center bg-secondary/60 text-body font-semibold text-white opacity-0 transition-opacity duration-200 group-hover:opacity-100 group-has-[:focus-visible]:opacity-100">View project</span>
      </div>
      <div className="flex flex-1 flex-col p-4">
        <p className="text-overline uppercase text-slate-500">{project.industry} · {project.semester}</p>
        <h2 className="mt-1 text-subheading text-secondary">
          <button type="button" onClick={onOpen} className="text-left outline-none after:absolute after:inset-0">{project.name}</button>
        </h2>
        <p className="mt-1.5 line-clamp-2 min-h-11 text-body-sm text-slate-600">{project.tagline}</p>
        <div className="mt-3 flex flex-wrap gap-1.5">
          {project.tags.map(tag => <Badge key={tag} size="sm" variant="neutral">{tag}</Badge>)}
        </div>
        <div className="mt-auto flex items-center justify-between gap-2 pt-4">
          <TeamAvatars members={project.members} />
          <InterestButton name={project.name} interest={interest} isInterested={isInterested} onToggleInterest={onToggleInterest} />
        </div>
      </div>
    </article>
  );
}

// Wellfound style: one compact row per startup with logo, summary, tags and a quick action.
function ProjectRow({ project, interest, isInterested, onToggleInterest, onOpen }: ProjectItemProps) {
  return (
    <article className="relative flex flex-col gap-4 rounded-2xl border border-slate-200 bg-white p-4 shadow-card transition-shadow hover:shadow-elevated has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-primary/40 sm:flex-row sm:items-center">
      <div className="flex h-16 w-16 shrink-0 items-center justify-center rounded-xl bg-secondary-50">
        <span className="text-heading text-secondary-400">{project.initial}</span>
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="text-subheading text-secondary">
            <button type="button" onClick={onOpen} className="text-left outline-none after:absolute after:inset-0">{project.name}</button>
          </h2>
          <Badge size="sm" variant={stageTone(project.stage)}>{project.stage}</Badge>
          {project.isFeatured && <Badge size="sm" variant="brand"><Star className="h-3 w-3" aria-hidden="true" />Featured</Badge>}
        </div>
        <p className="mt-1 line-clamp-1 text-body-sm text-slate-600">{project.tagline}</p>
        <div className="mt-2 flex flex-wrap items-center gap-1.5">
          <Badge size="sm" variant="neutral">{project.industry}</Badge>
          <Badge size="sm" variant="neutral">{project.semester}</Badge>
          {project.tags.map(tag => <Badge key={tag} size="sm" variant="neutral">{tag}</Badge>)}
        </div>
      </div>
      <div className="flex shrink-0 items-center justify-between gap-4 sm:justify-end">
        <TeamAvatars members={project.members} />
        <InterestButton name={project.name} interest={interest} isInterested={isInterested} onToggleInterest={onToggleInterest} />
      </div>
    </article>
  );
}

function ProjectDetails({ project, interest, isInterested, onToggleInterest }: Omit<ProjectItemProps, 'onOpen'>) {
  const facts: [string, string][] = [['Stage', project.stage], ['Industry', project.industry], ['Semester', project.semester], ['Team size', `${project.members.length} members`]];
  return (
    <div className="space-y-5">
      <div className="flex h-40 items-center justify-center rounded-xl bg-secondary-50">
        <span className="text-display text-secondary-400">{project.initial}</span>
      </div>
      <p className="text-body text-slate-600">{project.tagline}</p>
      <dl className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        {facts.map(([term, description]) => (
          <div key={term}>
            <dt className="text-overline uppercase text-slate-500">{term}</dt>
            <dd className="mt-1 text-body font-semibold text-secondary">{description}</dd>
          </div>
        ))}
      </dl>
      <div>
        <h3 className="mb-2 text-body-sm font-semibold text-secondary">Built with</h3>
        <div className="flex flex-wrap gap-1.5">{project.tags.map(tag => <Badge key={tag} size="sm" variant="neutral">{tag}</Badge>)}</div>
      </div>
      <div>
        <h3 className="mb-2 flex items-center gap-1.5 text-body-sm font-semibold text-secondary"><Users className="h-4 w-4" aria-hidden="true" />Team</h3>
        <ul className="grid gap-2 sm:grid-cols-2">
          {project.members.map(member => (
            <li key={member} className="flex items-center gap-2 text-body-sm text-slate-700">
              <span className="flex h-7 w-7 items-center justify-center rounded-full bg-secondary-100 text-overline text-secondary" aria-hidden="true">{initialsOf(member)}</span>{member}
            </li>
          ))}
        </ul>
      </div>
      <div className="flex items-center justify-between border-t border-slate-200 pt-4">
        <span className="text-caption text-slate-600">{interest} people are interested</span>
        <InterestButton name={project.name} interest={interest} isInterested={isInterested} onToggleInterest={onToggleInterest} />
      </div>
    </div>
  );
}

export default function ShowcaseGallery(): React.ReactElement {
  const [params, setParams] = useSearchParams();
  const [searchInput, setSearchInput] = useState(params.get('q') ?? '');
  const [interested, setInterested] = useState<Set<string>>(() => new Set());
  const [openId, setOpenId] = useState<string | null>(null);
  const tab = (params.get('tab') as ShowcaseTab | null) ?? 'featured';
  const sort = (params.get('sort') as ShowcaseSort | null) ?? 'popular';
  const view: ShowcaseView = params.get('view') === 'list' ? 'list' : 'grid';
  const stage = params.get('stage') ?? '';
  const industry = params.get('industry') ?? '';
  const semester = params.get('semester') ?? '';
  const keyword = params.get('q') ?? '';
  const page = Math.max(1, Number(params.get('page')) || 1);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      const next = new URLSearchParams(params);
      if (searchInput.trim()) next.set('q', searchInput.trim()); else next.delete('q');
      if (next.toString() !== params.toString()) {
        next.delete('page');
        setParams(next, { replace: true });
      }
    }, 300);
    return () => window.clearTimeout(timer);
  }, [searchInput, params, setParams]); // Search intentionally writes after debounce; filters write immediately below.

  const updateParam = (key: string, value: string) => {
    const next = new URLSearchParams(params);
    if (value) next.set(key, value); else next.delete(key);
    if (key !== 'page' && key !== 'view') next.delete('page'); // Any change to the result set returns to page 1.
    setParams(next);
  };

  const clearFilters = () => {
    setSearchInput('');
    const next = new URLSearchParams();
    if (tab !== 'featured') next.set('tab', tab);
    setParams(next);
  };

  const interestOf = (project: ShowcaseProject) => project.interest + (interested.has(project.id) ? 1 : 0);
  const toggleInterest = (id: string) => setInterested(current => {
    const next = new Set(current);
    if (next.has(id)) next.delete(id); else next.add(id);
    return next;
  });

  const matching = useMemo(() => {
    const needle = keyword.toLocaleLowerCase();
    return projects.filter(project =>
      (!needle || `${project.name} ${project.tagline} ${project.industry} ${project.tags.join(' ')}`.toLocaleLowerCase().includes(needle))
      && (!stage || project.stage === stage)
      && (!industry || project.industry === industry)
      && (!semester || project.semester === semester));
  }, [keyword, stage, industry, semester]);

  const counts = useMemo(() => ({
    featured: matching.filter(project => project.isFeatured).length,
    new: matching.filter(project => project.semester === NEW_SEMESTER).length,
    trending: matching.filter(project => project.interest >= TRENDING_MIN_INTEREST).length,
    all: matching.length,
  }), [matching]);

  const sortedProjects = useMemo(() => {
    const inTab = matching.filter(project => {
      if (tab === 'featured') return project.isFeatured;
      if (tab === 'new') return project.semester === NEW_SEMESTER;
      if (tab === 'trending') return project.interest >= TRENDING_MIN_INTEREST;
      return true;
    });
    const interestValue = (project: ShowcaseProject) => project.interest + (interested.has(project.id) ? 1 : 0);
    return [...inTab].sort((a, b) => {
      if (sort === 'name') return a.name.localeCompare(b.name);
      if (sort === 'newest') return semesterRank(b.semester) - semesterRank(a.semester) || interestValue(b) - interestValue(a);
      return interestValue(b) - interestValue(a);
    });
  }, [matching, tab, sort, interested]);

  const pageCount = Math.max(1, Math.ceil(sortedProjects.length / PAGE_SIZE));
  const currentPage = Math.min(page, pageCount);
  const visibleProjects = sortedProjects.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);
  const openProject = projects.find(project => project.id === openId) ?? null;

  const summaryCards: { value: ShowcaseTab; label: string; icon: typeof Star; color: 'primary' | 'info' | 'warning' | 'navy' }[] = [
    { value: 'featured', label: 'Featured', icon: Star, color: 'primary' },
    { value: 'new', label: 'New', icon: Sparkles, color: 'info' },
    { value: 'trending', label: 'Trending', icon: TrendingUp, color: 'warning' },
    { value: 'all', label: 'All', icon: LayoutGrid, color: 'navy' },
  ];

  const activeFilters = [
    keyword && { key: 'q', label: `“${keyword}”` },
    stage && { key: 'stage', label: `Stage: ${stage}` },
    industry && { key: 'industry', label: `Industry: ${industry}` },
    semester && { key: 'semester', label: `Semester: ${semester}` },
  ].filter((filter): filter is { key: string; label: string } => Boolean(filter));

  const itemProps = (project: ShowcaseProject): ProjectItemProps => ({
    project,
    interest: interestOf(project),
    isInterested: interested.has(project.id),
    onToggleInterest: () => toggleInterest(project.id),
    onOpen: () => setOpenId(project.id),
  });

  return (
    <main className="mx-auto max-w-[96rem] px-4 py-6 sm:px-6 sm:py-8 lg:px-8">
      <PageHeader title="Project Showcase" subtitle="Discover standout student startup projects." />

      <section aria-label="Project summary" className="mb-6 grid grid-cols-2 gap-4 lg:grid-cols-4">{summaryCards.map((card, index) => <StatCard key={card.value} title={card.label} value={counts[card.value]} icon={card.icon} color={card.color} delay={index * 0.05} selected={tab === card.value} onSelect={() => updateParam('tab', card.value)} />)}</section>

      <section aria-label="Search and filter projects" className="grid gap-4 sm:grid-cols-2 lg:grid-cols-[minmax(0,2fr)_repeat(3,minmax(0,1fr))]">
        <div className="sm:col-span-2 lg:col-span-1"><Input label="Search projects" icon={Search} value={searchInput} onChange={event => setSearchInput(event.target.value)} placeholder="Search by name, description, keyword..." /></div>
        <FilterSelect label="Stage" value={stage} options={['Idea', 'MVP', 'Prototype', 'Launch']} onChange={value => updateParam('stage', value)} />
        <FilterSelect label="Industry" value={industry} options={[...new Set(projects.map(project => project.industry))]} onChange={value => updateParam('industry', value)} />
        <FilterSelect label="Semester" value={semester} options={[...new Set(projects.map(project => project.semester))]} onChange={value => updateParam('semester', value)} />
      </section>

      <nav aria-label="Project type" className="mt-8 flex gap-6 border-b border-slate-200">
        {tabs.map(item => <button key={item.value} type="button" onClick={() => updateParam('tab', item.value)} aria-current={tab === item.value ? 'page' : undefined} className={cn('border-b-2 px-1 pb-3 text-body-sm font-semibold transition-colors', tab === item.value ? 'border-primary text-primary-700' : 'border-transparent text-slate-500 hover:text-secondary')}>{item.label}</button>)}
      </nav>

      <div className="mt-5 flex flex-wrap items-center justify-between gap-3">
        <div className="flex min-w-0 flex-wrap items-center gap-2">
          <p aria-live="polite" className="text-body-sm font-semibold text-secondary">{sortedProjects.length} {sortedProjects.length === 1 ? 'project' : 'projects'}</p>
          {activeFilters.map(filter => (
            <span key={filter.key} className="inline-flex items-center gap-1 rounded-full border border-primary-200 bg-primary-50 py-0.5 pl-2.5 pr-1 text-caption font-semibold text-primary-800">
              {filter.label}
              <button type="button" aria-label={`Remove filter ${filter.label}`} onClick={() => { if (filter.key === 'q') setSearchInput(''); updateParam(filter.key, ''); }} className="flex h-5 w-5 items-center justify-center rounded-full hover:bg-primary-100"><X className="h-3 w-3" aria-hidden="true" /></button>
            </span>
          ))}
          {activeFilters.length > 0 && <Button variant="ghost-primary" size="xs" onClick={clearFilters}>Clear all</Button>}
        </div>
        <div className="flex items-center gap-3">
          <label className="flex items-center gap-2 text-body-sm text-slate-600">
            <span className="font-semibold text-secondary">Sort by</span>
            <select value={sort} onChange={event => updateParam('sort', event.target.value === 'popular' ? '' : event.target.value)} className={cn(selectClass, 'w-auto')}>
              {sortOptions.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
            </select>
          </label>
          <div role="group" aria-label="Layout" className="flex overflow-hidden rounded-lg border border-slate-300 bg-white">
            {([['grid', LayoutGrid, 'Grid view'], ['list', List, 'List view']] as const).map(([value, Icon, label]) => (
              <button key={value} type="button" aria-label={label} aria-pressed={view === value} onClick={() => updateParam('view', value === 'grid' ? '' : value)} className={cn('flex h-10 w-10 items-center justify-center transition-colors', view === value ? 'bg-primary-50 text-primary-700' : 'text-slate-500 hover:bg-slate-50 hover:text-secondary')}><Icon className="h-4 w-4" aria-hidden="true" /></button>
            ))}
          </div>
        </div>
      </div>

      {visibleProjects.length > 0 ? (
        <section aria-live="polite" className={cn('mt-5', view === 'grid' ? 'grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 2xl:grid-cols-5' : 'flex flex-col gap-3')}>
          {visibleProjects.map(project => view === 'grid' ? <ProjectCard key={project.id} {...itemProps(project)} /> : <ProjectRow key={project.id} {...itemProps(project)} />)}
        </section>
      ) : (
        <section className="mt-5 rounded-2xl border border-slate-200 bg-white p-10 text-center shadow-card">
          <h2 className="text-subheading text-secondary">No matching projects found</h2>
          <p className="mt-1 text-body-sm text-slate-600">Try a different keyword or remove a filter.</p>
          <Button variant="ghost-primary" size="sm" className="mt-2" onClick={clearFilters}>Clear filters</Button>
        </section>
      )}

      {pageCount > 1 && (
        <nav aria-label="Pagination" className="mt-8 flex justify-center gap-2">
          <Button variant="outline" size="xs" aria-label="Previous page" disabled={currentPage === 1} onClick={() => updateParam('page', String(currentPage - 1))}>‹</Button>
          {Array.from({ length: pageCount }, (_, index) => index + 1).map(number => (
            <Button key={number} variant={number === currentPage ? 'primary' : 'outline'} size="xs" aria-current={number === currentPage ? 'page' : undefined} onClick={() => updateParam('page', String(number))}>{number}</Button>
          ))}
          <Button variant="outline" size="xs" aria-label="Next page" disabled={currentPage === pageCount} onClick={() => updateParam('page', String(currentPage + 1))}>›</Button>
        </nav>
      )}

      <Modal isOpen={openProject !== null} onClose={() => setOpenId(null)} title={openProject?.name} size="lg">
        {openProject && <ProjectDetails project={openProject} interest={interestOf(openProject)} isInterested={interested.has(openProject.id)} onToggleInterest={() => toggleInterest(openProject.id)} />}
      </Modal>
    </main>
  );
}
