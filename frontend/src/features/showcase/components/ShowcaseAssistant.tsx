import { useCallback, useEffect, useId, useRef, useState, type FormEvent, type KeyboardEvent, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation } from 'react-router-dom';
import { ExternalLink, Eye, KeyRound, RotateCcw, Send, ThumbsUp, Trash2, X } from 'lucide-react';
import { showcaseApi } from '../../../api/showcaseApi';
import AutoSizeTextarea from '../../../components/ui/AutoSizeTextarea';
import Button from '../../../components/ui/Button';
import { badgeTones } from '../../../components/ui/Badge';
import { useAuthContext } from '../../../context/AuthContext';
import { formatNumber as fmtNumber } from '../../../i18n/format';
import { useAssistantServerKey } from '../hooks/useAssistantServerKey';
import HubbyMascot from './HubbyMascot';
import { CurrentKeys, KeyStorageChoice } from './ShowcaseAssistantKeySettings';
import type { ShowcaseAssistantProjectDto, ShowcaseStage } from '../../../types/showcase';
import { parseApiError } from '../../../utils/apiError';
import { cn } from '../../../utils/cn';
import { getBrowserStorage } from '../../../utils/rubricDraft';
import { stageLabel } from '../../../utils/showcase';
import {
  ASSISTANT_KEY_URL,
  ASSISTANT_MAX_MESSAGE_LENGTH,
  ASSISTANT_STARTER_QUESTIONS,
  assistantErrorMessage,
  assistantKeyLast4,
  assistantKeyProblem,
  assistantKeyRequestError,
  assistantKeySource,
  buildAssistantHistory,
  cleanAssistantKey,
  isAssistantKeyError,
  isAssistantKeyMissing,
  type AssistantChatMessage,
  type AssistantKeyStorage,
} from '../../../utils/showcaseAssistant';
import { loadAssistantChat, saveAssistantChat } from '../../../utils/showcaseAssistantChat';
import { forgetAssistantKey, recallAssistantKey, rememberAssistantKey } from '../../../utils/showcaseAssistantKey';

const GREETED_KEY = 'ehub-hubby-greeted';

const chipClass = 'rounded-full border border-primary-200 bg-primary-50 px-3 py-1.5 text-left text-body-sm font-medium text-primary-800 transition-colors hover:bg-primary-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary disabled:pointer-events-none disabled:opacity-50 dark:border-primary-700 dark:bg-primary-900/30 dark:text-primary-200 dark:hover:bg-primary-900/50';

function ProjectResult({ project, backTo }: { project: ShowcaseAssistantProjectDto; backTo: string }) {
  const { t } = useTranslation('showcase');
  return (
    <li className="relative rounded-xl border border-slate-200 bg-white p-3 transition-colors hover:border-primary hover:bg-slate-50 focus-within:border-primary dark:border-slate-600 dark:bg-dark-surface dark:hover:bg-slate-800">
      <div className="flex items-start gap-3">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-lg border border-slate-200 bg-white dark:border-slate-600 dark:bg-dark-surface">
          {project.logoUrl
            ? <img src={project.logoUrl} alt="" className="h-full w-full object-contain p-0.5" loading="lazy" />
            : <span className="text-body-sm font-semibold text-secondary dark:text-slate-100" aria-hidden="true">{project.projectName.slice(0, 2).toUpperCase()}</span>}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <Link
              to={`/showcase/${project.id}`}
              state={{ showcaseBackTo: backTo, showcaseBackLabel: 'Project Showcase' }}
              className="line-clamp-1 text-body font-semibold text-secondary after:absolute after:inset-0 after:content-[''] focus-visible:underline focus-visible:outline-none dark:text-white"
            >
              {project.projectName}
            </Link>
            <span className={cn('rounded-full border px-2 py-0.5 text-caption font-medium', badgeTones.navy)}>{stageLabel(project.developmentStage as ShowcaseStage)}</span>
          </div>
          {project.reason && <p className="mt-1 text-body-sm text-slate-700 dark:text-slate-200">{project.reason}</p>}
          <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-caption text-slate-500 dark:text-slate-400">
            {project.industries.slice(0, 2).map(industry => <span key={industry} className="font-medium text-primary-700 dark:text-primary-300">{industry}</span>)}
            <span className="inline-flex items-center gap-1"><ThumbsUp className="h-3.5 w-3.5" aria-hidden="true" />{fmtNumber(project.likeCount)}<span className="sr-only"> {t('showcaseAssistant.luotThich')}</span></span>
            <span className="inline-flex items-center gap-1"><Eye className="h-3.5 w-3.5" aria-hidden="true" />{fmtNumber(project.viewCount)}<span className="sr-only"> {t('showcaseAssistant.luotXem')}</span></span>
          </div>
        </div>
      </div>
    </li>
  );
}

