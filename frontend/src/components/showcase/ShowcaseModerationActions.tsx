import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { Check, EyeOff, Eye, XCircle } from 'lucide-react';
import Button from '../ui/Button';
import ConfirmDialog from '../ui/ConfirmDialog';
import { showcaseApi } from '../../api/showcaseApi';
import { moderationActionsFor } from '../../utils/showcase';
import { parseApiError } from '../../utils/apiError';
import type { ShowcaseDecision, ShowcaseDto, ShowcaseStatus } from '../../types/showcase';
import { useTranslation } from 'react-i18next';
import i18n from '../../i18n';

type DialogDecision = Extract<ShowcaseDecision, 'reject' | 'hide'>;

const DIALOGS: Record<DialogDecision, { title: string; description: string; confirm: string; success: string }> = {
  reject: {
    get title() { return i18n.t('showcase:showcaseModerationActions.rejectThisPage'); },
    get description() { return i18n.t('showcase:showcaseModerationActions.thePageReturnsToDraftAnd'); },
    get confirm() { return i18n.t('showcase:showcaseModerationActions.rejectPage'); },
    get success() { return i18n.t('showcase:showcaseModerationActions.showcaseRejected'); },
  },
  hide: {
    get title() { return i18n.t('showcase:showcaseModerationActions.hideThisPage'); },
    get description() { return i18n.t('showcase:showcaseModerationActions.thePageIsRemovedFromThe'); },
    get confirm() { return i18n.t('showcase:showcaseModerationActions.hidePage'); },
    get success() { return i18n.t('showcase:showcaseModerationActions.showcaseHidden'); },
  },
};

interface ShowcaseModerationActionsProps {
  showcaseId: string;
  title: string;
  status: ShowcaseStatus;
  wasEverPublished: boolean;
  onDecided: (showcase: ShowcaseDto) => void;
}

/** Decision buttons for a reviewer; only the actions the server would accept for the current status are shown. */
export default function ShowcaseModerationActions({ showcaseId, title, status, wasEverPublished, onDecided }: ShowcaseModerationActionsProps) {
  const { t } = useTranslation('showcase');
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState<ShowcaseDecision | null>(null);
  const [dialog, setDialog] = useState<DialogDecision | null>(null);
  const [note, setNote] = useState('');
  const actions = moderationActionsFor(status, wasEverPublished);

  if (actions.length === 0) return null;

  const run = async (decision: ShowcaseDecision, text?: string) => {
    if (busy) return false;
    setBusy(decision);
    try {
      const updated = await showcaseApi.decide(showcaseId, decision, text);
      void queryClient.invalidateQueries({ queryKey: ['showcase'] });
      onDecided(updated);
      return true;
    } catch (error) {
      // The page may have changed since the list was loaded; keep the dialog open so the note is not lost.
      toast.error(parseApiError(error, t('showcaseModerationActions.unableToSaveTheDecisionTry')).message);
      return false;
    } finally {
      setBusy(null);
    }
  };

  const quick = async (decision: 'approve' | 'unhide') => {
    if (await run(decision)) toast.success(decision === 'approve' ? t('showcaseModerationActions.showcaseApproved') : t('showcaseModerationActions.showcaseRestored'));
  };

  const confirmDialog = async () => {
    if (!dialog) return;
    if (await run(dialog, note)) {
      toast.success(DIALOGS[dialog].success);
      setDialog(null);
      setNote('');
    }
  };

  return (
    <>
      <div className="flex flex-wrap gap-2">
        {actions.includes('approve') && (
          <Button size="sm" variant="secondary" icon={Check} isLoading={busy === 'approve'} disabled={busy !== null} onClick={() => quick('approve')}>{t('showcaseModerationActions.approve')}</Button>
        )}
        {actions.includes('unhide') && (
          <Button size="sm" variant="secondary" icon={Eye} isLoading={busy === 'unhide'} disabled={busy !== null} onClick={() => quick('unhide')}>{t('showcaseModerationActions.showAgain')}</Button>
        )}
        {actions.includes('reject') && (
          <Button size="sm" variant="danger-outline" icon={XCircle} disabled={busy !== null} onClick={() => setDialog('reject')}>{t('showcaseModerationActions.rejectProject')}</Button>
        )}
        {actions.includes('hide') && (
          <Button size="sm" variant="danger-outline" icon={EyeOff} disabled={busy !== null} onClick={() => setDialog('hide')}>{t('showcaseModerationActions.hide')}</Button>
        )}
      </div>

      <ConfirmDialog
        isOpen={dialog !== null}
        onClose={() => { if (!busy) { setDialog(null); setNote(''); } }}
        onConfirm={confirmDialog}
        title={dialog ? `${DIALOGS[dialog].title}: ${title}` : ''}
        description={dialog ? DIALOGS[dialog].description : ''}
        confirmText={dialog ? DIALOGS[dialog].confirm : ''}
        confirmVariant="danger"
        reason={note}
        onReasonChange={setNote}
        reasonLabel={t('showcaseModerationActions.noteForTheTeam')}
        reasonRequired
        isSubmitting={busy !== null}
      />
    </>
  );
}
