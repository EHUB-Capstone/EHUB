import { useState } from 'react';
import { Flag } from 'lucide-react';
import toast from 'react-hot-toast';
import Button from '../ui/Button';
import Modal from '../ui/Modal';
import { showcaseApi } from '../../api/showcaseApi';
import { parseApiError } from '../../utils/apiError';
import type { ShowcaseReportReason, ShowcaseViolationCategory } from '../../types/showcase';
import { useShowcaseViolationCategories } from '../../hooks/useShowcaseViolationCategories';
import { showcaseCategoryLabel, showcaseReportReasonFromCategory } from '../../utils/showcaseModeration';
import { useTranslation } from 'react-i18next';
import i18n from '../../i18n';

const REASONS: Array<{ value: ShowcaseReportReason; label: string }> = [
  { value: 'Spam', get label() { return i18n.t('showcase:showcaseReportButton.spamOrScam'); } },
  { value: 'Harassment', get label() { return i18n.t('showcase:showcaseReportButton.harassmentOrHate'); } },
  { value: 'InappropriateContent', get label() { return i18n.t('showcase:showcaseReportButton.inappropriateContent'); } },
  { value: 'CopyrightViolation', get label() { return i18n.t('showcase:showcaseReportButton.copyrightViolation'); } },
  { value: 'Other', get label() { return i18n.t('showcase:showcaseReportButton.other'); } },
];

export default function ShowcaseReportButton({ showcaseId, commentId }: { showcaseId: string; commentId?: string }) {
  const { t, i18n: language } = useTranslation('showcase');
  const isComment = !!commentId;
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState<ShowcaseReportReason>('InappropriateContent');
  const [category, setCategory] = useState<ShowcaseViolationCategory | ''>('');
  const categories = useShowcaseViolationCategories(open);
  const categoryOptions = categories.data?.filter(item => isComment || showcaseReportReasonFromCategory(item.code) === reason) ?? [];
  const selectedCategory = categoryOptions.find(item => item.code === category)?.code ?? '';
  const canSubmit = !isComment || (categories.isSuccess && !!selectedCategory);
  const [note, setNote] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  const submit = async () => {
    if (submitting || !canSubmit) return;
    setSubmitting(true);
    try {
      await showcaseApi.createReport(showcaseId, {
        commentId: commentId ?? null,
        reason: isComment && selectedCategory ? showcaseReportReasonFromCategory(selectedCategory) : reason,
        category: selectedCategory || null,
        note: note.trim() || null,
      });
      toast.success(t('showcaseReportButton.reportSubmittedThankYouForHelping'));
      setSubmitted(true);
      setOpen(false);
      setNote('');
    } catch (error) {
      toast.error(parseApiError(error, t('showcaseReportButton.couldNotSubmitTheReportTry')).message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <Button size="sm" variant="outline" icon={Flag} disabled={submitted} onClick={() => setOpen(true)}>{submitted ? t('showcaseReportButton.reported') : t('showcaseReportButton.report')}</Button>
      <Modal isOpen={open} onClose={() => { if (!submitting) setOpen(false); }} title={commentId ? t('showcaseReportButton.reportComment') : t('showcaseReportButton.reportShowcase')}
        submitText={t('showcaseReportButton.submitReport')} isSubmitting={submitting} submitDisabled={!canSubmit} onSubmit={submit}>
        <div className="space-y-4">
          <p className="text-body-sm text-slate-600">{t('showcaseReportButton.reportsAreReviewedByAuthorisedStaff')}</p>
          {!isComment && <label className="block text-body-sm font-semibold text-secondary">{t('common:dialog.reason')}
            <select value={reason} onChange={(event) => { setReason(event.target.value as ShowcaseReportReason); setCategory(''); }}
              className="mt-1.5 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-body">
              {REASONS.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
            </select>
          </label>}
          {(isComment || reason !== 'CopyrightViolation') && <div>
            <label className="block text-body-sm font-semibold text-secondary">{t('autoModeration.category')} {!isComment && <span className="font-normal text-slate-500">{t('showcaseReportButton.optional')}</span>}
              <select value={selectedCategory} required={isComment} aria-required={isComment} disabled={!categories.isSuccess || submitting} onChange={event => setCategory(categoryOptions.find(item => item.code === event.target.value)?.code ?? '')}
                className="mt-1.5 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-body">
                <option value="">{t('ruleProposals.chooseCategory')}</option>
                {categoryOptions.map(item => <option key={item.code} value={item.code}>{showcaseCategoryLabel(item, language.resolvedLanguage ?? language.language)}</option>)}
              </select>
            </label>
            {categories.isPending && <p role="status" className="mt-2 text-body-sm text-slate-600">{t('violationCategories.loading')}</p>}
            {categories.isError && <div role="alert" className="mt-2"><p className="text-body-sm text-slate-600">{t('violationCategories.loadFailed')}</p><Button size="sm" variant="outline" onClick={() => { void categories.refetch(); }}>{t('showcaseComments.retry')}</Button></div>}
          </div>}
          <label className="block text-body-sm font-semibold text-secondary">{t('showcaseReportButton.note')} <span className="font-normal text-slate-500">{t('showcaseReportButton.optional')}</span>
            <textarea value={note} onChange={(event) => setNote(event.target.value)} maxLength={1000} rows={4}
              className="mt-1.5 w-full resize-none rounded-lg border border-slate-300 px-3 py-2 text-body" placeholder={t('showcaseReportButton.addContextForTheReviewer')} />
          </label>
        </div>
      </Modal>
    </>
  );
}