/** What both key forms (the full one and the one under a restored conversation) are given. */
interface KeyFormProps {
  draft: string;
  notice: string;
  /** Good news about the last action (saved to the account, removed from it). */
  info: string;
  /** Why the key saved to the account could not be looked up; empty when it could. */
  lookupError: string;
  inputRef: React.Ref<HTMLInputElement>;
  storage: AssistantKeyStorage;
  /** Whether the server can keep keys at all. */
  accountAvailable: boolean;
  saving: boolean;
  /** A key that already works is being changed, so there is something to go back to. */
  changing: boolean;
  /** The keys in use now, each with a way to let go of it. */
  current: ReactNode;
  onDraft: (value: string) => void;
  onStorage: (value: AssistantKeyStorage) => void;
  onSubmit: (event: FormEvent) => void;
  onCancel: () => void;
  onRetryLookup: () => void;
}

function KeyMessages({ info, lookupError, onRetryLookup }: Pick<KeyFormProps, 'info' | 'lookupError' | 'onRetryLookup'>) {
  const { t } = useTranslation('showcase');
  return (
    <>
      {info && <p role="status" className="rounded-lg border border-success-light bg-success-50 px-3 py-2 text-body-sm text-success-dark">{info}</p>}
      {lookupError && (
        <p role="alert" className="flex flex-wrap items-center gap-x-2 text-body-sm text-danger-dark">
          <span>{lookupError}</span>
          <button
            type="button"
            onClick={onRetryLookup}
            className="inline-flex min-h-9 items-center rounded-md px-1 font-semibold text-primary-700 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:text-primary-300"
          >
            {t('showcaseAssistant.thuLai')}
          </button>
        </p>
      )}
    </>
  );
}

/** Shown while the first look at the key saved to the account is running, so the form does not flash up for someone who has one. */
function CheckingSavedKey({ compact }: { compact: boolean }) {
  const { t } = useTranslation('showcase');
  if (compact) return <p role="status" className="border-t border-slate-200 p-3 text-body-sm text-slate-600 dark:border-slate-600 dark:text-slate-300">{t('showcaseAssistant.checkingSavedKey')}</p>;
  return (
    <div role="status" className="flex flex-1 flex-col items-center justify-center gap-2 px-4 py-6 text-center">
      <HubbyMascot mood="thinking" className="h-16 w-auto" />
      <p className="text-body-sm text-slate-600 dark:text-slate-300">{t('showcaseAssistant.checkingSavedKey')}</p>
    </div>
  );
}

/**
 * The assistant runs on the user's own Gemini key: the system has none of its own. This is where it is pasted, and where the
 * user chooses how long it is kept. The key is checked for shape only (nothing is sent anywhere to test it); Google's answer to
 * the first question tells whether it works.
 */
function KeyForm({ draft, notice, info, lookupError, inputRef, storage, accountAvailable, saving, changing, current, onDraft, onStorage, onSubmit, onCancel, onRetryLookup }: KeyFormProps) {
  const { t } = useTranslation('showcase');
  return (
    // The explanation scrolls; the field, the options and the buttons stay in view so the way forward is never below the fold.
    <form onSubmit={onSubmit} aria-label={t('showcaseAssistant.nhapApiKeyGemini')} className="flex min-h-0 flex-1 flex-col">
      <div className="flex-1 space-y-3 overflow-y-auto px-4 py-4">
        <div className="flex flex-col items-center gap-2 text-center"><HubbyMascot className="hubby-float h-12 w-auto" /></div>
        <h3 className="text-body font-semibold text-secondary dark:text-white">{changing ? t('showcaseAssistant.changeKeyTitle') : t('showcaseAssistant.nhapApiKeyGeminiCuaBan')}</h3>
        <KeyMessages info={info} lookupError={lookupError} onRetryLookup={onRetryLookup} />
        {current}
        <p className="text-body-sm text-slate-600 dark:text-slate-300">{t('showcaseAssistant.hubbyChayBangKeyCuaChinh')}</p>
        <ul className="list-disc space-y-1 pl-5 text-caption text-slate-500 dark:text-slate-400">
          <li>{t('showcaseAssistant.keyChiNamTrongBoNho')}</li>
          <li>{t('showcaseAssistant.keyDuocGuiKemTungCau')}</li>
          <li>{t('showcaseAssistant.cauHoiVaThongTinCac')}</li>
        </ul>
        <a
          href={ASSISTANT_KEY_URL}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex min-h-9 items-center gap-1 text-body-sm font-semibold text-primary-700 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:text-primary-300"
        >
          {t('showcaseAssistant.layKeyTaiGoogleAiStudio')} <ExternalLink className="h-3.5 w-3.5" aria-hidden="true" />
          <span className="sr-only"> {t('showcaseAssistant.moTrongTabMoi')}</span>
        </a>
      </div>
      <div className="space-y-2 border-t border-slate-200 px-4 py-3 dark:border-slate-600">
        <KeyField draft={draft} notice={notice} inputRef={inputRef} onDraft={onDraft} label={t('showcaseAssistant.apiKeyGemini')} />
        <KeyStorageChoice value={storage} accountAvailable={accountAvailable} disabled={saving} onChange={onStorage} />
        <div className="flex gap-2">
          {changing && <Button type="button" size="md" variant="ghost" disabled={saving} onClick={onCancel}>{t('common:actions.cancel')}</Button>}
          <Button type="submit" size="md" icon={KeyRound} isLoading={saving} disabled={!draft.trim()} className="flex-1">
            {changing ? t('showcaseAssistant.saveKey') : t('showcaseAssistant.luuKeyVaBatDau')}
          </Button>
        </div>
      </div>
    </form>
  );
}

