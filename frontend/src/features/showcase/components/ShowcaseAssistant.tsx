import { useCallback, useEffect, useId, useRef, useState, type FormEvent, type KeyboardEvent } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { ExternalLink, Eye, KeyRound, RotateCcw, Send, ThumbsUp, X } from 'lucide-react';
import { showcaseApi } from '../../../api/showcaseApi';
import AutoSizeTextarea from '../../../components/ui/AutoSizeTextarea';
import Button from '../../../components/ui/Button';
import { badgeTones } from '../../../components/ui/Badge';
import HubbyMascot from './HubbyMascot';
import type { ShowcaseAssistantProjectDto, ShowcaseStage } from '../../../types/showcase';
import { parseApiError } from '../../../utils/apiError';
import { cn } from '../../../utils/cn';
import { stageLabel } from '../../../utils/showcase';
import {
  ASSISTANT_KEY_URL,
  ASSISTANT_MAX_MESSAGE_LENGTH,
  ASSISTANT_STARTER_QUESTIONS,
  assistantErrorMessage,
  assistantKeyProblem,
  buildAssistantHistory,
  cleanAssistantKey,
  isAssistantKeyError,
  type AssistantChatMessage,
} from '../../../utils/showcaseAssistant';
import { useTranslation } from 'react-i18next';

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
            <span className="inline-flex items-center gap-1"><ThumbsUp className="h-3.5 w-3.5" aria-hidden="true" />{project.likeCount.toLocaleString()}<span className="sr-only"> {t('showcaseAssistant.luotThich')}</span></span>
            <span className="inline-flex items-center gap-1"><Eye className="h-3.5 w-3.5" aria-hidden="true" />{project.viewCount.toLocaleString()}<span className="sr-only"> {t('showcaseAssistant.luotXem')}</span></span>
          </div>
        </div>
      </div>
    </li>
  );
}

/**
 * The assistant runs on the user's own Gemini key: the server keeps none. This is where it is pasted. The key is checked for
 * shape only (nothing is sent anywhere to test it); Google's answer to the first question tells whether it works.
 */
function KeyForm({ draft, notice, inputRef, onDraft, onSubmit }: {
  draft: string;
  notice: string;
  inputRef: React.Ref<HTMLInputElement>;
  onDraft: (value: string) => void;
  onSubmit: (event: FormEvent) => void;
}) {
  const { t } = useTranslation('showcase');
  const id = useId();
  return (
    <form onSubmit={onSubmit} aria-label={t('showcaseAssistant.nhapApiKeyGemini')} className="flex-1 space-y-3 overflow-y-auto px-4 py-4">
      <div className="flex flex-col items-center gap-2 text-center"><HubbyMascot className="hubby-float h-20 w-auto" /></div>
      <h3 className="text-body font-semibold text-secondary dark:text-white">{t('showcaseAssistant.nhapApiKeyGeminiCuaBan')}</h3>
      <p className="text-body-sm text-slate-600 dark:text-slate-300">
        {t('showcaseAssistant.hubbyChayBangKeyCuaChinh')}
      </p>
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
      <div>
        <label htmlFor={`${id}-key`} className="text-caption font-semibold text-slate-700 dark:text-slate-200">{t('showcaseAssistant.apiKeyGemini')}</label>
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
      <Button type="submit" size="md" icon={KeyRound} disabled={!draft.trim()} className="w-full">{t('showcaseAssistant.luuKeyVaBatDau')}</Button>
    </form>
  );
}

