import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import toast from 'react-hot-toast';
import Badge from '../ui/Badge';
import Button from '../ui/Button';
import Modal from '../ui/Modal';
import ConfirmDialog from '../ui/ConfirmDialog';
import { showcaseApi } from '../../api/showcaseApi';
import { parseApiError } from '../../utils/apiError';
import { matchesSearchQuery } from '../../utils/searchText';
import { showcaseCategoryLabel, showcaseCategoryFromReason, showcaseReasonFromCategory, supportsShowcasePhraseBlocking } from '../../utils/showcaseModeration';
import { useShowcaseViolationCategories } from '../../hooks/useShowcaseViolationCategories';
import { formatDateTime as fmtDateTime } from '../../i18n/format';
import type { SaveShowcaseModerationRuleRequest, ShowcaseModerationRuleDto, ShowcaseModerationRuleStatus, ShowcaseViolationCategory } from '../../types/showcase';

type RuleDraft = SaveShowcaseModerationRuleRequest & { status: ShowcaseModerationRuleStatus; category: ShowcaseViolationCategory };
type EditingRule = { id: string | null; body: RuleDraft; originalStatus?: ShowcaseModerationRuleStatus };
const emptyRule: RuleDraft = { phrase: '', description: '', reason: 'Harassment', category: 'Insult', status: 'Active' };