/** The one place the key is typed: hidden while typing, never auto-filled, and only checked for shape by the parent. */
function KeyField({ draft, notice, inputRef, onDraft, label }: {
  draft: string;
  notice: string;
  inputRef: React.Ref<HTMLInputElement>;
  onDraft: (value: string) => void;
  label: string;
}) {
  const { t } = useTranslation('showcase');
  const id = useId();
  return (
    <div className="min-w-0">
      <label htmlFor={`${id}-key`} className="text-caption font-semibold text-slate-700 dark:text-slate-200">{label}</label>
      <input
        id={`${id}-key`}
        ref={inputRef}
        type="password"
        autoComplete="off"
        autoCapitalize="off"
        spellCheck={false}
        value={draft}
        maxLength={512}
        placeholder={t('showcaseAssistant.danKeyVaoDay')}
        aria-invalid={notice ? true : undefined}
        aria-describedby={notice ? `${id}-notice` : undefined}
        onChange={event => onDraft(event.target.value)}
        className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-body-sm text-slate-900 placeholder:text-slate-400 hover:border-slate-400 focus:border-primary-600 focus:outline-none focus:ring-2 focus:ring-primary/30 dark:border-slate-600 dark:bg-dark-surface dark:text-slate-100"
      />
      {notice && <p id={`${id}-notice`} role="alert" className="mt-1.5 text-body-sm text-danger-dark">{notice}</p>}
    </div>
  );
}

/**
 * Shown under a conversation when there is no key to carry on with (after a reload, or when the key was refused), and when the
 * user chooses to change the key. The conversation stays visible above it.
 */
function ResumeKeyForm({ draft, notice, info, lookupError, inputRef, storage, accountAvailable, saving, changing, current, onDraft, onStorage, onSubmit, onCancel, onRetryLookup }: KeyFormProps) {
  const { t } = useTranslation('showcase');
  return (
    <form onSubmit={onSubmit} aria-label={changing ? t('showcaseAssistant.changeKeyTitle') : t('showcaseAssistant.reenterGeminiKey')} className="max-h-[60%] space-y-2 overflow-y-auto border-t border-slate-200 p-3 dark:border-slate-600">
      <KeyMessages info={info} lookupError={lookupError} onRetryLookup={onRetryLookup} />
      {current}
      <div className="flex items-end gap-2">
        <div className="min-w-0 flex-1">
          <KeyField draft={draft} notice={notice} inputRef={inputRef} onDraft={onDraft} label={changing ? t('showcaseAssistant.apiKeyGemini') : t('showcaseAssistant.pasteKeyToContinue')} />
        </div>
        {changing && <Button type="button" size="md" variant="ghost" disabled={saving} className="shrink-0" onClick={onCancel}>{t('common:actions.cancel')}</Button>}
        <Button type="submit" size="md" icon={KeyRound} isLoading={saving} disabled={!draft.trim()} className="shrink-0">
          {changing ? t('showcaseAssistant.saveKey') : t('showcaseAssistant.resumeConversation')}
        </Button>
      </div>
      <KeyStorageChoice value={storage} accountAvailable={accountAvailable} disabled={saving} onChange={onStorage} />
      <p className="text-caption text-slate-500 dark:text-slate-400">
        {!changing && <>{t('showcaseAssistant.conversationRestored')}{' '}</>}
        <a href={ASSISTANT_KEY_URL} target="_blank" rel="noopener noreferrer" className="font-semibold text-primary-700 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:text-primary-300">
          {t('showcaseAssistant.layKeyTaiGoogleAiStudio')}<span className="sr-only"> {t('showcaseAssistant.moTrongTabMoi')}</span>
        </a>
      </p>
    </form>
  );
}

