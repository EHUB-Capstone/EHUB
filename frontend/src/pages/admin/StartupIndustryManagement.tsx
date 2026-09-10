import { startTransition, useEffect, useRef, useState } from 'react';
import {
  Building2,
  ChevronDown,
  Edit3,
  Layers3,
  LoaderCircle,
  Plus,
  Power,
  Search,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { startupIndustryApi } from '../../api/startupIndustryApi';
import Badge from '../../components/ui/Badge';
import ConfirmDialog from '../../components/ui/ConfirmDialog';
import EmptyState from '../../components/ui/EmptyState';
import ErrorState from '../../components/ui/ErrorState';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import Modal from '../../components/ui/Modal';
import PageHeader from '../../components/ui/PageHeader';
import type {
  StartupIndustryDto,
  StartupIndustrySort,
  StartupIndustryStatus,
} from '../../types/startupIndustries';
import { parseApiError } from '../../utils/apiError';

type StatusFilter = 'all' | StartupIndustryStatus;

interface IndustryFormState {
  name: string;
  description: string;
  status: StartupIndustryStatus;
}

const emptyForm: IndustryFormState = {
  name: '',
  description: '',
  status: 'active',
};

function extractIndustry(response: unknown): StartupIndustryDto | null {
  const envelope = response as { data?: unknown } | null;
  const candidate = (envelope?.data ?? response) as Partial<StartupIndustryDto> | null;
  if (!candidate || typeof candidate.id !== 'string' || typeof candidate.name !== 'string') return null;
  if (candidate.status !== 'active' && candidate.status !== 'inactive') return null;

  return {
    id: candidate.id,
    name: candidate.name,
    description: typeof candidate.description === 'string' ? candidate.description : null,
    status: candidate.status,
  };
}

function matchesCurrentView(
  industry: StartupIndustryDto,
  search: string,
  status: StatusFilter,
): boolean {
  const matchesSearch = !search || industry.name.toLocaleLowerCase().includes(search.toLocaleLowerCase());
  return matchesSearch && (status === 'all' || industry.status === status);
}

function sortIndustries(
  industries: StartupIndustryDto[],
  sort: StartupIndustrySort,
): StartupIndustryDto[] {
  return [...industries].sort((left, right) => {
    const comparison = left.name.localeCompare(right.name);
    return sort === 'name-desc' ? -comparison : comparison;
  });
}

export default function StartupIndustryManagement(): React.ReactElement {
  const [industries, setIndustries] = useState<StartupIndustryDto[]>([]);
  const [isInitialLoading, setIsInitialLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [refreshVersion, setRefreshVersion] = useState(0);
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [sortOption, setSortOption] = useState<StartupIndustrySort>('name-asc');
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [editingIndustryId, setEditingIndustryId] = useState<string | null>(null);
  const [form, setForm] = useState<IndustryFormState>(emptyForm);
  const [statusTarget, setStatusTarget] = useState<StartupIndustryDto | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [isChangingStatus, setIsChangingStatus] = useState(false);
  const hasLoadedRef = useRef(false);

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setDebouncedSearch(searchQuery.trim());
    }, 250);

    return () => window.clearTimeout(timeout);
  }, [searchQuery]);

  useEffect(() => {
    const controller = new AbortController();
    const loadIndustries = async () => {
      const isFirstLoad = !hasLoadedRef.current;
      if (isFirstLoad) setIsInitialLoading(true);
      else setIsRefreshing(true);
      setLoadError(null);

      try {
        const params = {
          ...(debouncedSearch ? { search: debouncedSearch } : {}),
          ...(statusFilter !== 'all' ? { status: statusFilter } : {}),
          sort: sortOption,
        };
        const response = await startupIndustryApi.getAll(params, controller.signal);
        const payload = response?.data ?? response;
        startTransition(() => {
          setIndustries(Array.isArray(payload?.industries) ? payload.industries : []);
        });
        hasLoadedRef.current = true;
      } catch (error) {
        if (controller.signal.aborted) return;
        const message = parseApiError(error, 'Failed to load startup industries.').message;
        if (isFirstLoad) {
          setIndustries([]);
          setLoadError(message);
        } else {
          toast.error(message);
        }
      } finally {
        if (!controller.signal.aborted) {
          setIsInitialLoading(false);
          setIsRefreshing(false);
        }
      }
    };

    void loadIndustries();

    return () => controller.abort();
  }, [debouncedSearch, refreshVersion, sortOption, statusFilter]);

  const upsertVisibleIndustry = (industry: StartupIndustryDto) => {
    startTransition(() => {
      setIndustries((current) => {
        const next = current.filter((item) => item.id !== industry.id);
        if (matchesCurrentView(industry, debouncedSearch, statusFilter)) next.push(industry);
        return sortIndustries(next, sortOption);
      });
    });
  };

  const openCreateForm = () => {
    setEditingIndustryId(null);
    setForm(emptyForm);
    setIsFormOpen(true);
  };

  const openEditForm = (industry: StartupIndustryDto) => {
    setEditingIndustryId(industry.id);
    setForm({
      name: industry.name,
      description: industry.description ?? '',
      status: industry.status,
    });
    setIsFormOpen(true);
  };

  const closeForm = () => {
    setIsFormOpen(false);
    setEditingIndustryId(null);
    setForm(emptyForm);
  };

  const saveIndustry = async () => {
    const name = form.name.trim();

    if (!name) {
      toast.error('Industry name is required.');
      return;
    }

    const isDuplicate = industries.some(
      (industry) => industry.id !== editingIndustryId
        && industry.name.toLocaleLowerCase() === name.toLocaleLowerCase(),
    );

    if (isDuplicate) {
      toast.error('An industry with this name already exists.');
      return;
    }

    setIsSaving(true);
    try {
      const payload = {
        name,
        description: form.description.trim() || null,
        status: form.status,
      };
      let response: unknown;
      if (editingIndustryId) {
        response = await startupIndustryApi.update(editingIndustryId, payload);
        toast.success('Industry updated successfully.');
      } else {
        response = await startupIndustryApi.create(payload);
        toast.success('Industry added successfully.');
      }
      const savedIndustry = extractIndustry(response);
      if (savedIndustry) upsertVisibleIndustry(savedIndustry);
      else setRefreshVersion((version) => version + 1);
      closeForm();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to save startup industry.').message);
    } finally {
      setIsSaving(false);
    }
  };

  const confirmStatusChange = async () => {
    if (!statusTarget) return;

    const nextStatus: StartupIndustryStatus = statusTarget.status === 'active' ? 'inactive' : 'active';
    setIsChangingStatus(true);
    try {
      const response = await startupIndustryApi.changeStatus(statusTarget.id, nextStatus);
      toast.success(`${statusTarget.name} is now ${nextStatus}.`);
      const updatedIndustry = extractIndustry(response);
      if (updatedIndustry) upsertVisibleIndustry(updatedIndustry);
      else setRefreshVersion((version) => version + 1);
      setStatusTarget(null);
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to update industry status.').message);
    } finally {
      setIsChangingStatus(false);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Startup Industry Management"
        subtitle="Manage the standardized startup industries used across E-HUB projects."
        action={{ label: 'Add Industry', icon: Plus, variant: 'primary', onClick: openCreateForm }}
      />

      <section
        className="overflow-hidden rounded-2xl border border-slate-200/70 bg-white shadow-card"
        aria-busy={isInitialLoading || isRefreshing}
      >
        <div className="border-b border-slate-100 p-4 sm:p-5">
          <div className="flex flex-col gap-3 xl:flex-row xl:items-center xl:justify-between">
            <div className="relative min-w-0 flex-1 xl:max-w-xl">
              <Search className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
              <input
                type="search"
                aria-label="Search industries"
                placeholder="Search industry..."
                value={searchQuery}
                onChange={(event) => setSearchQuery(event.target.value)}
                className="h-11 w-full rounded-xl border border-slate-200 bg-slate-50/60 pl-10 pr-4 text-sm text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-primary focus:bg-white focus:ring-2 focus:ring-primary/15"
              />
            </div>

            <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
              <div className="inline-flex w-full rounded-xl bg-slate-100 p-1 sm:w-auto" aria-label="Filter industries by status">
                {(['all', 'active', 'inactive'] as const).map((status) => (
                  <button
                    key={status}
                    type="button"
                    aria-pressed={statusFilter === status}
                    onClick={() => setStatusFilter(status)}
                    className={`flex-1 rounded-lg px-4 py-2 text-sm font-semibold capitalize transition sm:flex-none ${
                      statusFilter === status
                        ? 'bg-white text-slate-900 shadow-sm'
                        : 'text-slate-500 hover:text-slate-800'
                    }`}
                  >
                    {status}
                  </button>
                ))}
              </div>

              <label className="relative block w-full sm:w-52">
                <span className="sr-only">Sort by</span>
                <select
                  value={sortOption}
                  onChange={(event) => setSortOption(event.target.value as StartupIndustrySort)}
                  className="h-11 w-full appearance-none rounded-xl border border-slate-200 bg-white pl-3.5 pr-9 text-sm font-medium text-slate-700 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15"
                >
                  <option value="name-asc">Name A-Z</option>
                  <option value="name-desc">Name Z-A</option>
                </select>
                <ChevronDown className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
              </label>
            </div>
          </div>
        </div>

        {isInitialLoading ? (
          <div className="p-5"><LoadingSkeleton variant="table" lines={6} /></div>
        ) : loadError ? (
          <div className="p-5">
            <ErrorState
              title="Unable to load industries"
              message={loadError}
              onRetry={() => setRefreshVersion((version) => version + 1)}
            />
          </div>
        ) : industries.length === 0 ? (
          <EmptyState
            icon={Building2}
            title="No industries found"
            description={searchQuery || statusFilter !== 'all'
              ? 'Try a different search term or status filter.'
              : 'Create the first standardized startup industry for E-HUB projects.'}
            action={searchQuery || statusFilter !== 'all'
              ? {
                  label: 'Clear filters',
                  onClick: () => {
                    setSearchQuery('');
                    setDebouncedSearch('');
                    setStatusFilter('all');
                  },
                }
              : { label: 'Add Industry', onClick: openCreateForm }}
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[860px]">
              <thead>
                <tr className="bg-slate-50/70 text-left">
                  <th className="w-16 px-5 py-3.5 text-xs font-semibold uppercase tracking-wider text-slate-400">#</th>
                  <th className="px-5 py-3.5 text-xs font-semibold uppercase tracking-wider text-slate-400">Industry</th>
                  <th className="px-5 py-3.5 text-xs font-semibold uppercase tracking-wider text-slate-400">Description</th>
                  <th className="w-28 px-5 py-3.5 text-center text-xs font-semibold uppercase tracking-wider text-slate-400">Projects</th>
                  <th className="w-28 px-5 py-3.5 text-xs font-semibold uppercase tracking-wider text-slate-400">Status</th>
                  <th className="w-28 px-5 py-3.5 text-right text-xs font-semibold uppercase tracking-wider text-slate-400">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {industries.map((industry, index) => (
                  <tr key={industry.id} className="group transition-colors hover:bg-primary-50/30">
                    <td className="px-5 py-4 text-sm font-medium text-slate-400">{String(index + 1).padStart(2, '0')}</td>
                    <td className="px-5 py-4">
                      <p className="font-semibold text-slate-900">{industry.name}</p>
                    </td>
                    <td className="max-w-sm px-5 py-4">
                      <p className="whitespace-pre-wrap break-words text-sm leading-5 text-slate-600">
                        {industry.description || <span className="text-slate-400">—</span>}
                      </p>
                    </td>
                    <td className="px-5 py-4 text-center">
                      <span className="inline-flex min-w-10 items-center justify-center rounded-lg bg-slate-100 px-2.5 py-1.5 text-sm font-bold tabular-nums text-slate-700">
                        N/A
                      </span>
                    </td>
                    <td className="px-5 py-4">
                      <Badge variant={industry.status === 'active' ? 'Active' : 'Inactive'} dot size="md">
                        {industry.status === 'active' ? 'Active' : 'Inactive'}
                      </Badge>
                    </td>
                    <td className="px-5 py-4">
                      <div className="flex items-center justify-end gap-1">
                        <button
                          type="button"
                          aria-label={`Edit ${industry.name}`}
                          title="Edit industry"
                          onClick={() => openEditForm(industry)}
                          className="rounded-lg p-2 text-slate-400 transition hover:bg-primary-50 hover:text-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                        >
                          <Edit3 className="h-4 w-4" />
                        </button>
                        <button
                          type="button"
                          aria-label={`${industry.status === 'active' ? 'Deactivate' : 'Activate'} ${industry.name}`}
                          title={industry.status === 'active' ? 'Deactivate industry' : 'Activate industry'}
                          onClick={() => setStatusTarget(industry)}
                          className={`rounded-lg p-2 transition focus:outline-none focus:ring-2 focus:ring-primary/20 ${
                            industry.status === 'active'
                              ? 'text-slate-400 hover:bg-danger-50 hover:text-danger'
                              : 'text-slate-400 hover:bg-success-50 hover:text-success-dark'
                          }`}
                        >
                          <Power className="h-4 w-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {!isInitialLoading && !loadError && <div className="flex flex-col gap-2 border-t border-slate-100 bg-slate-50/40 px-5 py-3.5 text-xs text-slate-500 sm:flex-row sm:items-center sm:justify-between">
          <span className="inline-flex items-center gap-2" aria-live="polite">
            {isRefreshing && <LoaderCircle className="h-3.5 w-3.5 animate-spin text-primary" aria-hidden="true" />}
            Showing <strong className="font-semibold text-slate-700">{industries.length}</strong> industries
          </span>
          <span className="inline-flex items-center gap-1.5">
            <Layers3 className="h-3.5 w-3.5 text-secondary" />
            Standardized taxonomy for E-HUB projects
          </span>
        </div>}
      </section>

      <Modal
        isOpen={isFormOpen}
        onClose={closeForm}
        onSubmit={saveIndustry}
        title={editingIndustryId ? 'Edit Industry' : 'Add Industry'}
        submitText={editingIndustryId ? 'Save Changes' : 'Add Industry'}
        isSubmitting={isSaving}
      >
        <div className="space-y-4">
          <label className="block">
            <span className="mb-1.5 block text-sm font-semibold text-slate-700">Industry name <span className="text-danger">*</span></span>
            <input
              autoFocus
              type="text"
              maxLength={100}
              aria-describedby="industry-name-character-count"
              value={form.name}
              onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))}
              placeholder="e.g. Logistics & Supply Chain"
              className="w-full rounded-xl border border-slate-200 px-3.5 py-2.5 text-sm outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15"
            />
            <span id="industry-name-character-count" className="mt-1.5 block text-right text-xs text-slate-400">
              {form.name.length}/100
            </span>
          </label>

          <label className="block">
            <span className="mb-1.5 block text-sm font-semibold text-slate-700">Description</span>
            <textarea
              rows={4}
              maxLength={240}
              aria-describedby="industry-description-character-count"
              value={form.description}
              onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))}
              placeholder="Describe the scope of this startup industry..."
              className="w-full resize-y rounded-xl border border-slate-200 px-3.5 py-2.5 text-sm leading-6 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15"
            />
            <span id="industry-description-character-count" className="mt-1.5 block text-right text-xs text-slate-400">
              {form.description.length}/240
            </span>
          </label>

          <label className="block">
            <span className="mb-1.5 block text-sm font-semibold text-slate-700">Status</span>
            <select
              value={form.status}
              onChange={(event) => setForm((current) => ({ ...current, status: event.target.value as StartupIndustryStatus }))}
              className="w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15"
            >
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
            </select>
          </label>
        </div>
      </Modal>

      <ConfirmDialog
        isOpen={Boolean(statusTarget)}
        onClose={() => setStatusTarget(null)}
        onConfirm={confirmStatusChange}
        title={statusTarget?.status === 'active' ? 'Deactivate Industry' : 'Activate Industry'}
        description={statusTarget?.status === 'active'
          ? `Deactivate ${statusTarget.name}? It will no longer appear as an active startup industry.`
          : `Activate ${statusTarget?.name}? It will appear as an active startup industry.`}
        confirmText={statusTarget?.status === 'active' ? 'Deactivate' : 'Activate'}
        confirmVariant={statusTarget?.status === 'active' ? 'danger' : 'primary'}
        isSubmitting={isChangingStatus}
      />
    </div>
  );
}