export default function ShowcaseAssistant(): React.ReactElement {
  const { t } = useTranslation('showcase');
  const location = useLocation();
  const backTo = `${location.pathname}${location.search}`;
  const titleId = useId();
  const [open, setOpen] = useState(false);
  const [messages, setMessages] = useState<AssistantChatMessage[]>([]);
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
  // The user's own Gemini key. It lives only in this component's memory: reloading the page or leaving Showcase forgets it. It is
  // never written to storage, and the server keeps none.
  const [apiKey, setApiKey] = useState('');
  const [keyDraft, setKeyDraft] = useState('');
  const [keyNotice, setKeyNotice] = useState('');
  const keyInputRef = useRef<HTMLInputElement | null>(null);

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

  useEffect(() => {
    // With no key yet the first thing to do is paste one, so focus goes there; afterwards it goes to the question box.
    if (open) (apiKey ? inputRef : keyInputRef).current?.focus();
    else {
      if (wasOpen.current) launcherRef.current?.focus();
      // A half-typed key does not wait around in memory once the panel is closed.
      setKeyDraft('');
    }
    wasOpen.current = open;
  }, [open, apiKey]);

  useEffect(() => {
    const list = listRef.current;
    if (list) list.scrollTop = list.scrollHeight;
  }, [messages, pending, open]);

  const nextId = () => `m${++counter.current}`;

  const send = useCallback(async (rawQuestion: string, replace: readonly string[] = []) => {
    const question = rawQuestion.trim();
    // inFlight is a ref so two quick clicks cannot both pass before React re-renders the disabled state.
    if (!question || question.length > ASSISTANT_MAX_MESSAGE_LENGTH || inFlight.current) return;
    if (!apiKey) {
      setKeyNotice(t('showcaseAssistant.hayNhapApiKeyGeminiCua'));
      return;
    }
    inFlight.current = true;
    const userId = nextId();
    const history = buildAssistantHistory(messages, replace);
    setMessages(current => [...current.filter(message => !replace.includes(message.id)), { id: userId, role: 'user', content: question }]);
    setDraft('');
    setPending(true);
    const request = new AbortController();
    controller.current = request;
    try {
      const reply = await showcaseApi.askAssistant({ message: question, history }, apiKey, request.signal);
      if (request.signal.aborted) return;
      setMessages(current => [...current, {
        id: nextId(), role: 'assistant', content: reply.answer, projects: reply.projects, suggestions: reply.suggestions,
      }]);
    } catch (error) {
      if (request.signal.aborted) return;
      const message = assistantErrorMessage(error, parseApiError(error, '').message || undefined);
      if (isAssistantKeyError(error)) {
        // The key is the problem, not the question: forget it, hand the question back and ask for a key that works.
        setMessages(current => current.filter(item => item.id !== userId));
        setDraft(question);
        setApiKey('');
        setKeyNotice(message);
        return;
      }
      setMessages(current => [...current, { id: nextId(), role: 'error', content: message, question, userMessageId: userId }]);
    } finally {
      inFlight.current = false;
      if (!request.signal.aborted) setPending(false);
    }
  }, [messages, apiKey]);

  const submit = (event?: FormEvent) => {
    event?.preventDefault();
    void send(draft);
  };

  const saveKey = (event: FormEvent) => {
    event.preventDefault();
    const problem = assistantKeyProblem(keyDraft);
    if (problem) {
      setKeyNotice(problem);
      return;
    }
    setApiKey(cleanAssistantKey(keyDraft));
    setKeyDraft('');
    setKeyNotice('');
  };

  const forgetKey = () => {
    controller.current?.abort();
    inFlight.current = false;
    setPending(false);
    setApiKey('');
    setKeyDraft('');
    setKeyNotice('');
  };

  const onPanelKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') setOpen(false);
  };

  const last = messages[messages.length - 1];
  const suggestions = last?.role === 'assistant' ? last.suggestions : [];

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
            <p className="text-caption text-slate-500 dark:text-slate-400">{apiKey ? t('showcaseAssistant.hoiVeCacDuAnDa') : t('showcaseAssistant.canApiKeyGeminiCuaBan')}</p>
          </div>
        </div>
        <button type="button" onClick={() => setOpen(false)} aria-label={t('showcaseAssistant.dongTroLy')} className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:text-slate-300 dark:hover:bg-slate-800 dark:hover:text-white">
          <X className="h-5 w-5" aria-hidden="true" />
        </button>
      </header>

      {!apiKey ? (
        <KeyForm draft={keyDraft} notice={keyNotice} inputRef={keyInputRef} onDraft={value => { setKeyDraft(value); if (keyNotice) setKeyNotice(''); }} onSubmit={saveKey} />
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

      <form onSubmit={submit} className="border-t border-slate-200 p-3 dark:border-slate-600">
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
            onClick={forgetKey}
            className="inline-flex min-h-9 items-center gap-1 rounded-md px-1 font-semibold text-primary-700 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:text-primary-300"
          >
            <KeyRound className="h-3.5 w-3.5" aria-hidden="true" />{t('showcaseAssistant.doiApiKey')}
          </button>
        </p>
      </form>
      </>
      )}
    </div>
  );
}
