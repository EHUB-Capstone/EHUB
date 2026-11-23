import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { ImagePlus, Plus, Save, Send, Trash2, Undo2, X } from 'lucide-react';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ConfirmDialog from '../../components/ui/ConfirmDialog';
import EmptyState from '../../components/ui/EmptyState';
import ErrorState from '../../components/ui/ErrorState';
import Input from '../../components/ui/Input';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import PageHeader from '../../components/ui/PageHeader';
import ShowcaseProfileSummary from '../../components/showcase/ShowcaseProfileSummary';
import ShowcaseStatusBadge from '../../components/showcase/ShowcaseStatusBadge';
import ShowcaseTextField from '../../components/showcase/ShowcaseTextField';
import { showcaseApi } from '../../api/showcaseApi';
import { parseApiError } from '../../utils/apiError';
import {
  SHOWCASE_ERROR_CODES,
  SHOWCASE_LIMITS,
  SHOWCASE_LINK_TYPES,
  SHOWCASE_STAGES,
  SHOWCASE_STATUS_META,
  canSubmitForReview,
  canWithdraw,
  emptyLink,
  formFromShowcase,
  emptyForm,
  projectProfilePath,
  hasFormErrors,
  isFormDirty,
  missingForReview,
  toSaveRequest,
  validateImageFile,
  validateShowcaseForm,
  type ShowcaseFormErrors,
  type ShowcaseFormValues,
} from '../../utils/showcase';
import type { MyShowcaseEntryDto, ShowcaseLinkType, ShowcaseStage } from '../../types/showcase';
import { useTranslation } from 'react-i18next';

const MINE_KEY = ['showcase', 'mine'] as const;
const SELECT_CLASS = 'h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-body text-slate-900 hover:border-slate-400 focus:border-primary-600 focus:outline-none focus:ring-2 focus:ring-primary/30 disabled:cursor-not-allowed disabled:bg-slate-100 disabled:opacity-60';

