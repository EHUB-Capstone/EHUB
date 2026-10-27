import { forwardRef, useCallback, useLayoutEffect, useRef, type KeyboardEvent, type TextareaHTMLAttributes } from 'react';
import { cn } from '../../utils/cn';
import { autoSizeHeight, shouldSubmitOnEnter } from '../../utils/autoSizeTextarea';

interface AutoSizeTextareaProps extends Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, 'rows'> {
  minRows?: number;
  maxRows?: number;
  // When set, Enter calls it and Shift+Enter inserts a new line (chat style).
  submitOnEnter?: (event: KeyboardEvent<HTMLTextAreaElement>) => void;
}

// Textarea that grows with its content (like chat inputs) and only scrolls once it reaches maxRows.
const AutoSizeTextarea = forwardRef<HTMLTextAreaElement, AutoSizeTextareaProps>(({
  minRows = 1, maxRows = 8, submitOnEnter, className, onKeyDown, value, ...props
}, forwardedRef) => {
  const innerRef = useRef<HTMLTextAreaElement | null>(null);

  const setRefs = useCallback((element: HTMLTextAreaElement | null) => {
    innerRef.current = element;
    if (typeof forwardedRef === 'function') forwardedRef(element);
    else if (forwardedRef) forwardedRef.current = element;
  }, [forwardedRef]);

  const resize = useCallback(() => {
    const element = innerRef.current;
    if (!element) return;
    const style = window.getComputedStyle(element);
    const fontSize = parseFloat(style.fontSize) || 14;
    const lineHeight = parseFloat(style.lineHeight) || fontSize * 1.5;
    element.style.height = 'auto';
    const { height, overflowY } = autoSizeHeight({
      scrollHeight: element.scrollHeight,
      lineHeight,
      verticalPadding: (parseFloat(style.paddingTop) || 0) + (parseFloat(style.paddingBottom) || 0),
      verticalBorder: (parseFloat(style.borderTopWidth) || 0) + (parseFloat(style.borderBottomWidth) || 0),
      minRows,
      maxRows,
    });
    element.style.height = `${height}px`;
    element.style.overflowY = overflowY;
  }, [maxRows, minRows]);

  useLayoutEffect(() => {
    resize();
  }, [resize, value]);

  // Line wrapping changes when the width changes (panel opened or closed, window resized).
  useLayoutEffect(() => {
    const element = innerRef.current;
    if (!element || typeof ResizeObserver === 'undefined') return undefined;
    let lastWidth = element.clientWidth;
    const observer = new ResizeObserver(() => {
      if (element.clientWidth === lastWidth) return;
      lastWidth = element.clientWidth;
      resize();
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, [resize]);

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    onKeyDown?.(event);
    if (event.defaultPrevented || !submitOnEnter) return;
    if (shouldSubmitOnEnter({ key: event.key, shiftKey: event.shiftKey, isComposing: event.nativeEvent.isComposing })) {
      event.preventDefault();
      submitOnEnter(event);
    }
  };

  return (
    <textarea
      ref={setRefs}
      rows={minRows}
      value={value}
      onKeyDown={handleKeyDown}
      className={cn('resize-none', className)}
      {...props}
    />
  );
});

AutoSizeTextarea.displayName = 'AutoSizeTextarea';
export default AutoSizeTextarea;
