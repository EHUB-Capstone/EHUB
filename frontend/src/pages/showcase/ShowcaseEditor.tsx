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

const MINE_KEY = ['showcase', 'mine'] as const;
const SELECT_CLASS = 'h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-body text-slate-900 hover:border-slate-400 focus:border-primary-600 focus:outline-none focus:ring-2 focus:ring-primary/30 disabled:cursor-not-allowed disabled:bg-slate-100 disabled:opacity-60';

export default function ShowcaseEditor() {
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
  const [uploading, setUploading] = useState(false);
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
      <main className="mx-auto max-w-5xl space-y-4 p-4 sm:p-8" role="status" aria-label="Loading showcase editor">
        <LoadingSkeleton variant="card" />
        <LoadingSkeleton lines={8} />
      </main>
    );
  }

  if (mine.isError) {
    return (
      <main className="mx-auto max-w-5xl p-4 sm:p-8">
        <ErrorState
          title="Could not load the editor"
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
            title="Project not found"
            description="You can only edit showcase pages of projects your team currently owns."
            action={<Link to="/showcase/manage"><Button variant="outline">Back to My Showcase</Button></Link>}
          />
        </Card>
      </main>
    );
  }

  if (form === null) return null;

  const locked = showcase !== null && !showcase.canEdit;
  const media = showcase?.media ?? [];
  const missing = missingForReview(entry.profile);

  const save = async () => {
    if (saving || locked) return;
    const found = validateShowcaseForm(form);
    setErrors(found);
    if (hasFormErrors(found)) {
      toast.error('Fix the highlighted fields and try again.');
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
      toast.success(showcase ? 'Changes saved.' : 'Draft created. You can now add images.');
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
      toast.success('Loaded the latest version.');
    }
  };

  const uploadImages = async (files: FileList | null, target: 'logo' | 'media') => {
    if (!files || files.length === 0 || !showcase || uploading) return;
    const chosen = Array.from(files).slice(0, target === 'logo' ? 1 : SHOWCASE_LIMITS.images - media.length);
    if (target === 'media' && media.length + files.length > SHOWCASE_LIMITS.images) {
      toast.error(`A page can have at most ${SHOWCASE_LIMITS.images} images.`);
    }
    setUploading(true);
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
    } finally {
      // An upload changes the page on the server, so reload to pick up the new version number before the next save.
      await mine.refetch();
      setUploading(false);
      if (logoInput.current) logoInput.current.value = '';
      if (mediaInput.current) mediaInput.current.value = '';
    }
  };

  const removeMedia = async () => {
    if (!showcase || !removeMediaId || removing) return;
    setRemoving(true);
    try {
      await showcaseApi.deleteMedia(showcase.id, removeMediaId);
      await mine.refetch();
      setRemoveMediaId(null);
      toast.success('Image removed.');
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
      toast.success('Submitted for review. You will be notified of the decision.');
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
      toast.success('The page is hidden. You can edit it and submit it again.');
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
        title={showcase ? 'Edit showcase page' : 'Create showcase page'}
        subtitle={`${entry.profile.projectName} · ${entry.teamName}`}
        breadcrumb={['Showcase', 'My Showcase', 'Editor']}
      />

      {conflict && (
        <div role="alert" className="mb-6 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-warning-light bg-warning-50 px-4 py-3 text-body text-warning-dark">
          <p>Someone on your team changed this page. Load the latest version to continue; your unsaved edits will be replaced.</p>
          <Button size="sm" variant="outline" onClick={() => { void reloadLatest(); }}>Load latest version</Button>
        </div>
      )}

      {showcase?.reviewNote && ['ChangesRequested', 'Draft', 'Hidden'].includes(showcase.status) && (
        <div role="note" className="mb-6 rounded-xl border border-warning-light bg-warning-50 px-4 py-3 text-body text-warning-dark">
          <p className="font-semibold">Reviewer note</p>
          <p className="mt-1 whitespace-pre-line">{showcase.reviewNote}</p>
        </div>
      )}

      {locked && (
        <div role="status" className="mb-6 rounded-xl border border-info-light bg-info-50 px-4 py-3 text-body text-info-dark">
          This page is waiting for review and cannot be edited. Withdraw it to make changes.
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
            <legend className="sr-only">Showcase content</legend>

            <Card hover={false} className="space-y-5">
              <Card.Header className="mb-0"><Card.Title>Showcase details</Card.Title></Card.Header>
              <div className="grid gap-5 sm:grid-cols-2">
                <div>
                  <label htmlFor="showcase-stage" className="mb-1.5 block text-body-sm font-semibold text-secondary">Development stage</label>
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
                  <p className="mt-1 text-caption text-slate-600">Be honest about the stage; unfinished projects are welcome.</p>
                </div>
                <label className="flex items-start gap-3 self-end rounded-lg border border-slate-200 bg-slate-50 p-3">
                  <input
                    type="checkbox"
                    className="mt-0.5 h-4 w-4 accent-primary"
                    checked={form.showTeamMembers}
                    onChange={(event) => update('showTeamMembers', event.target.checked)}
                  />
                  <span className="text-body-sm text-slate-700">
                    <span className="block font-semibold text-secondary">Show team members publicly</span>
                    Turn off to hide your names from the community. Reviewers always see them.
                  </span>
                </label>
              </div>
              <ShowcaseTextField label="What makes it different" value={form.highlights} max={SHOWCASE_LIMITS.highlights} error={errors.highlights}
                rows={4} hint="Optional. Only shown on the showcase page." onChange={(value) => update('highlights', value)} />
            </Card>

            <Card hover={false}>
              <Card.Header>
                <Card.Title>Links</Card.Title>
                <Button
                  size="sm"
                  variant="outline"
                  icon={Plus}
                  disabled={form.links.length >= SHOWCASE_LIMITS.links}
                  onClick={() => update('links', [...form.links, emptyLink()])}
                >
                  Add link
                </Button>
              </Card.Header>
              {errors.links && <p role="alert" className="mb-3 text-caption text-danger-dark">{errors.links}</p>}
              {form.links.length === 0 ? (
                <p className="text-body text-slate-600">Add your website, a demo or a video so visitors can try the project.</p>
              ) : (
                <ul className="space-y-4">
                  {form.links.map((link, index) => {
                    const rowErrors = errors.linkRows[link.key];
                    return (
                      <li key={link.key} className="grid gap-3 sm:grid-cols-[130px_minmax(0,1fr)_minmax(0,1.4fr)_auto] sm:items-start">
                        <div>
                          <label htmlFor={`link-type-${link.key}`} className="sr-only">Link {index + 1} type</label>
                          <select
                            id={`link-type-${link.key}`}
                            className={SELECT_CLASS}
                            value={link.linkType}
                            onChange={(event) => setLink(link.key, { linkType: event.target.value as ShowcaseLinkType })}
                          >
                            {SHOWCASE_LINK_TYPES.map((type) => <option key={type.value} value={type.value}>{type.label}</option>)}
                          </select>
                        </div>
                        <Input aria-label={`Link ${index + 1} label`} placeholder="Label" value={link.label} error={rowErrors?.label}
                          onChange={(event) => setLink(link.key, { label: event.target.value })} />
                        <Input aria-label={`Link ${index + 1} address`} placeholder={link.linkType === 'Video' ? 'https://www.youtube.com/watch?v=… or https://vimeo.com/…' : 'https://'} inputMode="url" value={link.url} error={rowErrors?.url}
                          onChange={(event) => setLink(link.key, { url: event.target.value })} />
                        <Button
                          variant="ghost"
                          className="h-10 w-10 px-0"
                          aria-label={`Remove link ${index + 1}`}
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
                {showcase ? 'Save changes' : 'Create draft'}
              </Button>
              <p className="text-body-sm text-slate-600" aria-live="polite">
                {dirty ? 'You have unsaved changes.' : 'All changes saved.'}
              </p>
            </div>
          )}
        </form>

        <aside className="space-y-6 lg:sticky lg:top-24">
          <Card hover={false} className="space-y-4">
            <Card.Header className="mb-0"><Card.Title>Publishing</Card.Title></Card.Header>
            {showcase ? (
              <>
                <ShowcaseStatusBadge status={showcase.status} />
                <p className="text-body-sm text-slate-600">{SHOWCASE_STATUS_META[showcase.status].description}</p>
                {canSubmitForReview(showcase.status) && (
                  <>
                    {missing.length > 0 && (
                      <p className="text-body-sm text-warning-dark">Complete {missing.join(', ')} in your Project Profile before submitting.</p>
                    )}
                    {dirty && <p className="text-body-sm text-slate-600">Save your changes before submitting.</p>}
                    <Button
                      variant="secondary"
                      icon={Send}
                      className="w-full"
                      disabled={dirty || missing.length > 0 || uploading}
                      onClick={() => { setAcknowledged(false); setSubmitOpen(true); }}
                    >
                      Submit for review
                    </Button>
                  </>
                )}
                {canWithdraw(showcase.status) && (
                  <Button variant="danger-outline" icon={Undo2} className="w-full" onClick={() => setWithdrawOpen(true)}>
                    {showcase.status === 'Published' ? 'Take page down' : 'Withdraw request'}
                  </Button>
                )}
                {showcase.status === 'Published' && (
                  <Link to={`/showcase/${showcase.id}`} className="inline-flex text-body-sm font-semibold text-primary-700 hover:underline">
                    View public page
                  </Link>
                )}
              </>
            ) : (
              <p className="text-body-sm text-slate-600">Create the draft first. It stays private until you submit it and a reviewer approves it.</p>
            )}
          </Card>

          <Card hover={false} className="space-y-4">
            <Card.Header className="mb-0"><Card.Title icon={ImagePlus}>Images</Card.Title></Card.Header>
            {!showcase ? (
              <p className="text-body-sm text-slate-600">Save the draft to add a logo and screenshots.</p>
            ) : (
              <>
                <div>
                  <p className="mb-2 text-body-sm font-semibold text-secondary">Logo</p>
                  <div className="flex items-center gap-3">
                    {showcase.logoUrl ? (
                      <img src={showcase.logoUrl} alt={`${entry.profile.projectName} logo`} className="h-14 w-14 rounded-xl border border-slate-200 object-cover" />
                    ) : (
                      <span className="grid h-14 w-14 place-items-center rounded-xl bg-slate-100 text-caption text-slate-600" aria-hidden="true">None</span>
                    )}
                    <input ref={logoInput} type="file" accept=".png,.jpg,.jpeg,.webp" className="sr-only" id="showcase-logo-input" aria-label="Choose a logo image" tabIndex={-1} disabled={locked || uploading}
                      onChange={(event) => { void uploadImages(event.target.files, 'logo'); }} />
                    <Button size="sm" variant="outline" disabled={locked || uploading} isLoading={uploading} onClick={() => logoInput.current?.click()}>
                      {showcase.logoUrl ? 'Replace logo' : 'Upload logo'}
                    </Button>
                  </div>
                </div>
                <div>
                  <div className="mb-2 flex items-center justify-between">
                    <p className="text-body-sm font-semibold text-secondary">Screenshots</p>
                    <Badge variant="neutral" size="sm">{media.length}/{SHOWCASE_LIMITS.images}</Badge>
                  </div>
                  {media.length > 0 && (
                    <ul className="mb-3 grid grid-cols-2 gap-2">
                      {media.map((item) => (
                        <li key={item.id} className="relative">
                          <img src={item.url} alt={item.caption ?? 'Showcase screenshot'} className="aspect-video w-full rounded-lg border border-slate-200 object-cover" loading="lazy" />
                          {!locked && (
                            <button
                              type="button"
                              onClick={() => setRemoveMediaId(item.id)}
                              aria-label="Remove screenshot"
                              className="absolute right-1 top-1 grid h-9 w-9 place-items-center rounded-lg bg-secondary text-white hover:bg-secondary-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600"
                            >
                              <X className="h-4 w-4" aria-hidden="true" />
                            </button>
                          )}
                        </li>
                      ))}
                    </ul>
                  )}
                  <input ref={mediaInput} type="file" accept=".png,.jpg,.jpeg,.webp" multiple className="sr-only" id="showcase-media-input" aria-label="Choose screenshots" tabIndex={-1}
                    disabled={locked || uploading || media.length >= SHOWCASE_LIMITS.images}
                    onChange={(event) => { void uploadImages(event.target.files, 'media'); }} />
                  <Button size="sm" variant="outline" icon={ImagePlus} disabled={locked || uploading || media.length >= SHOWCASE_LIMITS.images}
                    isLoading={uploading} onClick={() => mediaInput.current?.click()}>
                    Add screenshots
                  </Button>
                  <p className="mt-2 text-caption text-slate-600">PNG, JPG or WEBP, up to 5 MB each.</p>
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
        title="Submit for review?"
        description="A lecturer or administrator will review this page before anyone outside your team can see it."
        confirmText="Submit for review"
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
              I confirm this page does not reveal unpublished research, confidential partner information, personal data, or work my team has no right to share.
            </span>
          </label>
        )}
      />

      <ConfirmDialog
        isOpen={withdrawOpen}
        onClose={() => { if (!withdrawing) setWithdrawOpen(false); }}
        onConfirm={withdraw}
        title={showcase?.status === 'Published' ? 'Take this page down?' : 'Withdraw this request?'}
        description="The page will no longer be visible. You can edit it and submit it for review again."
        confirmText={showcase?.status === 'Published' ? 'Take down' : 'Withdraw'}
        isSubmitting={withdrawing}
      />

      <ConfirmDialog
        isOpen={removeMediaId !== null}
        onClose={() => { if (!removing) setRemoveMediaId(null); }}
        onConfirm={removeMedia}
        title="Remove this screenshot?"
        description="It will disappear from your page."
        confirmText="Remove"
        isSubmitting={removing}
      />
    </main>
  );
}
