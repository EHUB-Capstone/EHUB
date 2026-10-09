import { ArrowDown, ArrowLeft, ArrowRight, ArrowUp, ArrowUpDown } from 'lucide-react';
import Button from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';
import {
  PROJECT_DATA_EMPTY_VALUE,
  displayList,
  displayText,
  mentorName,
  pageSummary,
  summarizeList,
} from '../../../utils/projectData';
import { PROJECT_DATA_PAGE_SIZES } from '../../../types/projectData';
import type {
  ProjectDataItem,
  ProjectDataMentor,
  ProjectDataPage,
  ProjectDataPageSize,
  ProjectDataQuery,
  ProjectDataSortField,
} from '../../../types/projectData';
import ProjectAchievementBadges from './ProjectAchievementBadges';

const headerClass = 'px-4 py-3 text-left text-xs font-semibold uppercase tracking-wider text-slate-400 whitespace-nowrap';
const cellClass = 'px-4 py-3 align-top text-sm text-slate-700';

function ChipList({ values }: { values: readonly string[] }) {
  const { shown, hidden } = summarizeList(values);
  if (shown.length === 0) return <span className="text-slate-400">{PROJECT_DATA_EMPTY_VALUE}</span>;
  return (
    <span className="flex flex-wrap gap-1" title={displayList(values)}>
      {shown.map(value => (
        <span key={value} className="max-w-[10rem] truncate rounded-md bg-slate-100 px-1.5 py-0.5 text-xs text-slate-600">{value}</span>
      ))}
      {hidden > 0 && <span className="rounded-md bg-slate-100 px-1.5 py-0.5 text-xs font-medium text-slate-500">+{hidden}</span>}
    </span>
  );
}

export function MentorLabel({ mentor }: { mentor: ProjectDataMentor | null }) {
  if (!mentor) return <span className="text-slate-400">{PROJECT_DATA_EMPTY_VALUE}</span>;
  return (
    <span className="flex flex-col">
      <span>{mentorName(mentor)}</span>
      {mentor.isHistorical && (
        <span
          className="text-[11px] font-medium text-slate-400"
          title={mentor.endedAtUtc ? `Assignment ended ${new Date(mentor.endedAtUtc).toLocaleDateString()}` : 'Assignment ended'}
        >
          Assignment ended
        </span>
      )}
    </span>
  );
}

interface SortHeaderProps {
  label: string;
  field: ProjectDataSortField;
  query: ProjectDataQuery;
  onSort: (field: ProjectDataSortField) => void;
}

function SortHeader({ label, field, query, onSort }: SortHeaderProps) {
  const active = query.sortBy === field;
  const Icon = !active ? ArrowUpDown : query.isDescending ? ArrowDown : ArrowUp;
  return (
    <th scope="col" className={headerClass} aria-sort={!active ? 'none' : query.isDescending ? 'descending' : 'ascending'}>
      <button
        type="button"
        onClick={() => onSort(field)}
        className={cn('inline-flex items-center gap-1 uppercase tracking-wider hover:text-slate-700', active && 'text-slate-700')}
      >
        {label}
        <Icon className="h-3 w-3" aria-hidden="true" />
      </button>
    </th>
  );
}

interface ProjectDataTableProps {
  page: ProjectDataPage;
  query: ProjectDataQuery;
  isRefreshing: boolean;
  onSort: (field: ProjectDataSortField) => void;
  onOpen: (item: ProjectDataItem) => void;
  onPageChange: (pageIndex: number) => void;
  onPageSizeChange: (pageSize: ProjectDataPageSize) => void;
}