export default function ShowcaseModerationRules({ standalone = false }: { standalone?: boolean }) {
  const { t, i18n } = useTranslation('showcase');
  const queryClient = useQueryClient();
  const [expanded, setExpanded] = useState(standalone);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<'All' | ShowcaseModerationRuleStatus>('All');
  const [category, setCategory] = useState<ShowcaseViolationCategory | ''>('');
  const [editing, setEditing] = useState<EditingRule | null>(null);
  const [confirmation, setConfirmation] = useState<EditingRule | null>(null);
  const categories = useShowcaseViolationCategories(expanded || editing !== null);
  const blockingCategories = categories.data?.filter(supportsShowcasePhraseBlocking) ?? [];
  const language = i18n.resolvedLanguage ?? i18n.language;
  const rules = useQuery({ queryKey: ['showcase', 'moderation-rules'], queryFn: ({ signal }) => showcaseApi.getModerationRules(signal), enabled: expanded });
  const filtered = rules.data?.filter(rule => (status === 'All' || rule.status === status) && (!category || rule.category === category)
    && matchesSearchQuery(search, [rule.phrase, rule.description])) ?? [];
  const save = useMutation({
    mutationFn: ({ id, body }: EditingRule) => showcaseApi.saveModerationRule(id, body),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['showcase', 'moderation-rules'] });
      setConfirmation(null); setEditing(null); toast.success(t('autoModeration.saved'));
    },
    onError: error => { setConfirmation(null); toast.error(parseApiError(error, t('autoModeration.saveFailed')).message); },
  });
  const edit = (rule: ShowcaseModerationRuleDto) => {
    save.reset();
    setEditing({ id: rule.id, originalStatus: rule.status, body: { phrase: rule.phrase, description: rule.description, reason: rule.reason,
      category: rule.category ?? showcaseCategoryFromReason(rule.reason), status: rule.status, rowVersion: rule.rowVersion } });
  };
  const canSave = !!editing && !!editing.body.phrase.trim() && !!editing.body.description.trim() && categories.isSuccess
    && blockingCategories.some(item => item.code === editing.body.category);
  return <section className="mb-5 rounded-xl border border-slate-200 bg-white p-5">
    {standalone ? <h2 className="text-subheading text-secondary">{t('autoModeration.rulesTitle')}</h2>
      : <button type="button" className="flex min-h-9 w-full items-center justify-between text-left font-semibold text-secondary" aria-expanded={expanded} aria-controls="showcase-auto-rules" onClick={() => setExpanded(!expanded)}>{t('autoModeration.rulesTitle')}<span aria-hidden="true">{expanded ? '−' : '+'}</span></button>}
    {expanded && <div id="showcase-auto-rules" className="mt-4 space-y-5">
      <p className="text-body-sm text-slate-600">{t('autoModeration.rulesHelp')}</p>
      <div className="flex flex-wrap items-end gap-3">
        <label className="min-w-48 flex-1 text-body-sm font-semibold text-secondary">{t('violationManagement.search')}
          <input type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder={t('violationManagement.searchPlaceholder')} className="mt-2 w-full rounded-lg border border-slate-300 bg-white p-2 text-body font-normal text-secondary" />
        </label>
        <label className="text-body-sm font-semibold text-secondary">{t('autoModeration.status')}
          <select value={status} onChange={event => setStatus(event.target.value as typeof status)} className="mt-2 block w-full rounded-lg border border-slate-300 bg-white p-2 text-body font-normal text-secondary">
            <option value="All">{t('violationManagement.allStatuses')}</option>
            {(['Active', 'Inactive'] as const).map(item => <option key={item} value={item}>{t(`autoModeration.statuses.${item}`)}</option>)}
          </select>
        </label>
        <label className="text-body-sm font-semibold text-secondary">{t('autoModeration.category')}
          <select value={category} onChange={event => setCategory(blockingCategories.find(item => item.code === event.target.value)?.code ?? '')} className="mt-2 block w-full rounded-lg border border-slate-300 bg-white p-2 text-body font-normal text-secondary">
            <option value="">{t('violationManagement.allCategories')}</option>
            {blockingCategories.map(item => <option key={item.code} value={item.code}>{showcaseCategoryLabel(item, language)}</option>)}
          </select>
        </label>
        <Button size="sm" disabled={!categories.isSuccess} onClick={() => { save.reset(); setEditing({ id: null, body: { ...emptyRule } }); }}>{t('autoModeration.addRule')}</Button>
      </div>
      {rules.isPending && <p role="status">{t('autoModeration.loading')}</p>}
      {rules.isError && <div role="alert"><p>{t('autoModeration.loadFailed')}</p><Button size="sm" variant="outline" onClick={() => { void rules.refetch(); }}>{t('showcaseComments.retry')}</Button></div>}
      {rules.isSuccess && <>
        <p className="text-body-sm text-slate-600">{t('violationManagement.count', { count: filtered.length, total: rules.data.length })}</p>
        {filtered.length === 0 ? <p role="status" className="py-5 text-body-sm text-slate-600">{t('violationManagement.empty')}</p>
          : <div className="overflow-x-auto"><table className="w-full text-left text-body-sm">
            <thead className="border-b border-slate-200 text-slate-600"><tr>
              <th scope="col" className="px-3 py-3">{t('autoModeration.phrase')}</th><th scope="col" className="px-3 py-3">{t('autoModeration.category')}</th>
              <th scope="col" className="px-3 py-3">{t('autoModeration.description')}</th><th scope="col" className="px-3 py-3">{t('autoModeration.status')}</th>
              <th scope="col" className="px-3 py-3">{t('violationManagement.actions')}</th>
            </tr></thead>
            <tbody className="divide-y divide-slate-200">{filtered.map(rule => <tr key={rule.id}>
              <td className="break-words px-3 py-3 text-secondary"><span className="font-semibold">{rule.phrase}</span>
                <details className="mt-2 text-caption text-slate-600"><summary className="min-h-9 cursor-pointer">{t('reportAudit.ruleHistory')}</summary>
                  <p>{t('reportAudit.createdBy', { name: rule.createdByName ?? t('reportAudit.unknownActor'), date: rule.createdAtUtc ? fmtDateTime(rule.createdAtUtc) : t('reportAudit.unknownTime') })}</p>
                  <p className="mt-1">{t('reportAudit.updatedBy', { name: rule.updatedByName ?? t('reportAudit.unknownActor'), date: rule.updatedAtUtc ? fmtDateTime(rule.updatedAtUtc) : t('reportAudit.unknownTime') })}</p>
                </details>
              </td>
              <td className="px-3 py-3 text-slate-600">{t(`autoModeration.categories.${rule.category}`)}</td>
              <td className="max-w-md whitespace-pre-wrap break-words px-3 py-3 text-slate-600">{rule.description}</td>
              <td className="px-3 py-3"><Badge variant={rule.status}>{t(`autoModeration.statuses.${rule.status}`)}</Badge></td>
              <td className="px-3 py-3"><Button size="sm" variant="outline" onClick={() => edit(rule)}>{t('autoModeration.edit')}</Button></td>
            </tr>)}</tbody>
          </table></div>}
      </>}
      {categories.isPending && <p role="status">{t('violationCategories.loading')}</p>}
      {categories.isError && <div role="alert"><p>{t('violationCategories.loadFailed')}</p><Button size="sm" variant="outline" onClick={() => { void categories.refetch(); }}>{t('showcaseComments.retry')}</Button></div>}
      {categories.isSuccess && <details className="border-t border-slate-200 pt-4">
        <summary className="min-h-9 cursor-pointer font-semibold text-secondary">{t('violationManagement.categoryGuide')}</summary>
        <p className="mt-2 text-body-sm text-slate-600">{t('violationManagement.contextHelp')}</p>
        <ul className="mt-3 divide-y divide-slate-200">{categories.data.map(item => <li key={item.code} className="space-y-2 py-3">
          <div className="flex flex-wrap items-center gap-2"><span className="font-semibold text-secondary">{showcaseCategoryLabel(item, language)}</span>
            <Badge variant={supportsShowcasePhraseBlocking(item) ? 'info' : 'neutral'}>{t(supportsShowcasePhraseBlocking(item) ? 'violationManagement.keywordBlocking' : 'violationManagement.manualReview')}</Badge>
          </div>
          <p className="text-body-sm text-slate-600">{language.startsWith('vi') ? item.descriptionVi : item.descriptionEn}</p>
        </li>)}</ul>
      </details>}
    </div>}
    <Modal isOpen={editing !== null && confirmation === null} onClose={() => { if (!save.isPending) setEditing(null); }} title={t('autoModeration.rulesTitle')}
      submitText={t('autoModeration.save')} isSubmitting={save.isPending} submitDisabled={!canSave || save.isPending} onSubmit={() => {
        if (!editing || !canSave || save.isPending) return;
        if (editing.id && editing.originalStatus !== editing.body.status) setConfirmation(editing);
        else save.mutate(editing);
      }}>
      {editing && <div className="space-y-4">
        <label className="block text-body-sm">{t('autoModeration.phrase')}<input autoFocus disabled={save.isPending} className="mt-1 w-full rounded-lg border border-slate-300 bg-white p-2 text-secondary" value={editing.body.phrase} maxLength={120} onChange={event => setEditing({ ...editing, body: { ...editing.body, phrase: event.target.value } })} /></label>
        <label className="block text-body-sm">{t('autoModeration.description')}<textarea disabled={save.isPending} rows={3} className="mt-1 w-full rounded-lg border border-slate-300 bg-white p-2 text-secondary" value={editing.body.description} maxLength={300} onChange={event => setEditing({ ...editing, body: { ...editing.body, description: event.target.value } })} /></label>
        <label className="block text-body-sm">{t('autoModeration.category')}<select disabled={!categories.isSuccess || save.isPending} className="mt-1 w-full rounded-lg border border-slate-300 bg-white p-2 text-secondary" value={editing.body.category} onChange={event => {
          const selected = blockingCategories.find(item => item.code === event.target.value)?.code;
          if (selected) setEditing({ ...editing, body: { ...editing.body, category: selected, reason: showcaseReasonFromCategory(selected) } });
        }}>{blockingCategories.map(item => <option key={item.code} value={item.code}>{showcaseCategoryLabel(item, language)}</option>)}</select></label>
        <label className="block text-body-sm">{t('autoModeration.status')}<select disabled={save.isPending} value={editing.body.status} onChange={event => setEditing({ ...editing, body: { ...editing.body, status: event.target.value as ShowcaseModerationRuleStatus } })} className="mt-1 w-full rounded-lg border border-slate-300 bg-white p-2 text-secondary">
          {(['Active', 'Inactive'] as const).map(item => <option key={item} value={item}>{t(`autoModeration.statuses.${item}`)}</option>)}
        </select></label>
        {categories.isPending && <p role="status">{t('violationCategories.loading')}</p>}
        {categories.isError && <div role="alert"><p>{t('violationCategories.loadFailed')}</p><Button size="sm" variant="outline" onClick={() => { void categories.refetch(); }}>{t('showcaseComments.retry')}</Button></div>}
        {save.isError && <p role="alert" className="text-body-sm text-danger-dark">{parseApiError(save.error, t('autoModeration.saveFailed')).message}</p>}
      </div>}
    </Modal>
    <ConfirmDialog isOpen={confirmation !== null} onClose={() => { if (!save.isPending) setConfirmation(null); }} title={t('violationManagement.confirmStatusTitle')}
      description={t('violationManagement.confirmStatusHelp', { phrase: confirmation?.body.phrase, status: t(`autoModeration.statuses.${confirmation?.body.status ?? 'Active'}`) })}
      confirmText={t('autoModeration.save')} isSubmitting={save.isPending} onConfirm={() => { if (confirmation && !save.isPending) save.mutate(confirmation); }} />
  </section>;
}
