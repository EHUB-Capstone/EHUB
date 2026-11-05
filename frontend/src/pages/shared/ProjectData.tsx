import { useCallback, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { Database, Download, Loader2 } from 'lucide-react';
import EmptyState from '../../components/ui/EmptyState';
import ErrorState from '../../components/ui/ErrorState';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import Button from '../../components/ui/Button';
import PageHeader from '../../components/ui/PageHeader';
import ProjectDataDetailModal from '../../features/project-data/components/ProjectDataDetailModal';
import type { ProjectDataChanges, ProjectDataSaveError } from '../../features/project-data/components/ProjectDataDetailModal';
import ProjectDataDistribution from '../../features/project-data/components/ProjectDataDistribution';
import ProjectDataTrends from '../../features/project-data/components/ProjectDataTrends';
import ProjectDataSummaryCards from '../../features/project-data/components/ProjectDataSummaryCards';
import ProjectDataFilters from '../../features/project-data/components/ProjectDataFilters';
import ProjectDataTabs, { projectDataPanelId, projectDataTabId } from '../../features/project-data/components/ProjectDataTabs';
import ProjectDataTable from '../../features/project-data/components/ProjectDataTable';
import { useProjectData } from '../../features/project-data/hooks/useProjectData';
import { useAuth } from '../../hooks/useAuth';
import type { ProjectDataItem } from '../../types/projectData';
import { parseApiError } from '../../utils/apiError';
import { downloadBlob, fileDateStamp } from '../../utils/downloadBlob';
import { findItem, hasActiveSearchOrFilters, nextProjectDataSort, toggleAchievement } from '../../utils/projectData';

const CONCURRENCY_CONFLICT_CODE = 'PROJECT_DATA_CONCURRENCY_CONFLICT';
const WRITE_ROLES = ['ADMIN', 'LECTURER'];

export default function ProjectData() {
  const { user } = useAuth();
  const { query, tab, setTab, updateQuery, clearFilters, searchInput, setSearchInput, list, summary, distribution, trends, options, updateAchievements, updateCompetitions, exportProjectData, updateStartupIndustries } = useProjectData();
  const [selected, setSelected] = useState<ProjectDataItem | null>(null);
  const [saveError, setSaveError] = useState<ProjectDataSaveError | null>(null);
  const [industryError, setIndustryError] = useState<ProjectDataSaveError | null>(null);
  const [competitionError, setCompetitionError] = useState<ProjectDataSaveError | null>(null);
  const [isReloading, setIsReloading] = useState(false);

  // The scope's subject list is fixed by the user's classes, so chart colors do not change when a filter narrows it.
  const subjectOrder = useMemo(() => (options.data?.subjects ?? []).map(item => item.code), [options.data]);
  // The backend decides what each role may change; this only avoids offering controls that cannot work.
  const canManage = WRITE_ROLES.includes((user?.role ?? '').toUpperCase());
  const page = list.data;
  const isInitialLoading = list.isPending && !page;
  const isRefreshing = list.isFetching && !isInitialLoading;
  const filtered = hasActiveSearchOrFilters(query);

  const closeDetail = useCallback(() => {
    setSelected(null);
    setSaveError(null);
    setIndustryError(null);
    setCompetitionError(null);
    updateAchievements.reset();
    updateCompetitions.reset();
    updateStartupIndustries.reset();
  }, [updateAchievements, updateCompetitions, updateStartupIndustries]);

  const conflictMessage = 'This project was changed by someone else. Reload the latest data before saving again; your changes are kept until then.';

  // Achievements, industries and competitions are separate requests: each is sent only when it changed, and one failing
  // never undoes the others. Every save moves the project's row version, so the next request uses the version the
  // previous one returned.
  const saveChanges = useCallback(async (changes: ProjectDataChanges) => {
    if (!selected || updateAchievements.isPending || updateStartupIndustries.isPending || updateCompetitions.isPending) return;
    setSaveError(null);
    setIndustryError(null);
    setCompetitionError(null);
    const projectId = selected.projectId;
    let rowVersion = selected.rowVersion;
    const saved: string[] = [];
    // After an earlier part was saved, the toast says so, so the user knows what still needs another try.
    const failureToast = (what: string, isConflict: boolean, message: string) => toast.error(saved.length > 0
      ? `${saved.join(' and ')} saved, but ${what} not: ${isConflict ? 'the project is out of date.' : message}`
      : isConflict ? 'Project data is out of date.' : message);

    if (changes.achievements) {
      try {
        const result = await updateAchievements.mutateAsync({
          projectId,
          payload: { achievements: changes.achievements.labels, note: changes.achievements.note, rowVersion },
        });
        rowVersion = result.rowVersion;
        saved.push('Achievements');
        setSelected(current => current && current.projectId === projectId
          ? {
              ...current,
              achievements: result.achievements,
              achievementNote: result.note,
              achievementsUpdatedAtUtc: result.updatedAtUtc,
              achievementsUpdatedBy: result.updatedBy,
              rowVersion: result.rowVersion,
            }
          : current);
      } catch (error) {
        const parsed = parseApiError(error, 'Unable to update achievements.');
        const isConflict = parsed.code === CONCURRENCY_CONFLICT_CODE;
        setSaveError({ isConflict, message: isConflict ? conflictMessage : parsed.message });
        toast.error(isConflict ? 'Project data is out of date.' : parsed.message);
        return;
      }
    }

    if (changes.startupIndustryIds) {
      try {
        const result = await updateStartupIndustries.mutateAsync({
          projectId,
          payload: { startupIndustryIds: changes.startupIndustryIds, rowVersion },
        });
        rowVersion = result.rowVersion;
        saved.push('Startup industries');
        setSelected(current => current && current.projectId === projectId
          ? { ...current, startupIndustries: result.startupIndustries, rowVersion: result.rowVersion }
          : current);
      } catch (error) {
        const parsed = parseApiError(error, 'Unable to update startup industries.');
        const isConflict = parsed.code === CONCURRENCY_CONFLICT_CODE;
        setIndustryError({ isConflict, message: isConflict ? conflictMessage : parsed.message });
        failureToast('the startup industries were', isConflict, parsed.message);
        return;
      }
    }

    if (changes.competitions) {
      try {
        const result = await updateCompetitions.mutateAsync({
          projectId,
          payload: { entries: changes.competitions, rowVersion },
        });
        saved.push('Competitions');
        setSelected(current => current && current.projectId === projectId
          ? { ...current, competitions: result.competitions, rowVersion: result.rowVersion }
          : current);
      } catch (error) {
        const parsed = parseApiError(error, 'Unable to update competitions.');
        const isConflict = parsed.code === CONCURRENCY_CONFLICT_CODE;
        setCompetitionError({ isConflict, message: isConflict ? conflictMessage : parsed.message });
        failureToast('the competitions were', isConflict, parsed.message);
        return;
      }
    }

    if (saved.length > 0) toast.success(saved.length > 1 ? 'Changes saved.' : `${saved[0]} updated.`);
  }, [selected, updateAchievements, updateCompetitions, updateStartupIndustries]);

  const downloadProjectData = useCallback(async () => {
    if (exportProjectData.isPending) return;
    try {
      const blob = await exportProjectData.mutateAsync(query);
      downloadBlob(blob, `project-data-${fileDateStamp()}.xlsx`);
      toast.success('Project data report downloaded.');
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to export the project data report.').message);
    }
  }, [exportProjectData, query]);

  const reloadSelected = useCallback(async () => {
    if (!selected) return;
    setIsReloading(true);
    try {
      const result = await list.refetch();
      const latest = findItem(result.data?.items ?? [], selected.projectId);
      if (latest) {
        setSelected(latest);
        setSaveError(null);
        setIndustryError(null);
      } else {
        toast('This project is no longer on the current page.');
        closeDetail();
      }
    } finally {
      setIsReloading(false);
    }
  }, [closeDetail, list, selected]);

  const content = useMemo(() => {
    if (isInitialLoading) {
      return (
        <div className="rounded-2xl border border-slate-200/60 bg-white p-6 shadow-sm" role="status" aria-label="Loading project data">
          <LoadingSkeleton variant="table" lines={8} />
        </div>
      );
    }
    if (list.isError && !page) {
      return (
        <ErrorState
          title="Project data could not be loaded"
          message={parseApiError(list.error, 'Failed to load project data. Please try again.').message}
          onRetry={() => { void list.refetch(); }}
        />
      );
    }
    if (!page) return null;
    if (page.totalItems === 0) {
      return (
        <div className="rounded-2xl border border-slate-200/60 bg-white shadow-sm">
          {filtered ? (
            <EmptyState
              icon={Database}
              title="No projects match your search"
              description="Try different keywords or clear the filters to see every project in your scope."
              action={{ label: 'Clear filters', onClick: clearFilters }}
            />
          ) : (
            <EmptyState
              icon={Database}
              title="No project data yet"
              description="Projects appear here once teams in your classes have created a project workspace."
            />
          )}
        </div>
      );
    }
    if (page.items.length === 0) {
      return (
        <div className="rounded-2xl border border-slate-200/60 bg-white shadow-sm">
          <EmptyState
            icon={Database}
            title="This page is out of range"
            description={`There are ${page.totalItems} projects, but not enough for page ${page.pageIndex}.`}
            action={{ label: 'Go to first page', onClick: () => updateQuery({ pageIndex: 1 }) }}
          />
        </div>
      );
    }
    return (
      <ProjectDataTable
        page={page}
        query={query}
        isRefreshing={isRefreshing}
        onSort={field => updateQuery(nextProjectDataSort(query, field))}
        onOpen={setSelected}
        onPageChange={pageIndex => updateQuery({ pageIndex })}
        onPageSizeChange={pageSize => updateQuery({ pageSize })}
      />
    );
  }, [clearFilters, filtered, isInitialLoading, isRefreshing, list, page, query, updateQuery]);

  return (
    <div>
      <PageHeader
        title="Project Data"
        subtitle="Projects across semesters with their groups, industries, staff and achievements."
      >
        <Button
          variant="outline"
          icon={Download}
          isLoading={exportProjectData.isPending}
          onClick={() => { void downloadProjectData(); }}
          title="Download an Excel report (overview, projects, summaries and competitions) of the projects matching the current search and filters"
        >
          Export Project Data
        </Button>
      </PageHeader>

      <ProjectDataSummaryCards
        summary={summary.data}
        isLoading={summary.isPending && !summary.data}
        isError={summary.isError}
        isRefreshing={summary.isFetching && !summary.isPending}
        onRetry={() => { void summary.refetch(); }}
        achievements={query.achievement}
        hasFilters={hasActiveSearchOrFilters(query)}
        onToggleAchievement={achievement => updateQuery({ achievement: toggleAchievement(query.achievement, achievement) })}
        onClearFilters={clearFilters}
      />

      <ProjectDataFilters
        query={query}
        searchInput={searchInput}
        onSearchInputChange={setSearchInput}
        onChange={updateQuery}
        onClear={clearFilters}
        options={options.data}
        optionsLoading={options.isPending}
        optionsError={options.isError}
        onRetryOptions={() => { void options.refetch(); }}
      />

      <ProjectDataTabs value={tab} onChange={setTab} />

      {tab === 'analytics' && (
        <div role="tabpanel" id={projectDataPanelId('analytics')} aria-labelledby={projectDataTabId('analytics')}>
          <ProjectDataDistribution
            distribution={distribution.data}
            query={query}
            isLoading={distribution.isPending && !distribution.data}
            isError={distribution.isError}
            isRefreshing={distribution.isFetching && !distribution.isPending}
            onRetry={() => { void distribution.refetch(); }}
            onFilter={updateQuery}
            onViewTable={() => setTab('overview')}
          />
          <ProjectDataTrends
            trends={trends.data}
            isLoading={trends.isPending && !trends.data}
            isError={trends.isError}
            isRefreshing={trends.isFetching && !trends.isPending}
            onRetry={() => { void trends.refetch(); }}
            subjectOrder={subjectOrder}
          />
        </div>
      )}

      {tab === 'overview' && (
      <div role="tabpanel" id={projectDataPanelId('overview')} aria-labelledby={projectDataTabId('overview')}>
      {isRefreshing && (
        <p className="mb-2 flex items-center gap-2 text-xs font-medium text-primary-700" role="status" aria-live="polite">
          <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden="true" /> Updating results...
        </p>
      )}
      {list.isError && page && (
        <p role="alert" className="mb-2 flex flex-wrap items-center gap-2 text-sm text-danger">
          {parseApiError(list.error, 'Refreshing project data failed.').message}
          <button type="button" onClick={() => { void list.refetch(); }} className="font-semibold underline underline-offset-2">Retry</button>
        </p>
      )}

      {content}
      </div>
      )}

      <ProjectDataDetailModal
        item={selected}
        canManage={canManage}
        isSaving={updateAchievements.isPending || updateStartupIndustries.isPending || updateCompetitions.isPending}
        isReloading={isReloading}
        error={saveError}
        onClose={closeDetail}
        onSave={changes => { void saveChanges(changes); }}
        industryError={industryError}
        competitionError={competitionError}
        onReload={() => { void reloadSelected(); }}
      />
    </div>
  );
}
