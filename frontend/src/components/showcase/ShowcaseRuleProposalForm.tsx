import { useId, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import toast from 'react-hot-toast';
import Button from '../ui/Button';
import { showcaseApi } from '../../api/showcaseApi';
import { parseApiError } from '../../utils/apiError';
import { isShowcasePhraseInComment, showcaseCategoryLabel } from '../../utils/showcaseModeration';
import { useShowcaseViolationCategories } from '../../hooks/useShowcaseViolationCategories';
import type { ShowcaseReportDto, ShowcaseViolationCategory } from '../../types/showcase';

export default function ShowcaseRuleProposalForm({ report, isAdmin = false }: { report: ShowcaseReportDto; isAdmin?: boolean }) {
  const { t, i18n } = useTranslation('showcase');
  const queryClient = useQueryClient();
  const panelId = useId();
  const [expanded, setExpanded] = useState(false);
  const [phrase, setPhrase] = useState('');
  const [category, setCategory] = useState<ShowcaseViolationCategory | ''>('');
  const categories = useShowcaseViolationCategories(expanded);
  const submit = useMutation({
    mutationFn: (body: { phrase: string; category: ShowcaseViolationCategory }) => showcaseApi.proposeRule(report.id, body),
    onSuccess: async result => {
      await queryClient.invalidateQueries({ queryKey: ['showcase', 'rule-proposals'] });
      if (result.status === 'Approved') await queryClient.invalidateQueries({ queryKey: ['showcase', 'moderation-rules'] });
      toast.success(t(result.status === 'Approved' ? 'ruleProposals.activated' : 'ruleProposals.submitted'));
      setExpanded(false); setPhrase(''); setCategory('');
    },
    onError: error => toast.error(parseApiError(error, t('ruleProposals.submitFailed')).message),
  });
  if (!report.commentId || report.status === 'Dismissed' || (report.sources && !report.sources.includes('User'))) return null;
  const source = report.commentContent;
  const validPhrase = !!source && isShowcasePhraseInComment(source, phrase);
  return <div className="mt-4 border-t border-slate-200 pt-4">
    <Button type="button" size="sm" variant="outline" aria-expanded={expanded} aria-controls={panelId}
      disabled={!source || submit.isPending} title={!source ? t('ruleProposals.sourceUnavailable') : undefined}
      onClick={() => { submit.reset(); setExpanded(!expanded); }}>{t(isAdmin ? 'ruleProposals.addAndActivate' : 'ruleProposals.propose')}</Button>
    {expanded && <form id={panelId} className="mt-4 space-y-4" onSubmit={event => {
      event.preventDefault();
      if (validPhrase && category && categories.isSuccess && !submit.isPending) submit.mutate({ phrase: phrase.trim(), category });
    }}>
      <div><p className="mb-2 text-body-sm font-semibold text-secondary">{t('ruleProposals.comment')}</p>
        <blockquote className="whitespace-pre-wrap break-words border-l-4 border-slate-300 pl-3 text-body-sm text-slate-700">{source}</blockquote>
      </div>
      <label className="block text-body-sm font-semibold text-secondary">{t('ruleProposals.phrase')}
        <input autoFocus value={phrase} maxLength={120} aria-required="true" disabled={submit.isPending} placeholder={t('ruleProposals.placeholder')}
          onChange={event => setPhrase(event.target.value)} className="mt-2 w-full rounded-lg border border-slate-300 bg-white p-3 text-body font-normal text-secondary" />
      </label>
      {phrase.trim() && !validPhrase && <p role="alert" className="text-body-sm text-danger-dark">{t('ruleProposals.invalidPhrase')}</p>}
      <label className="block text-body-sm font-semibold text-secondary">{t('autoModeration.category')}
        <select value={category} aria-required="true" disabled={submit.isPending || !categories.isSuccess} className="mt-2 w-full rounded-lg border border-slate-300 bg-white p-3 text-body font-normal text-secondary"
          onChange={event => setCategory(categories.data?.find(item => item.code === event.target.value)?.code ?? '')}>
          <option value="">{t('ruleProposals.chooseCategory')}</option>
          {categories.data?.map(item => <option key={item.code} value={item.code}>{showcaseCategoryLabel(item, i18n.resolvedLanguage ?? i18n.language)}</option>)}
        </select>
      </label>
      {categories.isPending && <p role="status" className="text-body-sm text-slate-600">{t('violationCategories.loading')}</p>}
      {categories.isError && <div role="alert"><p className="text-body-sm text-danger-dark">{t('violationCategories.loadFailed')}</p><Button size="sm" variant="outline" onClick={() => { void categories.refetch(); }}>{t('showcaseComments.retry')}</Button></div>}
      <p className="text-body-sm text-slate-600">{t(isAdmin ? 'ruleProposals.adminHelp' : 'ruleProposals.pendingHelp')}</p>
      {submit.isError && <p role="alert" className="text-body-sm text-danger-dark">{parseApiError(submit.error, t('ruleProposals.submitFailed')).message}</p>}
      <div className="flex flex-wrap gap-2">
        <Button type="submit" size="sm" variant="outline" isLoading={submit.isPending} disabled={!validPhrase || !category || !categories.isSuccess || submit.isPending}>{t(isAdmin ? 'ruleProposals.approve' : 'ruleProposals.confirm')}</Button>
        <Button type="button" size="sm" variant="ghost" disabled={submit.isPending} onClick={() => { setExpanded(false); submit.reset(); }}>{t('common:actions.cancel')}</Button>
      </div>
    </form>}
  </div>;
}
