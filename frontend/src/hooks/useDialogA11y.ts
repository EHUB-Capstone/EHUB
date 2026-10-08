import { useEffect, useRef, type RefObject } from 'react';
import { nextFocusIndex } from '../utils/mentorSlotBoard';

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

interface DialogA11yOptions {
  onClose: () => void;
  /** Set to false while another dialog is open on top, so only the top dialog reacts to Esc and Tab. */
  enabled?: boolean;
  /** Set to true while saving: Esc must not close the dialog then. */
  busy?: boolean;
}

/**
 * Keyboard behaviour of a modal dialog: Esc closes it, Tab stays inside it, focus moves in when it opens
 * and returns to the control that opened it when it closes.
 */
export function useDialogA11y(ref: RefObject<HTMLElement | null>, { onClose, enabled = true, busy = false }: DialogA11yOptions) {
  const closeRef = useRef(onClose);
  const busyRef = useRef(busy);
  closeRef.current = onClose;
  busyRef.current = busy;

  useEffect(() => {
    const opener = document.activeElement as HTMLElement | null;
    const container = ref.current;
    if (container && !container.contains(document.activeElement)) {
      (container.querySelector<HTMLElement>(FOCUSABLE) ?? container).focus({ preventScroll: true });
    }
    return () => {
      if (opener && document.contains(opener)) opener.focus({ preventScroll: true });
    };
  }, [ref]);

  useEffect(() => {
    if (!enabled) return undefined;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        if (!busyRef.current) {
          event.stopPropagation();
          closeRef.current();
        }
        return;
      }
      if (event.key !== 'Tab') return;
      const container = ref.current;
      if (!container) return;
      const items = Array.from(container.querySelectorAll<HTMLElement>(FOCUSABLE)).filter(item => item.offsetParent !== null);
      if (items.length === 0) {
        event.preventDefault();
        container.focus();
        return;
      }
      const current = items.indexOf(document.activeElement as HTMLElement);
      const edge = event.shiftKey ? current <= 0 : current === items.length - 1;
      if (current === -1 || edge) {
        event.preventDefault();
        items[nextFocusIndex(current, items.length, event.shiftKey)].focus();
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [enabled, ref]);
}