export default function ShowcaseEditor() {
  const { t } = useTranslation('showcase');
  const { lineageId = '' } = useParams();
  const queryClient = useQueryClient();
  const mine = useQuery({ queryKey: MINE_KEY, queryFn: ({ signal }) => showcaseApi.getMine(signal), staleTime: 0 });
  const entry = mine.data?.find((item) => item.projectLineageId === lineageId);
  const showcase = entry?.showcase ?? null;

  // Edits live in `draft`. Until the first keystroke the form mirrors the server data, so a refetch (for example
  // after an image upload) never overwrites what the team typed.
  const [draft, setDraft] = useState<ShowcaseFormValues | null>(null);
  const baseline = entry
    ? (entry.showcase ? formFromShowcase(entry.showcase) : emptyForm())
    : null;
  const form = draft ?? baseline;
  const [errors, setErrors] = useState<ShowcaseFormErrors>({ linkRows: {} });
  const [saving, setSaving] = useState(false);
  const [conflict, setConflict] = useState(false);
  const [uploadTarget, setUploadTarget] = useState<'logo' | 'media' | null>(null);
  const [removeMediaId, setRemoveMediaId] = useState<string | null>(null);
  const [removing, setRemoving] = useState(false);
  const [submitOpen, setSubmitOpen] = useState(false);
  const [acknowledged, setAcknowledged] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [withdrawOpen, setWithdrawOpen] = useState(false);
  const [withdrawing, setWithdrawing] = useState(false);
  const logoInput = useRef<HTMLInputElement>(null);
  const mediaInput = useRef<HTMLInputElement>(null);

  // A page that does not exist yet always counts as unsaved so the team can create the draft as it is.
  const dirty = form !== null && (showcase === null || (draft !== null && baseline !== null && isFormDirty(draft, baseline)));
  const hasUnsavedEdits = showcase !== null && dirty;

  useEffect(() => {
    if (!hasUnsavedEdits) return undefined;
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [hasUnsavedEdits]);

  const patchEntry = (patch: (current: MyShowcaseEntryDto) => MyShowcaseEntryDto) => {
    queryClient.setQueryData<MyShowcaseEntryDto[]>(MINE_KEY, (old) =>
      old?.map((item) => (item.projectLineageId === lineageId ? patch(item) : item)));
  };

  const reportError = (error: unknown, fallback: string) => {
    const parsed = parseApiError(error, fallback);
    if (parsed.code === SHOWCASE_ERROR_CODES.concurrency) setConflict(true);
    toast.error(parsed.message);
  };

  const update = <K extends keyof ShowcaseFormValues>(key: K, value: ShowcaseFormValues[K]) => {
    setDraft((current) => {
      const source = current ?? baseline;
      return source ? { ...source, [key]: value } : current;
    });
  };

  if (mine.isPending) {
    return (
      <main className="mx-auto max-w-5xl space-y-4 p-4 sm:p-8" role="status" aria-label={t('showcaseEditor.loadingShowcaseEditor')}>
        <LoadingSkeleton variant="card" />
        <LoadingSkeleton lines={8} />
      </main>
    );
  }

  if (mine.isError) {
    return (
      <main className="mx-auto max-w-5xl p-4 sm:p-8">
        <ErrorState
          title={t('showcaseEditor.couldNotLoadTheEditor')}
          message={parseApiError(mine.error, 'Failed to load your showcase pages.').message}
          onRetry={() => { void mine.refetch(); }}
        />
      </main>
    );
  }

  if (!entry) {
    return (
      <main className="mx-auto max-w-5xl p-4 sm:p-8">
        <Card hover={false}>
          <EmptyState
            title={t('showcaseEditor.projectNotFound')}
            description={t('showcaseEditor.youCanOnlyEditShowcasePages')}
            action={<Link to="/showcase/manage"><Button variant="outline">{t('showcaseEditor.backToMyShowcase')}</Button></Link>}
          />
        </Card>
      </main>
    );
  }

  if (form === null) return null;

  const locked = showcase !== null && !showcase.canEdit;
  const uploading = uploadTarget !== null;
  const media = showcase?.media ?? [];
  const missing = missingForReview(entry.profile);

  const save = async () => {
    if (saving || locked) return;
    const found = validateShowcaseForm(form);
    setErrors(found);
    if (hasFormErrors(found)) {
      toast.error(t('showcaseEditor.fixTheHighlightedFieldsAndTry'));
      return;
    }
    setSaving(true);
    try {
      const saved = showcase
        ? await showcaseApi.update(showcase.id, toSaveRequest(form, { rowVersion: showcase.rowVersion }))
        : await showcaseApi.create(toSaveRequest(form, { projectLineageId: lineageId }));
      patchEntry((current) => ({ ...current, showcase: saved }));
      setDraft(null);
      setConflict(false);
      toast.success(showcase ? t('showcaseEditor.changesSaved') : t('showcaseEditor.draftCreatedYouCanNowAdd'));
    } catch (error) {
      // Keep everything the team typed so nothing is lost on a failed save.
      reportError(error, 'Unable to save the page. Your changes are still here.');
    } finally {
      setSaving(false);
    }
  };

  const reloadLatest = async () => {
    const latest = await mine.refetch();
    const fresh = latest.data?.find((item) => item.projectLineageId === lineageId);
    if (fresh?.showcase) {
      setDraft(null);
      setErrors({ linkRows: {} });
      setConflict(false);
      toast.success(t('showcaseEditor.loadedTheLatestVersion'));
    }
  };

  const uploadImages = async (files: FileList | null, target: 'logo' | 'media') => {
    if (!files || files.length === 0 || !showcase || uploading) return;
    const chosen = Array.from(files).slice(0, target === 'logo' ? 1 : SHOWCASE_LIMITS.images - media.length);
    if (target === 'media' && media.length + files.length > SHOWCASE_LIMITS.images) {
      toast.error(t('showcaseEditor.aPageCanHaveAtMost', { images: SHOWCASE_LIMITS.images }));
    }
    setUploadTarget(target);
    try {
      for (const file of chosen) {
        const problem = validateImageFile(file);
        if (problem) {
          toast.error(`${file.name}: ${problem}`);
          continue;
        }
        try {
          if (target === 'logo') {
            const updated = await showcaseApi.uploadLogo(showcase.id, file);
            patchEntry((current) => ({ ...current, showcase: updated }));
          } else {
            await showcaseApi.uploadMedia(showcase.id, file);
          }
          toast.success(`${file.name} uploaded.`);
        } catch (error) {
          reportError(error, `Unable to upload ${file.name}.`);
        }
      }
      // An upload changes the page on the server, so reload to pick up the new version number before the next save.
      await mine.refetch();
    } finally {
      const input = target === 'logo' ? logoInput.current : mediaInput.current;
      if (input) input.value = '';
      setUploadTarget(null);
    }
  };

  const removeMedia = async () => {
    if (!showcase || !removeMediaId || removing) return;
    setRemoving(true);
    try {
      await showcaseApi.deleteMedia(showcase.id, removeMediaId);
      await mine.refetch();
      setRemoveMediaId(null);
      toast.success(t('showcaseEditor.imageRemoved'));
    } catch (error) {
      reportError(error, 'Unable to remove the image.');
    } finally {
      setRemoving(false);
    }
  };

  const submitForReview = async () => {
    if (!showcase || submitting) return;
    setSubmitting(true);
    try {
      const updated = await showcaseApi.submit(showcase.id, true);
      patchEntry((current) => ({ ...current, showcase: updated }));
      setSubmitOpen(false);
      setAcknowledged(false);
      toast.success(t('showcaseEditor.submittedForReviewYouWillBe'));
    } catch (error) {
      reportError(error, 'Unable to submit the page.');
    } finally {
      setSubmitting(false);
    }
  };

  const withdraw = async () => {
    if (!showcase || withdrawing) return;
    setWithdrawing(true);
    try {
      const updated = await showcaseApi.withdraw(showcase.id);
      patchEntry((current) => ({ ...current, showcase: updated }));
      void queryClient.invalidateQueries({ queryKey: ['showcase'] });
      setWithdrawOpen(false);
      toast.success(t('showcaseEditor.thePageIsHiddenYouCan'));
    } catch (error) {
      reportError(error, 'Unable to withdraw the page.');
    } finally {
      setWithdrawing(false);
    }
  };

  const setLink = (key: string, patch: Partial<{ linkType: ShowcaseLinkType; label: string; url: string }>) =>
    update('links', form.links.map((link) => (link.key === key ? { ...link, ...patch } : link)));

  return (
    <main className="mx-auto max-w-6xl p-4 sm:p-8">
      <PageHeader
        title={showcase ? t('showcaseEditor.editShowcasePage') : t('showcaseEditor.createShowcasePage')}
        subtitle={`${entry.profile.projectName} · ${entry.teamName}`}
        breadcrumb={[{ label: t('showcaseReports.showcase'), to: '/showcase' }, { label: t('myShowcase.myShowcase'), to: '/showcase/manage' }, 'Editor']}
      />

      {conflict && (
        <div role="alert" className="mb-6 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-warning-light bg-warning-50 px-4 py-3 text-body text-warning-dark">
          <p>{t('showcaseEditor.someoneOnYourTeamChangedThis')}</p>
          <Button size="sm" variant="outline" onClick={() => { void reloadLatest(); }}>{t('showcaseEditor.loadLatestVersion')}</Button>
        </div>
      )}

      {showcase?.reviewNote && ['Draft', 'Hidden'].includes(showcase.status) && (
        <div role="note" className="mb-6 rounded-xl border border-warning-light bg-warning-50 px-4 py-3 text-body text-warning-dark">
          <p className="font-semibold">{t('showcaseDetail.reviewerNote')}</p>
          <p className="mt-1 whitespace-pre-line">{showcase.reviewNote}</p>
        </div>
      )}

      {locked && (
        <div role="status" className="mb-6 rounded-xl border border-info-light bg-info-50 px-4 py-3 text-body text-info-dark">
          {t('showcaseEditor.thisPageIsWaitingForReview')}
        </div>
      )}

      <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_320px]">
        <form
          className="space-y-6"
          noValidate
          onSubmit={(event) => { event.preventDefault(); void save(); }}
        >
          <ShowcaseProfileSummary profile={entry.profile} editPath={projectProfilePath(entry.teamId)} />

          <fieldset disabled={locked || saving} className="min-w-0 space-y-6">
            <legend className="sr-only">{t('showcaseEditor.showcaseContent')}</legend>

            <Card hover={false} className="space-y-5">
              <Card.Header className="mb-0"><Card.Title>{t('showcaseEditor.showcaseDetails')}</Card.Title></Card.Header>
              <div className="grid gap-5 sm:grid-cols-2">
                <div>
                  <label htmlFor="showcase-stage" className="mb-1.5 block text-body-sm font-semibold text-secondary">{t('showcaseEditor.developmentStage')}</label>
                  <select
                    id="showcase-stage"
                    className={SELECT_CLASS}
                    value={form.developmentStage}
                    onChange={(event) => update('developmentStage', event.target.value as ShowcaseStage)}
                  >
                    {SHOWCASE_STAGES.map((stage) => (
                      <option key={stage.value} value={stage.value}>{stage.label} — {stage.hint}</option>
                    ))}
                  </select>
                  <p className="mt-1 text-caption text-slate-600">{t('showcaseEditor.beHonestAboutTheStageUnfinished')}</p>
                </div>
                <label className="flex items-start gap-3 self-end rounded-lg border border-slate-200 bg-slate-50 p-3">
                  <input
                    type="checkbox"
                    className="mt-0.5 h-4 w-4 accent-primary"
                    checked={form.showTeamMembers}
                    onChange={(event) => update('showTeamMembers', event.target.checked)}
                  />
                  <span className="text-body-sm text-slate-700">
                    <span className="block font-semibold text-secondary">{t('showcaseEditor.showTeamMembersPublicly')}</span>
                    {t('showcaseEditor.turnOffToHideYourNames')}
                  </span>
                </label>
              </div>
              <ShowcaseTextField label={t('showcaseDetail.whatMakesItDifferent')} value={form.highlights} max={SHOWCASE_LIMITS.highlights} error={errors.highlights}
                rows={4} hint={t('showcaseEditor.optionalOnlyShownOnTheShowcase')} onChange={(value) => update('highlights', value)} />
            </Card>

            <Card hover={false}>
              <Card.Header>
                <Card.Title>{t('showcaseDetail.links')}</Card.Title>
                <Button
                  size="sm"
                  variant="outline"
                  icon={Plus}
                  disabled={form.links.length >= SHOWCASE_LIMITS.links}
                  onClick={() => update('links', [...form.links, emptyLink()])}
                >
                  {t('showcaseEditor.addLink')}
                </Button>
              </Card.Header>
              {errors.links && <p role="alert" className="mb-3 text-caption text-danger-dark">{errors.links}</p>}
              {form.links.length === 0 ? (
                <p className="text-body text-slate-600">{t('showcaseEditor.addYourWebsiteADemoOr')}</p>
              ) : (
                <ul className="space-y-4">
                  {form.links.map((link, index) => {
                    const rowErrors = errors.linkRows[link.key];
                    return (
                      <li key={link.key} className="grid gap-3 sm:grid-cols-[130px_minmax(0,1fr)_minmax(0,1.4fr)_auto] sm:items-start">
                        <div>
                          <label htmlFor={`link-type-${link.key}`} className="sr-only">{t('showcaseEditor.linkType', { v1: index + 1 })}</label>
                          <select
                            id={`link-type-${link.key}`}
                            className={SELECT_CLASS}
                            value={link.linkType}
                            onChange={(event) => setLink(link.key, { linkType: event.target.value as ShowcaseLinkType })}
                          >
                            {SHOWCASE_LINK_TYPES.map((type) => <option key={type.value} value={type.value}>{type.label}</option>)}
                          </select>
                        </div>
                        <Input aria-label={t('showcaseEditor.linkLabel', { v1: index + 1 })} placeholder={t('showcaseEditor.label')} value={link.label} error={rowErrors?.label}
                          onChange={(event) => setLink(link.key, { label: event.target.value })} />
                        <Input aria-label={t('showcaseEditor.linkAddress', { v1: index + 1 })} placeholder={link.linkType === 'Video' ? 'https://www.youtube.com/watch?v=… or https://vimeo.com/…' : 'https://'} inputMode="url" value={link.url} error={rowErrors?.url}
                          onChange={(event) => setLink(link.key, { url: event.target.value })} />
                        <Button
                          variant="ghost"
                          className="h-10 w-10 px-0"
                          aria-label={t('showcaseEditor.removeLink', { v1: index + 1 })}
                          onClick={() => update('links', form.links.filter((item) => item.key !== link.key))}
                        >
                          <Trash2 className="h-4 w-4" aria-hidden="true" />
                        </Button>
                      </li>
                    );
                  })}
                </ul>
              )}
            </Card>
          </fieldset>

          {!locked && (
            <div className="flex flex-wrap items-center gap-3">
              <Button type="submit" icon={Save} isLoading={saving} disabled={!dirty}>
                {showcase ? t('showcaseEditor.saveChanges') : t('showcaseEditor.createDraft')}
              </Button>
              <p className="text-body-sm text-slate-600" aria-live="polite">
                {dirty ? t('showcaseEditor.youHaveUnsavedChanges') : t('showcaseEditor.allChangesSaved')}
              </p>
            </div>
          )}
        </form>

        <aside className="space-y-6 lg:sticky lg:top-24">
          <Card hover={false} className="space-y-4">
            <Card.Header className="mb-0"><Card.Title>{t('showcaseEditor.publishing')}</Card.Title></Card.Header>
            {showcase ? (
              <>
                <ShowcaseStatusBadge status={showcase.status} />
                <p className="text-body-sm text-slate-600">{SHOWCASE_STATUS_META[showcase.status].description}</p>
                {canSubmitForReview(showcase.status) && (
                  <>
                    {missing.length > 0 && (
                      <p className="text-body-sm text-warning-dark">{t('showcaseEditor.completeInYourProjectProfileBefore', { v1: missing.join(', ') })}</p>
                    )}
                    {dirty && <p className="text-body-sm text-slate-600">{t('showcaseEditor.saveYourChangesBeforeSubmitting')}</p>}
                    <Button
                      variant="secondary"
                      icon={Send}
                      className="w-full"
                      disabled={dirty || missing.length > 0 || uploading}
                      onClick={() => { setAcknowledged(false); setSubmitOpen(true); }}
                    >
                      {t('showcaseEditor.submitForReview')}
                    </Button>
                  </>
                )}
                {canWithdraw(showcase.status) && (
                  <Button variant="danger-outline" icon={Undo2} className="w-full" onClick={() => setWithdrawOpen(true)}>
                    {showcase.status === 'Published' ? t('showcaseEditor.takePageDown') : t('showcaseEditor.withdrawRequest')}
                  </Button>
                )}
                {showcase.status === 'Published' && (
                  <Link to={`/showcase/${showcase.id}`} className="inline-flex text-body-sm font-semibold text-primary-700 hover:underline">
                    {t('myShowcase.viewPublicPage')}
                  </Link>
                )}
              </>
            ) : (
              <p className="text-body-sm text-slate-600">{t('showcaseEditor.createTheDraftFirstItStays')}</p>
            )}
          </Card>

          <Card hover={false} className="space-y-4">
            <Card.Header className="mb-0"><Card.Title icon={ImagePlus}>{t('showcaseEditor.images')}</Card.Title></Card.Header>
            {!showcase ? (
              <p className="text-body-sm text-slate-600">{t('showcaseEditor.saveTheDraftToAddA')}</p>
            ) : (
              <>
                <div>
                  <p className="mb-2 text-body-sm font-semibold text-secondary">{t('showcaseEditor.logo')}</p>
                  <div className="flex items-center gap-3">
                    {showcase.logoUrl ? (
                      <img src={showcase.logoUrl} alt={`${entry.profile.projectName} logo`} className="h-14 w-14 rounded-xl border border-slate-200 object-cover" />
                    ) : (
                      <span className="grid h-14 w-14 place-items-center rounded-xl bg-slate-100 text-caption text-slate-600" aria-hidden="true">{t('showcaseEditor.none')}</span>
                    )}
                    <input ref={logoInput} type="file" accept=".png,.jpg,.jpeg,.webp" className="sr-only" id="showcase-logo-input" aria-label={t('showcaseEditor.chooseALogoImage')} tabIndex={-1} disabled={locked || uploading}
                      onChange={(event) => { void uploadImages(event.target.files, 'logo'); }} />
                    <Button size="sm" variant="outline" disabled={locked || uploading} isLoading={uploadTarget === 'logo'} onClick={() => logoInput.current?.click()}>
                      {showcase.logoUrl ? t('showcaseEditor.replaceLogo') : t('showcaseEditor.uploadLogo')}
                    </Button>
                  </div>
                </div>
                <div>
                  <div className="mb-2 flex items-center justify-between">
                    <p className="text-body-sm font-semibold text-secondary">{t('showcaseDetail.screenshots')}</p>
                    <Badge variant="neutral" size="sm">{media.length}/{SHOWCASE_LIMITS.images}</Badge>
                  </div>
                  {media.length > 0 && (
                    <ul className="mb-3 grid grid-cols-2 gap-2">
                      {media.map((item) => (
                        <li key={item.id} className="relative">
                          <img src={item.url} alt={item.caption ?? t('showcaseEditor.showcaseScreenshot')} className="aspect-video w-full rounded-lg border border-slate-200 object-cover" loading="lazy" />
                          {!locked && (
                            <button
                              type="button"
                              onClick={() => setRemoveMediaId(item.id)}
                              aria-label={t('showcaseEditor.removeScreenshot')}
                              className="absolute right-1 top-1 grid h-9 w-9 place-items-center rounded-lg bg-secondary text-white hover:bg-secondary-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600"
                            >
                              <X className="h-4 w-4" aria-hidden="true" />
                            </button>
                          )}
                        </li>
                      ))}
                    </ul>
                  )}
                  <input ref={mediaInput} type="file" accept=".png,.jpg,.jpeg,.webp" multiple className="sr-only" id="showcase-media-input" aria-label={t('showcaseEditor.chooseScreenshots')} tabIndex={-1}
                    disabled={locked || uploading || media.length >= SHOWCASE_LIMITS.images}
                    onChange={(event) => { void uploadImages(event.target.files, 'media'); }} />
                  <Button size="sm" variant="outline" icon={ImagePlus} disabled={locked || uploading || media.length >= SHOWCASE_LIMITS.images}
                    isLoading={uploadTarget === 'media'} onClick={() => mediaInput.current?.click()}>
                    {t('showcaseEditor.addScreenshots')}
                  </Button>
                  <p className="mt-2 text-caption text-slate-600">{t('showcaseEditor.pngJpgOrWebpUpTo')}</p>
                </div>
              </>
            )}
          </Card>
        </aside>
      </div>

      <ConfirmDialog
        isOpen={submitOpen}
        onClose={() => { if (!submitting) setSubmitOpen(false); }}
        onConfirm={submitForReview}
        title={t('showcaseEditor.submitForReview2')}
        description={t('showcaseEditor.aLecturerOrAdministratorWillReview')}
        confirmText={t('showcaseEditor.submitForReview')}
        confirmVariant="primary"
        isSubmitting={submitting}
        confirmDisabled={!acknowledged}
        details={(
          <label className="flex w-full items-start gap-3 rounded-lg border border-slate-200 bg-slate-50 p-3 text-left">
            <input
              type="checkbox"
              className="mt-0.5 h-4 w-4 accent-primary"
              checked={acknowledged}
              onChange={(event) => setAcknowledged(event.target.checked)}
            />
            <span className="text-body-sm text-slate-700">
              {t('showcaseEditor.iConfirmThisPageDoesNot')}
            </span>
          </label>
        )}
      />

      <ConfirmDialog
        isOpen={withdrawOpen}
        onClose={() => { if (!withdrawing) setWithdrawOpen(false); }}
        onConfirm={withdraw}
        title={showcase?.status === 'Published' ? t('showcaseEditor.takeThisPageDown') : t('showcaseEditor.withdrawThisRequest')}
        description={t('showcaseEditor.thePageWillNoLongerBe')}
        confirmText={showcase?.status === 'Published' ? t('showcaseEditor.takeDown') : t('showcaseEditor.withdraw')}
        isSubmitting={withdrawing}
      />

      <ConfirmDialog
        isOpen={removeMediaId !== null}
        onClose={() => { if (!removing) setRemoveMediaId(null); }}
        onConfirm={removeMedia}
        title={t('showcaseEditor.removeThisScreenshot')}
        description={t('showcaseEditor.itWillDisappearFromYourPage')}
        confirmText={t('common:actions.remove')}
        isSubmitting={removing}
      />
    </main>
  );
}
