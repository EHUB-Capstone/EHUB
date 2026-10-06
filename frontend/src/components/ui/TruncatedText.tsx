import { useCallback, useEffect, useRef, useState } from 'react';
import type { CSSProperties, ElementType, HTMLAttributes, ReactNode } from 'react';
import { createPortal } from 'react-dom';

interface TruncatedTextProps extends Omit<HTMLAttributes<HTMLElement>, 'children'> {
  text: string;
  lines?: number;
  as?: ElementType;
  children?: ReactNode;
}

interface TooltipPosition {
  left: number;
  top?: number;
  bottom?: number;
}

const TOOLTIP_WIDTH = 320;
const VIEWPORT_MARGIN = 8;
const TOOLTIP_GAP = 8;

/**
 * Clamps long text to `lines` lines with an ellipsis; when the text is actually cut off,
 * hovering shows the full content in a tooltip.
 */
export default function TruncatedText({
  text,
  lines = 2,
  as: Tag = 'p',
  className = '',
  children,
  ...rest
}: TruncatedTextProps) {
  const ref = useRef<HTMLElement>(null);
  const [position, setPosition] = useState<TooltipPosition | null>(null);

  const hide = useCallback(() => setPosition(null), []);

  const show = useCallback(() => {
    const element = ref.current;
    if (!element) return;
    const isCutOff = element.scrollHeight > element.clientHeight + 1 || element.scrollWidth > element.clientWidth + 1;
    if (!isCutOff) return;

    const rect = element.getBoundingClientRect();
    const left = Math.max(VIEWPORT_MARGIN, Math.min(rect.left, window.innerWidth - TOOLTIP_WIDTH - VIEWPORT_MARGIN));
    const roomBelow = window.innerHeight - rect.bottom;
    setPosition(roomBelow >= window.innerHeight * 0.3 || roomBelow >= rect.top
      ? { left, top: rect.bottom + TOOLTIP_GAP }
      : { left, bottom: window.innerHeight - rect.top + TOOLTIP_GAP });
  }, []);

  useEffect(() => {
    if (!position) return undefined;
    window.addEventListener('scroll', hide, true);
    window.addEventListener('resize', hide);
    return () => {
      window.removeEventListener('scroll', hide, true);
      window.removeEventListener('resize', hide);
    };
  }, [position, hide]);

  const clampStyle: CSSProperties = {
    display: '-webkit-box',
    WebkitLineClamp: lines,
    WebkitBoxOrient: 'vertical',
    overflow: 'hidden',
    overflowWrap: 'anywhere',
  };

  return (
    <>
      <Tag
        {...rest}
        ref={ref}
        className={className}
        style={clampStyle}
        onMouseEnter={show}
        onMouseLeave={hide}
        onPointerDown={hide}
      >
        {children ?? text}
      </Tag>
      {position && createPortal(
        <div
          role="tooltip"
          className="pointer-events-none fixed z-[100] max-h-[50vh] overflow-hidden whitespace-pre-wrap break-words rounded-lg bg-slate-900 px-3 py-2 text-left text-xs font-medium leading-5 text-white shadow-lg"
          style={{ width: TOOLTIP_WIDTH, maxWidth: `calc(100vw - ${VIEWPORT_MARGIN * 2}px)`, ...position }}
        >
          {text}
        </div>,
        document.body,
      )}
    </>
  );
}
