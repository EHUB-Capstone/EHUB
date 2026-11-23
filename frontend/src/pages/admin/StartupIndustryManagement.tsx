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
  Upload,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { startupIndustryApi } from '../../api/startupIndustryApi';
import ImportStartupIndustriesModal from '../../components/admin/ImportStartupIndustriesModal';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
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
import { useTranslation } from 'react-i18next';

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

interface StartupIndustryManagementProps {
  /** Rendered as a tab inside another page: the host page owns the title. */
  embedded?: boolean;
}

export default function StartupIndustryManagement({ embedded = false }: StartupIndustryManagementProps = {}): React.ReactElement {
  const { t } = useTranslation('admin');
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
  const [isImportOpen, setIsImportOpen] = useState(false);
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
        const message = parseApiError(error, t('startupIndustryManagement.failedToLoadStartupIndustries')).message;
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
      toast.error(t('startupIndustryManagement.industryNameIsRequired'));
      return;
    }

    const isDuplicate = industries.some(
      (industry) => industry.id !== editingIndustryId
        && industry.name.toLocaleLowerCase() === name.toLocaleLowerCase(),
    );

    if (isDuplicate) {
      toast.error(t('startupIndustryManagement.anIndustryWithThisNameAlready'));
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
        toast.success(t('startupIndustryManagement.industryUpdatedSuccessfully'));
      } else {
        response = await startupIndustryApi.create(payload);
        toast.success(t('startupIndustryManagement.industryAddedSuccessfully'));
      }
      const savedIndustry = extractIndustry(response);
      if (savedIndustry) upsertVisibleIndustry(savedIndustry);
      else setRefreshVersion((version) => version + 1);
      closeForm();
    } catch (error) {
      toast.error(parseApiError(error, t('startupIndustryManagement.failedToSaveStartupIndustry')).message);
    } finally {
      setIsSaving(false);
    }
  };

  const openImportModal = () => {
    setIsImportOpen(true);
  };

  const confirmStatusChange = async () => {
    if (!statusTarget) return;

    const nextStatus: StartupIndustryStatus = statusTarget.status === 'active' ? 'inactive' : 'active';
    setIsChangingStatus(true);
    try {
      const response = await startupIndustryApi.changeStatus(statusTarget.id, nextStatus);
      toast.success(t('startupIndustryManagement.isNow', { name: statusTarget.name, status: t(`startupIndustryManagement.status.${nextStatus}`) }));
      const updatedIndustry = extractIndustry(response);
      if (updatedIndustry) upsertVisibleIndustry(updatedIndustry);
      else setRefreshVersion((version) => version + 1);
      setStatusTarget(null);
    } catch (error) {
      toast.error(parseApiError(error, t('startupIndustryManagement.failedToUpdateIndustryStatus')).message);
    } finally {
      setIsChangingStatus(false);
    }
  };

  return (
    <div className="space-y-6">
      {embedded ? (
        <div className="flex flex-wrap justify-end gap-2">
          <Button variant="outline" icon={Upload} onClick={openImportModal}>{t('startupIndustryManagement.importIndustry')}</Button>
          <Button icon={Plus} onClick={openCreateForm}>{t('startupIndustryManagement.addIndustry')}</Button>
        </div>
      ) : (
        <PageHeader
          title={t('startupIndustryManagement.startupIndustryManagement')}
          subtitle={t('startupIndustryManagement.manageTheStandardizedStartupIndustriesUsed')}
          actions={[
            { label: t('startupIndustryManagement.importIndustry'), icon: Upload, variant: 'outline', onClick: openImportModal },
            { label: t('startupIndustryManagement.addIndustry'), icon: Plus, variant: 'primary', onClick: openCreateForm },
          ]}
        />
      )}

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
                aria-label={t('startupIndustryManagement.searchIndustries')}
                placeholder={t('startupIndustryManagement.searchIndustry')}
                value={searchQuery}
                onChange={(event) => setSearchQuery(event.target.value)}
                className="h-11 w-full rounded-xl border border-slate-200 bg-slate-50/60 pl-10 pr-4 text-sm text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-primary focus:bg-white focus:ring-2 focus:ring-primary/15"
              />
            </div>

            <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
              <div className="inline-flex w-full rounded-xl bg-slate-100 p-1 sm:w-auto" aria-label={t('startupIndustryManagement.filterIndustriesByStatus')}>
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
                <span className="sr-only">{t('startupIndustryManagement.sortBy')}</span>
                <select
                  value={sortOption}
                  onChange={(event) => setSortOption(event.target.value as StartupIndustrySort)}
                  className="h-11 w-full appearance-none rounded-xl border border-slate-200 bg-white pl-3.5 pr-9 text-sm font-medium text-slate-700 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15"
                >
                  <option value="name-asc">{t('startupIndustryManagement.nameAZ')}</option>
                  <option value="name-desc">{t('startupIndustryManagement.nameZA')}</option>
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
              title={t('startupIndustryManagement.unableToLoadIndustries')}
              message={loadError}
              onRetry={() => setRefreshVersion((version) => version + 1)}
            />
          </div>
        ) : industries.length === 0 ? (
          <EmptyState
            icon={Building2}
            title={t('startupIndustryManagement.noIndustriesFound')}
            description={searchQuery || statusFilter !== 'all'
              ? t('startupIndustryManagement.tryADifferentSearchTermOr')
              : t('startupIndustryManagement.createTheFirstStandardizedStartupIndustry')}
            action={searchQuery || statusFilter !== 'all'
              ? {
                  label: t('startupIndustryManagement.clearFilters'),
                  onClick: () => {
                    setSearchQuery('');
                    setDebouncedSearch('');
                    setStatusFilter('all');
                  },
                }
              : { label: t('startupIndustryManagement.addIndustry'), onClick: openCreateForm }}
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[860px]">
              <thead>
                <tr className="bg-slate-50/70 text-left">
                  <th className="w-16 px-5 py-3.5 text-xs font-semibold uppercase tracking-wider text-slate-600">#</th>
                  <th className="px-5 py-3.5 text-xs font-semibold uppercase tracking-wider text-slate-600">{t('startupIndustryManagement.industry')}</th>
                  <th className="px-5 py-3.5 text-xs font-semibold uppercase tracking-wider text-slate-600">{t('startupIndustryManagement.description')}</th>
                  <th className="w-28 px-5 py-3.5 text-center text-xs font-semibold uppercase tracking-wider text-slate-600">{t('startupIndustryManagement.projects')}</th>
                  <th className="w-28 px-5 py-3.5 text-xs font-semibold uppercase tracking-wider text-slate-600">{t('mentorProfileDetail.status')}</th>
                  <th className="w-28 px-5 py-3.5 text-right text-xs font-semibold uppercase tracking-wider text-slate-600">{t('startupIndustryManagement.actions')}</th>
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
                        {t('startupIndustryManagement.nA')}
                      </span>
                    </td>
                    <td className="px-5 py-4">
                      <Badge variant={industry.status === 'active' ? 'Active' : 'Inactive'} dot size="md">
                        {industry.status === 'active' ? t('classManagement.active') : t('classManagement.inactive')}
                      </Badge>
                    </td>
                    <td className="px-5 py-4">
                      <div className="flex items-center justify-end gap-1">
                        <button
                          type="button"
                          aria-label={t('startupIndustryManagement.edit', { name: industry.name })}
                          title={t('startupIndustryManagement.editIndustry')}
                          onClick={() => openEditForm(industry)}
                          className="rounded-lg p-2 text-slate-400 transition hover:bg-primary-50 hover:text-primary-700 focus:outline-none focus:ring-2 focus:ring-primary/20"
                        >
                          <Edit3 className="h-4 w-4" />
                        </button>
                        <button
                          type="button"
                          aria-label={t(industry.status === 'active' ? 'startupIndustryManagement.deactivateNamed' : 'startupIndustryManagement.activateNamed', { name: industry.name })}
                          title={industry.status === 'active' ? t('startupIndustryManagement.deactivateIndustry') : t('startupIndustryManagement.activateIndustry')}
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
            {isRefreshing && <LoaderCircle className="h-3.5 w-3.5 animate-spin text-primary-700" aria-hidden="true" />}
            {t('startupIndustryManagement.showing')} <strong className="font-semibold text-slate-700">{industries.length}</strong> {t('startupIndustryManagement.industries')}
          </span>
          <span className="inline-flex items-center gap-1.5">
            <Layers3 className="h-3.5 w-3.5 text-secondary" />
            {t('startupIndustryManagement.standardizedTaxonomyForEHubProjects')}
          </span>
        </div>}
      </section>

      <Modal
        isOpen={isFormOpen}
        onClose={closeForm}
        onSubmit={saveIndustry}
        title={editingIndustryId ? t('startupIndustryManagement.editIndustry2') : t('startupIndustryManagement.addIndustry')}
        submitText={editingIndustryId ? t('startupIndustryManagement.saveChanges') : t('startupIndustryManagement.addIndustry')}
        isSubmitting={isSaving}
      >
        <div className="space-y-4">
          <label className="block">
            <span className="mb-1.5 block text-sm font-semibold text-slate-700">{t('startupIndustryManagement.industryName')} <span className="text-danger">*</span></span>
            <input
              autoFocus
              type="text"
              maxLength={100}
              aria-describedby="industry-name-character-count"
              value={form.name}
              onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))}
              placeholder={t('startupIndustryManagement.eGLogisticsSupplyChain')}
              className="w-full rounded-xl border border-slate-200 px-3.5 py-2.5 text-sm outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15"
            />
            <span id="industry-name-character-count" className="mt-1.5 block text-right text-xs text-slate-400">
              {form.name.length}/100
            </span>
          </label>

          <label className="block">
            <span className="mb-1.5 block text-sm font-semibold text-slate-700">{t('startupIndustryManagement.description')}</span>
            <textarea
              rows={4}
              maxLength={240}
              aria-describedby="industry-description-character-count"
              value={form.description}
              onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))}
              placeholder={t('startupIndustryManagement.describeTheScopeOfThisStartup')}
              className="w-full resize-y rounded-xl border border-slate-200 px-3.5 py-2.5 text-sm leading-6 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15"
            />
            <span id="industry-description-character-count" className="mt-1.5 block text-right text-xs text-slate-400">
              {form.description.length}/240
            </span>
          </label>

          <label className="block">
            <span className="mb-1.5 block text-sm font-semibold text-slate-700">{t('mentorProfileDetail.status')}</span>
            <select
              value={form.status}
              onChange={(event) => setForm((current) => ({ ...current, status: event.target.value as StartupIndustryStatus }))}
              className="w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15"
            >
              <option value="active">{t('classManagement.active')}</option>
              <option value="inactive">{t('classManagement.inactive')}</option>
            </select>
          </label>
        </div>
      </Modal>

      {isImportOpen && (
        <ImportStartupIndustriesModal
          onClose={() => setIsImportOpen(false)}
          onImported={() => setRefreshVersion((version) => version + 1)}
        />
      )}

      <ConfirmDialog
        isOpen={Boolean(statusTarget)}
        onClose={() => setStatusTarget(null)}
        onConfirm={confirmStatusChange}
        title={statusTarget?.status === 'active' ? t('startupIndustryManagement.deactivateIndustry2') : t('startupIndustryManagement.activateIndustry2')}
        description={statusTarget?.status === 'active'
          ? t('startupIndustryManagement.deactivateItWillNoLongerAppear', { name: statusTarget.name })
          : t('startupIndustryManagement.activateItWillAppearAsAn', { name: statusTarget?.name })}
        confirmText={statusTarget?.status === 'active' ? t('startupIndustryManagement.deactivate') : t('startupIndustryManagement.activate')}
        confirmVariant={statusTarget?.status === 'active' ? 'danger' : 'primary'}
        isSubmitting={isChangingStatus}
      />
    </div>
  );
}
