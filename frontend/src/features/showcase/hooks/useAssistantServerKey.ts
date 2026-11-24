import { useCallback, useEffect, useRef, useState } from 'react';
import { showcaseApi } from '../../../api/showcaseApi';
import type { ShowcaseAssistantKeyStatusDto } from '../../../types/showcase';
import { assistantKeyRequestError } from '../../../utils/showcaseAssistant';

export type ServerKeyPhase = 'idle' | 'loading' | 'ready' | 'error';

interface ServerKeyState {
  /** The account this answer is about, so another account signing in on the same tab never sees it. */
  owner: string;
  phase: ServerKeyPhase;
  status: ShowcaseAssistantKeyStatusDto | null;
  /** Why the last look failed, in words for the user. */
  error: string;
  /** A look has finished at least once (well or badly), so a later retry does not send the screen back to "checking". */
  checked: boolean;
}

const idle = (owner: string): ServerKeyState => ({ owner, phase: 'idle', status: null, error: '', checked: false });

/**
 * What the server knows about the Gemini key this account saved: whether there is one and its last four characters, never the key
 * itself. It is looked up once, the first time the assistant is opened (`enabled`); saving or removing replace it with the
 * server's own answer.
 */
export function useAssistantServerKey(accountId: string, enabled: boolean) {
  const [state, setState] = useState<ServerKeyState>(() => idle(accountId));
  const current = state.owner === accountId ? state : idle(accountId);
  const request = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    if (!accountId) return;
    request.current?.abort();
    const controller = new AbortController();
    request.current = controller;
    setState(previous => {
      const same = previous.owner === accountId;
      return { owner: accountId, phase: 'loading', status: same ? previous.status : null, error: '', checked: same && previous.checked };
    });
    showcaseApi.getAssistantKey(controller.signal).then(
      status => {
        if (!controller.signal.aborted) setState({ owner: accountId, phase: 'ready', status, error: '', checked: true });
      },
      (error: unknown) => {
        if (controller.signal.aborted) return;
        setState(previous => ({
          owner: accountId, phase: 'error', status: previous.owner === accountId ? previous.status : null,
          error: assistantKeyRequestError(error, 'check'), checked: true,
        }));
      },
    );
  }, [accountId]);

  useEffect(() => {
    if (enabled && accountId && current.phase === 'idle') load();
  }, [enabled, accountId, current.phase, load]);

  // A look that is still running when the account changes or the assistant goes away is cancelled.
  useEffect(() => () => request.current?.abort(), [accountId]);

  const apply = useCallback((status: ShowcaseAssistantKeyStatusDto) => {
    // An older look that is still on its way must not overwrite what the server just answered to a save or a removal.
    request.current?.abort();
    setState({ owner: accountId, phase: 'ready', status, error: '', checked: true });
    return status;
  }, [accountId]);

  /** Saves the key to the account (the server encrypts it). Rejects when it could not be saved. */
  const save = useCallback(async (apiKey: string) => apply(await showcaseApi.saveAssistantKey(apiKey)), [apply]);
  /** Removes the saved key. Rejects when it could not be removed. */
  const remove = useCallback(async () => apply(await showcaseApi.deleteAssistantKey()), [apply]);

  return { phase: current.phase, status: current.status, error: current.error, checked: current.checked, reload: load, save, remove };
}
