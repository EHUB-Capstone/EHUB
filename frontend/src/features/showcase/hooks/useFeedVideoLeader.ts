import { useEffect, useSyncExternalStore, type RefObject } from 'react';

/**
 * Decides which feed post may auto-play its video: the one that is mostly on screen (at least MIN_RATIO visible), and
 * only one at a time. One shared IntersectionObserver serves every post, so a long feed costs a single observer.
 */
const MIN_RATIO = 0.6;
const TIE_MARGIN = 0.05;

interface Entry { id: string; ratio: number; top: number }

const entries = new Map<Element, Entry>();
const listeners = new Set<() => void>();
let observer: IntersectionObserver | null = null;
let leader: string | null = null;

function recompute() {
  let best: Entry | null = null;
  for (const entry of entries.values()) {
    if (entry.ratio < MIN_RATIO) continue;
    // The clearly most visible post wins; on a near tie the one nearer the top does.
    if (!best || entry.ratio > best.ratio + TIE_MARGIN || (Math.abs(entry.ratio - best.ratio) <= TIE_MARGIN && entry.top < best.top)) best = entry;
  }
  const next = best?.id ?? null;
  if (next === leader) return;
  leader = next;
  listeners.forEach(listener => listener());
}

function sharedObserver(): IntersectionObserver | null {
  if (typeof IntersectionObserver === 'undefined') return null;
  observer ??= new IntersectionObserver(changes => {
    for (const change of changes) {
      const entry = entries.get(change.target);
      if (!entry) continue;
      entry.ratio = change.isIntersecting ? change.intersectionRatio : 0;
      entry.top = change.boundingClientRect.top;
    }
    recompute();
  }, { threshold: [0, 0.25, 0.5, 0.6, 0.75, 1] });
  return observer;
}

const subscribe = (listener: () => void) => { listeners.add(listener); return () => { listeners.delete(listener); }; };

/** True while this post is the one that should be playing. Pass enabled=false for posts without a video. */
export function useFeedVideoLeader(id: string, target: RefObject<Element | null>, enabled: boolean): boolean {
  useEffect(() => {
    const element = target.current;
    const shared = sharedObserver();
    if (!enabled || !element || !shared) return undefined;
    entries.set(element, { id, ratio: 0, top: 0 });
    shared.observe(element);
    return () => {
      shared.unobserve(element);
      entries.delete(element);
      recompute();
    };
  }, [id, target, enabled]);
  return useSyncExternalStore(subscribe, () => leader === id, () => false);
}
