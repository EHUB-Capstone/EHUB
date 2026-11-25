import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import toast from 'react-hot-toast';
import Button from '../ui/Button';
import Modal from '../ui/Modal';
import { showcaseApi } from '../../api/showcaseApi';
import { parseApiError } from '../../utils/apiError';
import { SHOWCASE_VIOLATION_CATEGORIES, showcaseCategoryFromReason, showcaseReasonFromCategory } from '../../utils/showcaseModeration';
import type { SaveShowcaseModerationRuleRequest, ShowcaseModerationRuleDto } from '../../types/showcase';

const emptyRule: SaveShowcaseModerationRuleRequest = { phrase: '', description: '', reason: 'Harassment', category: 'Insult', isEnabled: true };

export default function ShowcaseModerationRules() {
  const { t } = useTranslation('showcase');
  const queryClient = useQueryClient();
  const [expanded, setExpanded] = useState(false);
  const [editing, setEditing] = useState<{ id: string | null; body: SaveShowcaseModerationRuleRequest } | null>(null);
  const rules = useQuery({ queryKey: ['showcase', 'moderation-rules'], queryFn: ({ signal }) => showcaseApi.getModerationRules(signal), enabled: expanded });
  const save = useMutation({
    mutationFn: ({ id, body }: NonNullable<typeof editing>) => showcaseApi.saveModerationRule(id, body),
    onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ['showcase', 'moderation-rules'] }); setEditing(null); toast.success(t('autoModeration.saved')); },
    onError: (error) => toast.error(parseApiError(error, t('autoModeration.saveFailed')).message),
  });
  const edit = (rule: ShowcaseModerationRuleDto) => setEditing({ id: rule.id, body: { phrase: rule.phrase, description: rule.description, reason: rule.reason, category: rule.category ?? showcaseCategoryFromReason(rule.reason), isEnabled: rule.isEnabled, rowVersion: rule.rowVersion } });
  return <section className="mb-5 rounded-xl border border-slate-200 bg-white p-5">
    <button type="button" className="flex min-h-9 w-full items-center justify-between text-left font-semibold text-secondary" aria-expanded={expanded} aria-controls="showcase-auto-rules" onClick={() => setExpanded(!expanded)}>{t('autoModeration.rulesTitle')}<span aria-hidden="true">{expanded ? '−' : '+'}</span></button>
    {expanded && <div id="showcase-auto-rules" className="mt-4 space-y-4">
      <p className="text-body-sm text-slate-600">{t('autoModeration.rulesHelp')}</p>
      <Button size="sm" onClick={() => setEditing({ id: null, body: { ...emptyRule } })}>{t('autoModeration.addRule')}</Button>
      {rules.isPending && <p role="status">{t('autoModeration.loading')}</p>}
      {rules.isError && <div role="alert"><p>{t('autoModeration.loadFailed')}</p><Button size="sm" variant="outline" onClick={() => { void rules.refetch(); }}>{t('showcaseComments.retry')}</Button></div>}
      {rules.isSuccess && <ul className="divide-y divide-slate-200">{rules.data.map(rule => <li key={rule.id} className="flex items-start justify-between gap-4 py-3">
        <div className="min-w-0"><p className="break-words font-semibold text-secondary">{rule.phrase}</p><p className="break-words text-body-sm text-slate-600">{rule.description}</p><p className="text-caption text-slate-500">{t(`autoModeration.categories.${rule.category ?? showcaseCategoryFromReason(rule.reason)}`)} · {t(rule.isEnabled ? 'autoModeration.enabled' : 'autoModeration.disabled')}</p></div>
        <Button size="sm" variant="outline" onClick={() => edit(rule)}>{t('autoModeration.edit')}</Button>
      </li>)}</ul>}
    </div>}
    <Modal isOpen={editing !== null} onClose={() => { if (!save.isPending) setEditing(null); }} title={t('autoModeration.rulesTitle')} submitText={t('autoModeration.save')} isSubmitting={save.isPending}
      submitDisabled={!editing?.body.phrase.trim() || !editing?.body.description.trim()} onSubmit={() => { if (editing) save.mutate(editing); }}>
      {editing && <div className="space-y-4">
        <label className="block text-body-sm">{t('autoModeration.phrase')}<input className="mt-1 w-full rounded-lg border border-slate-300 bg-white p-2 text-secondary" value={editing.body.phrase} maxLength={120} onChange={e => setEditing({ ...editing, body: { ...editing.body, phrase: e.target.value } })} /></label>
        <label className="block text-body-sm">{t('autoModeration.description')}<input className="mt-1 w-full rounded-lg border border-slate-300 bg-white p-2 text-secondary" value={editing.body.description} maxLength={300} onChange={e => setEditing({ ...editing, body: { ...editing.body, description: e.target.value } })} /></label>
        <label className="block text-body-sm">{t('autoModeration.category')}<select className="mt-1 w-full rounded-lg border border-slate-300 bg-white p-2 text-secondary" value={editing.body.category ?? showcaseCategoryFromReason(editing.body.reason)} onChange={e => {
          const category = SHOWCASE_VIOLATION_CATEGORIES.find(item => item === e.target.value);
          if (category) setEditing({ ...editing, body: { ...editing.body, category, reason: showcaseReasonFromCategory(category) } });
        }}>
          {SHOWCASE_VIOLATION_CATEGORIES.map(category => <option key={category} value={category}>{t(`autoModeration.categories.${category}`)}</option>)}
        </select></label>
        <label className="flex min-h-9 items-center gap-2 text-body-sm"><input type="checkbox" checked={editing.body.isEnabled} onChange={e => setEditing({ ...editing, body: { ...editing.body, isEnabled: e.target.checked } })} />{t('autoModeration.enabled')}</label>
      </div>}
    </Modal>
  </section>;
}