export default function ProjectDataTable({
  page,
  query,
  isRefreshing,
  onSort,
  onOpen,
  onPageChange,
  onPageSizeChange,
}: ProjectDataTableProps) {
  const totalPages = Math.max(page.totalPages, 1);

  return (
    <div className="overflow-hidden rounded-2xl border border-slate-200/60 bg-white shadow-sm">
      <div className="relative" aria-busy={isRefreshing}>
        {/* Desktop and tablet: the full ten-column table. */}
        <div className={cn('hidden overflow-x-auto transition-opacity md:block', isRefreshing && 'opacity-60')} tabIndex={0} aria-label="Scrollable project data table">
          <table className="w-full min-w-[1180px] border-collapse">
            <thead>
              <tr className="border-b border-slate-100 bg-slate-50/60">
                <SortHeader label="Semester" field="semester" query={query} onSort={onSort} />
                <SortHeader label="Class Code" field="classCode" query={query} onSort={onSort} />
                <SortHeader label="Group" field="group" query={query} onSort={onSort} />
                <SortHeader label="Project Name" field="projectName" query={query} onSort={onSort} />
                <th scope="col" className={headerClass}>Description</th>
                <th scope="col" className={headerClass}>Startup Industry</th>
                <th scope="col" className={headerClass}>Lecturer</th>
                <th scope="col" className={headerClass}>Mentor</th>
                <th scope="col" className={headerClass}>Mentor - GV</th>
                <th scope="col" className={headerClass}>Achievements</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {page.items.map(item => (
                <tr key={item.projectId} className="hover:bg-slate-50/60">
                  <td className={cn(cellClass, 'whitespace-nowrap')}>{displayText(item.semesterCode)}</td>
                  <td className={cn(cellClass, 'whitespace-nowrap')}>{displayText(item.classCode)}</td>
                  <td className={cellClass}><ChipList values={item.groups} /></td>
                  <td className={cn(cellClass, 'min-w-[10rem]')}>
                    <button type="button" onClick={() => onOpen(item)} className="text-left font-semibold text-primary hover:underline focus-visible:underline">
                      {item.projectName}
                    </button>
                  </td>
                  <td className={cn(cellClass, 'max-w-xs')}>
                    <p className="line-clamp-2 break-words" title={item.description ?? undefined}>{displayText(item.description)}</p>
                  </td>
                  <td className={cellClass}><ChipList values={item.startupIndustries} /></td>
                  <td className={cellClass}>{displayText(item.lecturer?.fullName)}</td>
                  <td className={cellClass}><MentorLabel mentor={item.mentor} /></td>
                  <td className={cellClass}><MentorLabel mentor={item.academicMentor} /></td>
                  <td className={cellClass}><ProjectAchievementBadges achievements={item.achievements} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Phones: identifying fields only; everything else lives in the detail dialog. */}
        <ul className={cn('divide-y divide-slate-100 md:hidden', isRefreshing && 'opacity-60')}>
          {page.items.map(item => (
            <li key={item.projectId} className="space-y-2 p-4">
              <button type="button" onClick={() => onOpen(item)} className="text-left text-sm font-semibold text-primary hover:underline">
                {item.projectName}
              </button>
              <p className="text-xs text-slate-500">
                {displayText(item.semesterCode)} · {displayText(item.classCode)} · {displayText(item.lecturer?.fullName)}
              </p>
              <ProjectAchievementBadges achievements={item.achievements} />
            </li>
          ))}
        </ul>
      </div>

      <div className="flex flex-col gap-3 border-t border-slate-100 bg-slate-50/50 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-xs text-slate-500" aria-live="polite">{pageSummary(page)}</p>
        <div className="flex flex-wrap items-center gap-3">
          <label className="flex items-center gap-2 text-xs text-slate-500">
            Rows per page
            <select
              value={query.pageSize}
              onChange={event => onPageSizeChange(Number(event.target.value) as ProjectDataPageSize)}
              className="h-8 rounded-lg border border-slate-200 bg-white px-2 text-xs text-slate-700 outline-none focus:border-primary"
            >
              {PROJECT_DATA_PAGE_SIZES.map(size => <option key={size} value={size}>{size}</option>)}
            </select>
          </label>
          <span className="text-xs text-slate-500">Page <strong className="text-slate-900">{page.pageIndex}</strong> of <strong className="text-slate-900">{totalPages}</strong></span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" icon={ArrowLeft} disabled={isRefreshing || page.pageIndex <= 1} onClick={() => onPageChange(page.pageIndex - 1)}>Prev</Button>
            <Button variant="outline" size="sm" iconRight={ArrowRight} disabled={isRefreshing || page.pageIndex >= totalPages} onClick={() => onPageChange(page.pageIndex + 1)}>Next</Button>
          </div>
        </div>
      </div>
    </div>
  );
}
