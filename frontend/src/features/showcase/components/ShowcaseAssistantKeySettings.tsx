import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Trash2 } from 'lucide-react';
import Button from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';
import type { AssistantKeyStorage } from '../../../utils/showcaseAssistant';

/**
 * Where a key the user types is kept. Nothing is kept beyond this page unless they choose it, and the option that keeps it on the
 * server only appears when the server can encrypt it. The hint under the chosen option says plainly what that choice means.
 */
export function KeyStorageChoice({ value, accountAvailable, disabled, onChange }: {
  value: AssistantKeyStorage;
  accountAvailable: boolean;
  disabled: boolean;
  onChange: (value: AssistantKeyStorage) => void;
}) {
  const { t } = useTranslation('showcase');
  const name = useId();
  const hintId = useId();
  const options: { value: AssistantKeyStorage; label: string; hint: string }[] = [
    { value: 'session', label: t('showcaseAssistant.storageSession'), hint: t('showcaseAssistant.storageSessionHint') },
    { value: 'tab', label: t('showcaseAssistant.storageTab'), hint: t('showcaseAssistant.storageTabHint') },
    ...(accountAvailable
      ? [{ value: 'account' as const, label: t('showcaseAssistant.storageAccount'), hint: t('showcaseAssistant.storageAccountHint') }]
      : []),
  ];
  const chosen = options.find(option => option.value === value) ?? options[0];
  return (
    // Native radios, shown as one row of buttons so the choice takes little room; the line under them says what the chosen one means.
    <fieldset disabled={disabled} aria-describedby={hintId} className="min-w-0">
      <legend className="text-caption font-semibold text-slate-700 dark:text-slate-200">{t('showcaseAssistant.storageLegend')}</legend>
      <div className={cn('mt-1 grid gap-1.5', options.length === 3 ? 'grid-cols-3' : 'grid-cols-2')}>
        {options.map(option => (
          <label key={option.value} className="relative block min-w-0 cursor-pointer">
            <input
              type="radio"
              name={name}
              value={option.value}
              checked={value === option.value}
              onChange={() => onChange(option.value)}
              className="peer sr-only"
            />
            {/* The chosen look is picked here, not with peer-checked: the dark-theme overrides in index.css sit outside Tailwind's layers and would win. */}
            <span className={cn(
              'flex min-h-9 items-center justify-center rounded-lg border px-2 py-1.5 text-center text-caption font-semibold transition-colors peer-focus-visible:ring-2 peer-focus-visible:ring-primary peer-disabled:opacity-50',
              value === option.value
                ? 'border-primary bg-primary-50 text-primary-800 dark:border-primary-700 dark:bg-primary-900/30 dark:text-primary-200'
                : 'border-slate-300 bg-white text-slate-700 hover:border-slate-400 dark:border-slate-600 dark:bg-dark-surface dark:text-slate-200',
            )}>
              {option.label}
            </span>
          </label>
        ))}
      </div>
      <p id={hintId} className="mt-1.5 text-caption text-slate-500 dark:text-slate-400">{chosen.hint}</p>
    </fieldset>
  );
}

const rowClass = 'rounded-lg bg-slate-50 px-3 py-2 text-body-sm text-slate-700 dark:bg-secondary-800 dark:text-slate-200';

/** The key saved to the account. Taking it away asks first, and only counts as done once the server has removed it. */
function SavedKeyRow({ last4, refused, onRemove }: { last4: string; refused: boolean; onRemove: () => Promise<boolean> }) {
  const { t } = useTranslation('showcase');
  const [confirming, setConfirming] = useState(false);
  const [removing, setRemoving] = useState(false);
  const remove = async () => {
    if (removing) return;
    setRemoving(true);
    try {
      if (await onRemove()) setConfirming(false);
    } finally {
      setRemoving(false);
    }
  };
  return (
    <li className={rowClass}>
      <div className="flex items-center justify-between gap-2">
        <span className="min-w-0">{refused ? t('showcaseAssistant.savedKeyRefused', { last4 }) : t('showcaseAssistant.savedKeyInUse', { last4 })}</span>
        {!confirming && (
          <Button size="sm" variant="danger-outline" icon={Trash2} className="shrink-0" onClick={() => setConfirming(true)}>
            {t('showcaseAssistant.removeSavedKey')}
          </Button>
        )}
      </div>
      {confirming && (
        <div role="group" aria-label={t('showcaseAssistant.confirmRemoveSavedKey')} className="mt-2 flex flex-wrap items-center justify-between gap-2">
          <span>{t('showcaseAssistant.removeSavedKeyPrompt')}</span>
          <span className="flex items-center gap-2">
            <Button size="sm" variant="ghost" autoFocus disabled={removing} onClick={() => setConfirming(false)}>{t('showcaseAssistant.keepSavedKey')}</Button>
            <Button size="sm" variant="danger" icon={Trash2} isLoading={removing} onClick={() => void remove()}>{t('showcaseAssistant.removeSavedKeyConfirm')}</Button>
          </span>
        </div>
      )}
    </li>
  );
}

/** The keys a question could be asked with right now, each with a way to let go of it. Only the last four characters are ever shown. */
export function CurrentKeys({ typedLast4, typedRemembered, savedLast4, savedRefused, onForgetTyped, onRemoveSaved }: {
  /** The key typed on this page or kept in this tab; empty when there is none. */
  typedLast4: string;
  typedRemembered: boolean;
  /** The key saved to the account, or null when there is none. */
  savedLast4: string | null;
  savedRefused: boolean;
  onForgetTyped: () => void;
  onRemoveSaved: () => Promise<boolean>;
}) {
  const { t } = useTranslation('showcase');
  if (!typedLast4 && savedLast4 === null) return null;
  return (
    <ul aria-label={t('showcaseAssistant.currentKeys')} className="space-y-1.5">
      {typedLast4 && (
        <li className={cn(rowClass, 'flex items-center justify-between gap-2')}>
          <span className="min-w-0">
            {typedRemembered ? t('showcaseAssistant.tabKeyInUse', { last4: typedLast4 }) : t('showcaseAssistant.pageKeyInUse', { last4: typedLast4 })}
          </span>
          <Button size="sm" variant="ghost" className="shrink-0" onClick={onForgetTyped}>{t('showcaseAssistant.forgetKey')}</Button>
        </li>
      )}
      {savedLast4 !== null && <SavedKeyRow last4={savedLast4} refused={savedRefused} onRemove={onRemoveSaved} />}
    </ul>
  );
}
