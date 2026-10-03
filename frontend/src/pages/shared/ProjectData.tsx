import { useCallback, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { Database, Loader2 } from 'lucide-react';
import EmptyState from '../../components/ui/EmptyState';
import ErrorState from '../../components/ui/ErrorState';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import PageHeader from '../../components/ui/PageHeader';
import ProjectDataDetailModal from '../../features/project-data/components/ProjectDataDetailModal';
import type { ProjectDataSaveError } from '../../features/project-data/components/ProjectDataDetailModal';
import ProjectDataSummaryCards from '../../features/project-data/components/ProjectDataSummaryCards';
import ProjectDataFilters from '../../features/project-data/components/ProjectDataFilters';
import ProjectDataTable from '../../features/project-data/components/ProjectDataTable';
import { useProjectData } from '../../features/project-data/hooks/useProjectData';
import { useAuth } from '../../hooks/useAuth';
import type { ProjectAchievement, ProjectDataItem } from '../../types/projectData';
import { parseApiError } from '../../utils/apiError';
import { findItem, hasActiveSearchOrFilters, nextProjectDataSort } from '../../utils/projectData';

const CONCURRENCY_CONFLICT_CODE = 'PROJECT_DATA_CONCURRENCY_CONFLICT';
const WRITE_ROLES = ['ADMIN', 'LECTURER'];

export default function ProjectData() {
  const { user } = useAuth();
  const { query, updateQuery, clearFilters, searchInput, setSearchInput, list, summary, options, updateAchievements } = useProjectData();
  const [selected, setSelected] = useState<ProjectDataItem | null>(null);
  const [saveError, setSaveError] = useState<ProjectDataSaveError | null>(null);
  const [isReloading, setIsReloading] = useState(false);

  // The backend decides what each role may change; this only avoids offering controls that cannot work.
  const canManage = WRITE_ROLES.includes((user?.role ?? '').toUpperCase());
  const page = list.data;
  const isInitialLoading = list.isPending && !page;
  const isRefreshing = list.isFetching && !isInitialLoading;
  const filtered = hasActiveSearchOrFilters(query);

  const closeDetail = useCallback(() => {
    setSelected(null);
    setSaveError(null);
    updateAchievements.reset();
  }, [updateAchievements]);

  const saveAchievements = useCallback((achievements: ProjectAchievement[]) => {
    if (!selected || updateAchievements.isPending) return;
    setSaveError(null);
    updateAchievements.mutate(
      { projectId: selected.projectId, payload: { achievements, rowVersion: selected.rowVersion } },
      {
        onSuccess: result => {
          setSelected(current => current && current.projectId === result.projectId
            ? { ...current, achievements: result.achievements, rowVersion: result.rowVersion }
            : current);
          toast.success('Achievements updated.');
        },
        onError: error => {
          const parsed = parseApiError(error, 'Unable to update achievements.');
          const isConflict = parsed.code === CONCURRENCY_CONFLICT_CODE;
          setSaveError({
            isConflict,
            message: isConflict
              ? 'This project was changed by someone else. Reload the latest data before saving again; your selection is kept until then.'
              : parsed.message,
          });
          toast.error(isConflict ? 'Project data is out of date.' : parsed.message);
        },
      },
    );
  }, [selected, updateAchievements]);

  const reloadSelected = useCallback(async () => {
    if (!selected) return;
    setIsReloading(true);
    try {
      const result = await list.refetch();
      const latest = findItem(result.data?.items ?? [], selected.projectId);
      if (latest) {
        setSelected(latest);
        setSaveError(null);
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
      />

      <ProjectDataSummaryCards
        summary={summary.data}
        isLoading={summary.isPending && !summary.data}
        isError={summary.isError}
        isRefreshing={summary.isFetching && !summary.isPending}
        onRetry={() => { void summary.refetch(); }}
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

      {isRefreshing && (
        <p className="mb-2 flex items-center gap-2 text-xs font-medium text-primary" role="status" aria-live="polite">
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

      <ProjectDataDetailModal
        item={selected}
        canManage={canManage}
        isSaving={updateAchievements.isPending}
        isReloading={isReloading}
        error={saveError}
        onClose={closeDetail}
        onSave={saveAchievements}
        onReload={() => { void reloadSelected(); }}
      />
    </div>
  );
}