export default function ShowcaseAssistant(): React.ReactElement {
  const { t } = useTranslation('showcase');
  const location = useLocation();
  const backTo = `${location.pathname}${location.search}`;
  const titleId = useId();
  const [open, setOpen] = useState(false);
  const { user } = useAuthContext();
  // The signed-in account. (Inside send, "userId" is the id of the question message, a different thing.)
  const accountId = user?.id ?? '';
  // The conversation belongs to the signed-in account and is kept in this browser tab, so reloading the page does not lose it.
  // It never includes the API key. Another account never sees it, and signing out removes it.
  const [chat, setChat] = useState(() => ({ owner: accountId, messages: loadAssistantChat(getBrowserStorage(), accountId) }));
  const messages = chat.messages;
  const setMessages = useCallback((update: (current: AssistantChatMessage[]) => AssistantChatMessage[]) =>
    setChat(current => ({ ...current, messages: update(current.messages) })), []);
  const [confirmClear, setConfirmClear] = useState(false);
  const [draft, setDraft] = useState('');
  const [pending, setPending] = useState(false);
  const counter = useRef(0);
  const controller = useRef<AbortController | null>(null);
  const inFlight = useRef(false);
  const listRef = useRef<HTMLDivElement | null>(null);
  const inputRef = useRef<HTMLTextAreaElement | null>(null);
  const launcherRef = useRef<HTMLButtonElement | null>(null);
  const wasOpen = useRef(false);
  const [greeting, setGreeting] = useState(false);
  // The user's own Gemini key. It lives in this component's memory; only when the user chooses is it also kept in this tab's
  // storage under their own account (until they close the tab, sign out or change it), or saved to their account on the server,
  // where it is encrypted and this page never sees it again. A key kept here is sent with each question and the server keeps none.
  const [apiKey, setApiKey] = useState(() => recallAssistantKey(getBrowserStorage(), accountId));
  const [keyRemembered, setKeyRemembered] = useState(() => recallAssistantKey(getBrowserStorage(), accountId) !== '');
  // Where the next key typed is kept. It starts at "this page only" every time, so keeping it anywhere is always a choice.
  const [keyStorage, setKeyStorage] = useState<AssistantKeyStorage>('session');
  const [keyDraft, setKeyDraft] = useState('');
  const [keyNotice, setKeyNotice] = useState('');
  const [keyInfo, setKeyInfo] = useState('');
  const [savingKey, setSavingKey] = useState(false);
  // A ref as well, so two quick presses cannot both pass before React re-renders the disabled button.
  const saving = useRef(false);
  // A key that already works is being changed: the form is shown on request and can be cancelled.
  const [changingKey, setChangingKey] = useState(false);
  // The key saved to the account was just refused. It stays there (a refusal can be a quota that passes) but is not offered
  // again until the user acts or reopens the assistant.
  const [savedRefused, setSavedRefused] = useState(false);
  const keyInputRef = useRef<HTMLInputElement | null>(null);
  const savedKey = useAssistantServerKey(accountId, open);
  const { reload: reloadSavedKey } = savedKey;
  const saved = savedKey.status?.hasKey ? savedKey.status : null;
  const keySource = assistantKeySource(apiKey, savedKey.status, savedRefused);
  // The first look at the account's key is still running and nothing else can be used: wait for it instead of asking for a key.
  const checkingSaved = keySource === 'none' && !changingKey && !savedKey.checked;
  const askForKey = !checkingSaved && (keySource === 'none' || changingKey);

  // Hubby waves hello once per browser session, then stays quiet.
  useEffect(() => {
    let greeted = false;
    try { greeted = window.sessionStorage.getItem(GREETED_KEY) === '1'; } catch { /* storage blocked: greet again next load */ }
    if (greeted) return undefined;
    const show = window.setTimeout(() => {
      if (wasOpen.current) return;
      setGreeting(true);
      try { window.sessionStorage.setItem(GREETED_KEY, '1'); } catch { /* ignore */ }
    }, 1500);
    const hide = window.setTimeout(() => setGreeting(false), 9000);
    return () => { window.clearTimeout(show); window.clearTimeout(hide); };
  }, []);

  // Cancel a request that is still running when the user leaves the showcase pages.
  useEffect(() => () => controller.current?.abort(), []);

  // A different account signed in on this tab: show that account's conversation and key, never keep using the previous ones.
  useEffect(() => {
    if (chat.owner === accountId) return;
    setChat({ owner: accountId, messages: loadAssistantChat(getBrowserStorage(), accountId) });
    const remembered = recallAssistantKey(getBrowserStorage(), accountId);
    setApiKey(remembered);
    setKeyRemembered(remembered !== '');
    setKeyStorage('session');
    setKeyDraft('');
    setKeyNotice('');
    setKeyInfo('');
    setChangingKey(false);
    setSavedRefused(false);
  }, [chat.owner, accountId]);

  // Keep the conversation up to date for the next reload. It is saved under the account it was loaded for.
  useEffect(() => {
    saveAssistantChat(getBrowserStorage(), chat.owner, chat.messages);
  }, [chat]);

  useEffect(() => {
    // When a key is being asked for the first thing to do is paste it, so focus goes there; afterwards it goes to the question box.
    if (open) {
      if (!checkingSaved) (askForKey ? keyInputRef : inputRef).current?.focus();
    } else {
      if (wasOpen.current) launcherRef.current?.focus();
      // A half-typed key does not wait around in memory once the panel is closed, and nothing from this visit carries over.
      setKeyDraft('');
      setKeyStorage('session');
      setKeyNotice('');
      setKeyInfo('');
      setChangingKey(false);
      setSavedRefused(false);
    }
    wasOpen.current = open;
  }, [open, askForKey, checkingSaved]);

  useEffect(() => {
    const list = listRef.current;
    if (list) list.scrollTop = list.scrollHeight;
  }, [messages, pending, open]);

  // Restored messages keep their ids, so new ones must never repeat an id from an earlier visit.
  const nextId = () => `${Date.now().toString(36)}-${++counter.current}`;

  const send = useCallback(async (rawQuestion: string, replace: readonly string[] = []) => {
    const question = rawQuestion.trim();
    // inFlight is a ref so two quick clicks cannot both pass before React re-renders the disabled state.
    if (!question || question.length > ASSISTANT_MAX_MESSAGE_LENGTH || inFlight.current) return;
    if (keySource === 'none') {
      setKeyNotice(t('showcaseAssistant.hayNhapApiKeyGeminiCua'));
      return;
    }
    setKeyNotice('');
    setKeyInfo('');
    inFlight.current = true;
    const userId = nextId();
    const history = buildAssistantHistory(messages, replace);
    setMessages(current => [...current.filter(message => !replace.includes(message.id)), { id: userId, role: 'user', content: question }]);
    setDraft('');
    setPending(true);
    const request = new AbortController();
    controller.current = request;
    try {
      // A key kept on this page or in this tab is sent with the question; without one the server uses the one saved to the account.
      const reply = await showcaseApi.askAssistant({ message: question, history }, apiKey || null, request.signal);
      if (request.signal.aborted) return;
      setMessages(current => [...current, {
        id: nextId(), role: 'assistant', content: reply.answer, projects: reply.projects, suggestions: reply.suggestions,
      }]);
    } catch (error) {
      if (request.signal.aborted) return;
      const message = assistantErrorMessage(error, parseApiError(error, '').message || undefined);
      if (isAssistantKeyError(error)) {
        // The key is the problem, not the question: hand the question back and stop using that key.
        setMessages(current => current.filter(item => item.id !== userId));
        setDraft(question);
        if (apiKey) {
          // The key kept here was refused: forget it, also from this tab's storage, so nothing is retried with it.
          setApiKey('');
          forgetAssistantKey(getBrowserStorage(), accountId);
          setKeyRemembered(false);
        } else {
          // The key saved to the account was refused. It stays there (a refusal can be a quota that passes) but is not offered again
          // until the user acts. When the server has none at all (any more), look again so the screen says so.
          setSavedRefused(true);
          if (isAssistantKeyMissing(error)) reloadSavedKey();
        }
        setKeyNotice(message);
        return;
      }
      setMessages(current => [...current, { id: nextId(), role: 'error', content: message, question, userMessageId: userId }]);
    } finally {
      inFlight.current = false;
      if (!request.signal.aborted) setPending(false);
    }
  }, [messages, apiKey, keySource, reloadSavedKey, setMessages, accountId, t]);

  const submit = (event?: FormEvent) => {
    event?.preventDefault();
    void send(draft);
  };

  const saveKey = async (event: FormEvent) => {
    event.preventDefault();
    // A second press while the first is still being saved does nothing.
    if (saving.current) return;
    const problem = assistantKeyProblem(keyDraft);
    if (problem) {
      setKeyNotice(problem);
      return;
    }
    const key = cleanAssistantKey(keyDraft);
    setKeyInfo('');
    if (keyStorage === 'account') {
      saving.current = true;
      setSavingKey(true);
      try {
        await savedKey.save(key);
      } catch (error) {
        // Nothing is lost: the key stays in the box, so it can be tried again or kept somewhere else.
        setKeyNotice(assistantKeyRequestError(error, 'save'));
        return;
      } finally {
        saving.current = false;
        setSavingKey(false);
      }
      // The server holds the key now, so this page keeps no copy: the one typed here is dropped, also from this tab's storage.
      setApiKey('');
      forgetAssistantKey(getBrowserStorage(), accountId);
      setKeyRemembered(false);
      setSavedRefused(false);
      setKeyInfo(t('showcaseAssistant.savedToAccount'));
    } else {
      setApiKey(key);
      // Keeping the key in this tab is the user's choice. Without it, any copy kept earlier is dropped so the choice always means what it says.
      if (keyStorage === 'tab') setKeyRemembered(rememberAssistantKey(getBrowserStorage(), accountId, key));
      else {
        forgetAssistantKey(getBrowserStorage(), accountId);
        setKeyRemembered(false);
      }
    }
    setKeyDraft('');
    setKeyStorage('session');
    setKeyNotice('');
    setChangingKey(false);
  };

  /** Lets go of the key kept on this page or in this tab (the one saved to the account is only removed by its own button). */
  const forgetTypedKey = () => {
    controller.current?.abort();
    inFlight.current = false;
    setPending(false);
    setApiKey('');
    forgetAssistantKey(getBrowserStorage(), accountId);
    setKeyRemembered(false);
    setKeyNotice('');
    setKeyInfo('');
  };

  /** Removes the key saved to the account. Resolves true once the server has removed it. */
  const removeSavedKey = async (): Promise<boolean> => {
    setKeyNotice('');
    try {
      await savedKey.remove();
    } catch (error) {
      setKeyNotice(assistantKeyRequestError(error, 'remove'));
      return false;
    }
    setSavedRefused(false);
    setKeyInfo(t('showcaseAssistant.savedKeyRemoved'));
    return true;
  };

  const changeKey = () => {
    setChangingKey(true);
    setKeyNotice('');
    setKeyInfo('');
  };

  const cancelChange = () => {
    setChangingKey(false);
    setKeyDraft('');
    setKeyStorage('session');
    setKeyNotice('');
    setKeyInfo('');
  };

  const clearConversation = () => {
    controller.current?.abort();
    inFlight.current = false;
    setPending(false);
    setMessages(() => []);
    setConfirmClear(false);
  };

  const onPanelKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key !== 'Escape') return;
    // Escape first backs out of the "delete conversation" question, and only then closes the panel.
    if (confirmClear) setConfirmClear(false);
    else setOpen(false);
  };

  const last = messages[messages.length - 1];
  const suggestions = last?.role === 'assistant' ? last.suggestions : [];

  const keyFormProps: KeyFormProps = {
    draft: keyDraft,
    notice: keyNotice,
    info: keyInfo,
    lookupError: savedKey.phase === 'error' ? savedKey.error : '',
    inputRef: keyInputRef,
    storage: keyStorage,
    accountAvailable: savedKey.status?.storageAvailable === true,
    saving: savingKey,
    changing: changingKey,
    current: (
      <CurrentKeys
        typedLast4={apiKey ? assistantKeyLast4(apiKey) : ''}
        typedRemembered={keyRemembered}
        savedLast4={saved ? (saved.last4 ?? '') : null}
        savedRefused={savedRefused}
        onForgetTyped={forgetTypedKey}
        onRemoveSaved={removeSavedKey}
      />
    ),
    onDraft: value => { setKeyDraft(value); if (keyNotice) setKeyNotice(''); },
    onStorage: setKeyStorage,
    onSubmit: event => { void saveKey(event); },
    onCancel: cancelChange,
    onRetryLookup: reloadSavedKey,
  };
  const keyButtonLabel = keySource === 'saved'
    ? t('showcaseAssistant.savedKeyButton', { last4: saved?.last4 ?? '' })
    : t(keyRemembered ? 'showcaseAssistant.changeRememberedKey' : 'showcaseAssistant.doiApiKey');

  if (!open) {
    return (
      <div className="fixed bottom-3 right-3 z-50 flex flex-col items-end gap-2 sm:bottom-5 sm:right-5">
        {greeting && (
          <div role="status" className="relative max-w-[15rem] animate-scale-in rounded-2xl rounded-br-md border border-slate-200 bg-white py-2.5 pl-3.5 pr-9 text-body-sm text-slate-700 shadow-float dark:border-slate-600 dark:bg-dark-surface dark:text-slate-200">
            {t('showcaseAssistant.chaoBanMinhLa')} <strong className="font-semibold text-secondary dark:text-white">{t('showcaseAssistant.hubby')}</strong>{t('showcaseAssistant.canTimDuAnNaoTren')}
            <button type="button" onClick={() => setGreeting(false)} aria-label={t('showcaseAssistant.anLoiChao')} className="absolute right-1 top-1 flex h-9 w-9 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:hover:bg-slate-800 dark:hover:text-white">
              <X className="h-4 w-4" aria-hidden="true" />
            </button>
          </div>
        )}
        <button
          ref={launcherRef}
          type="button"
          onClick={() => { setGreeting(false); setOpen(true); }}
          aria-label={t('showcaseAssistant.hoiHubbyTroLyShowcase')}
          className="group flex items-center rounded-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600 focus-visible:ring-offset-2"
        >
          <span className="rounded-full border border-slate-200 bg-white py-2 pl-4 pr-9 text-body-sm font-semibold text-secondary shadow-float transition-colors group-hover:border-primary dark:border-slate-600 dark:bg-dark-surface dark:text-white">{t('showcaseAssistant.hoiHubby')}</span>
          <span className="-ml-7 flex h-16 w-16 items-center justify-center rounded-full border-2 border-white bg-primary-50 shadow-float transition-transform duration-200 group-hover:scale-110 group-active:scale-95 dark:border-slate-600 dark:bg-secondary-800">
            <HubbyMascot className="hubby-float h-12 w-auto transition-transform duration-200 group-hover:-rotate-6" />
          </span>
        </button>
      </div>
    );
  }

  return (
    <div
      role="dialog"
      aria-labelledby={titleId}
      onKeyDown={onPanelKeyDown}
      className="fixed inset-x-3 bottom-3 z-50 flex h-[min(34rem,calc(100dvh-1.5rem))] flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-float sm:inset-x-auto sm:bottom-6 sm:right-6 sm:w-[26rem] dark:border-slate-600 dark:bg-dark-surface"
    >
      <header className="flex items-center justify-between gap-3 border-b border-slate-200 px-4 py-3 dark:border-slate-600">
        <div className="flex min-w-0 items-center gap-2.5">
          <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-primary-50 dark:bg-secondary-800"><HubbyMascot mood={pending ? 'thinking' : 'idle'} className="h-9 w-auto" /></span>
          <div className="min-w-0">
            <h2 id={titleId} className="text-subheading font-bold text-secondary dark:text-white">{t('showcaseAssistant.hubbyTroLyShowcase')}</h2>
            <p className="text-caption text-slate-500 dark:text-slate-400">{keySource !== 'none' || checkingSaved ? t('showcaseAssistant.hoiVeCacDuAnDa') : t('showcaseAssistant.canApiKeyGeminiCuaBan')}</p>
          </div>
        </div>
        <div className="flex shrink-0 items-center gap-1">
          {messages.length > 0 && (
            <button
              type="button"
              onClick={() => setConfirmClear(true)}
              aria-label={t('showcaseAssistant.clearConversation')}
              title={t('showcaseAssistant.clearConversation')}
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:text-slate-300 dark:hover:bg-slate-800 dark:hover:text-white"
            >
              <Trash2 className="h-4 w-4" aria-hidden="true" />
            </button>
          )}
          <button type="button" onClick={() => setOpen(false)} aria-label={t('showcaseAssistant.dongTroLy')} className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:text-slate-300 dark:hover:bg-slate-800 dark:hover:text-white">
            <X className="h-5 w-5" aria-hidden="true" />
          </button>
        </div>
      </header>

      {confirmClear && (
        <div role="group" aria-label={t('showcaseAssistant.confirmClearConversation')} className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-200 bg-slate-50 px-4 py-2 text-body-sm text-slate-700 dark:border-slate-600 dark:bg-secondary-800 dark:text-slate-200">
          <span>{t('showcaseAssistant.clearConversationPrompt')}</span>
          <span className="flex items-center gap-2">
            <Button size="sm" variant="ghost" autoFocus onClick={() => setConfirmClear(false)}>{t('showcaseAssistant.keepConversation')}</Button>
            <Button size="sm" variant="danger-outline" icon={Trash2} onClick={clearConversation}>{t('showcaseAssistant.deleteConversation')}</Button>
          </span>
        </div>
      )}

      {messages.length === 0 && (checkingSaved || askForKey) ? (
        checkingSaved ? <CheckingSavedKey compact={false} /> : <KeyForm {...keyFormProps} />
      ) : (
      <>
      <div ref={listRef} role="log" aria-live="polite" aria-label={t('showcaseAssistant.cuocTroChuyen')} className="flex-1 space-y-3 overflow-y-auto px-4 py-4">
        {messages.length === 0 && (
          <div className="space-y-3">
            <div className="flex flex-col items-center gap-2 pb-1 text-center"><HubbyMascot className="hubby-float h-24 w-auto" /></div>
            <p className="text-body-sm text-slate-600 dark:text-slate-300">{t('showcaseAssistant.xinChaoMinhLaHubbyMinh')}</p>
            <ul className="flex flex-wrap gap-2" aria-label={t('showcaseAssistant.cauHoiGoiY')}>
              {ASSISTANT_STARTER_QUESTIONS.map(question => (
                <li key={question}><button type="button" className={chipClass} disabled={pending} onClick={() => void send(question)}>{question}</button></li>
              ))}
            </ul>
          </div>
        )}
        {messages.map(message => {
          if (message.role === 'user') {
            return <div key={message.id} className="flex justify-end"><p className="max-w-[85%] whitespace-pre-wrap break-words rounded-2xl rounded-br-md bg-secondary px-3.5 py-2 text-body-sm text-white">{message.content}</p></div>;
          }
          if (message.role === 'error') {
            return (
              <div key={message.id} role="alert" className="space-y-2 rounded-xl border border-danger-light bg-danger-50 px-3.5 py-3 text-body-sm text-danger-dark">
                <p>{message.content}</p>
                <Button size="sm" variant="danger-outline" icon={RotateCcw} disabled={pending} onClick={() => void send(message.question, [message.id, message.userMessageId])}>{t('showcaseAssistant.thuLai')}</Button>
              </div>
            );
          }
          return (
            <div key={message.id} className="space-y-2">
              <p className="max-w-[92%] whitespace-pre-wrap break-words rounded-2xl rounded-bl-md bg-slate-100 px-3.5 py-2 text-body-sm text-slate-800 dark:bg-secondary-800 dark:text-slate-100">{message.content}</p>
              {message.projects.length > 0 && (
                <ul aria-label={t('showcaseAssistant.duAnGoiY')} className="space-y-2">
                  {message.projects.map(project => <ProjectResult key={project.id} project={project} backTo={backTo} />)}
                </ul>
              )}
            </div>
          );
        })}
        {pending && (
          <div role="status" className="flex items-center gap-2">
            <HubbyMascot mood="thinking" className="h-9 w-auto" />
            <p className="rounded-2xl rounded-bl-md bg-slate-100 px-3.5 py-2 text-body-sm text-slate-600 dark:bg-secondary-800 dark:text-slate-300">{t('showcaseAssistant.hubbyDangTimDuAnPhu')}</p>
          </div>
        )}
        {suggestions.length > 0 && !pending && (
          <ul className="flex flex-wrap gap-2" aria-label={t('showcaseAssistant.cauHoiTiepTheo')}>
            {suggestions.map(question => <li key={question}><button type="button" className={chipClass} onClick={() => void send(question)}>{question}</button></li>)}
          </ul>
        )}
      </div>

      {checkingSaved ? (
        <CheckingSavedKey compact />
      ) : askForKey ? (
        <ResumeKeyForm {...keyFormProps} />
      ) : (
      <form onSubmit={submit} className="border-t border-slate-200 p-3 dark:border-slate-600">
        {keyNotice && <p role="alert" className="mb-2 text-body-sm text-danger-dark">{keyNotice}</p>}
        {keyInfo && !keyNotice && <p role="status" className="mb-2 rounded-lg border border-success-light bg-success-50 px-3 py-2 text-body-sm text-success-dark">{keyInfo}</p>}
        <div className="flex items-end gap-2">
          <div className="min-w-0 flex-1">
            <label htmlFor={`${titleId}-input`} className="sr-only">{t('showcaseAssistant.cauHoiCuaBan')}</label>
            <AutoSizeTextarea
              id={`${titleId}-input`}
              ref={inputRef}
              value={draft}
              maxLength={ASSISTANT_MAX_MESSAGE_LENGTH}
              maxRows={4}
              placeholder={t('showcaseAssistant.viDuCoDuAnNao')}
              onChange={event => setDraft(event.target.value)}
              submitOnEnter={event => { event.preventDefault(); submit(); }}
              className="w-full resize-none rounded-lg border border-slate-300 bg-white px-3 py-2 text-body-sm text-slate-900 placeholder:text-slate-400 hover:border-slate-400 focus:border-primary-600 focus:outline-none focus:ring-2 focus:ring-primary/30 dark:border-slate-600 dark:bg-dark-surface dark:text-slate-100"
            />
          </div>
          <Button type="submit" size="md" icon={Send} isLoading={pending} disabled={!draft.trim()} aria-label={t('showcaseAssistant.guiCauHoi')} className="shrink-0 px-3" />
        </div>
        <p className="mt-1.5 flex flex-wrap items-center justify-between gap-x-3 text-caption text-slate-500 dark:text-slate-400">
          <span>{t('showcaseAssistant.troLyAiCoTheNham')}</span>
          <button
            type="button"
            onClick={changeKey}
            className="inline-flex min-h-9 items-center gap-1 rounded-md px-1 font-semibold text-primary-700 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:text-primary-300"
          >
            <KeyRound className="h-3.5 w-3.5" aria-hidden="true" />{keyButtonLabel}
          </button>
        </p>
      </form>
      )}
      </>
      )}
    </div>
  );
}
