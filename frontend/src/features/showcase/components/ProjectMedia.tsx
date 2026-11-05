import { useEffect, useRef, useState } from 'react';
import { Play } from 'lucide-react';
import { VIDEO_IFRAME_SANDBOX, showcaseVideoEmbedUrl, type ShowcaseVideo } from '../../../utils/showcaseVideo';
import { useFeedVideoLeader } from '../hooks/useFeedVideoLeader';

const prefersReducedMotion = () => typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
/** A post must stay the most visible one this long before its video loads, so scrolling past never starts a download. */
const SCROLL_SETTLE_MS = 600;

interface ProjectMediaProps {
  id: string;
  name: string;
  video: ShowcaseVideo | null;
  coverUrl?: string | null;
  /** True while the pointer or keyboard focus is on the card: the video then plays at once. */
  active: boolean;
}

/**
 * The image area of a feed post. A video wins over a cover image and plays muted when the viewer hovers or focuses the
 * post, or when the post has settled as the one mostly on screen while scrolling. It stops as soon as the post scrolls
 * away, and at most one video plays at a time. With neither video nor image nothing is rendered.
 */
export default function ProjectMedia({ id, name, video, coverUrl, active }: ProjectMediaProps): React.ReactElement | null {
  const area = useRef<HTMLDivElement>(null);
  const isLeader = useFeedVideoLeader(id, area, video !== null);
  const [settled, setSettled] = useState(false);

  useEffect(() => {
    if (!isLeader) return undefined;
    const timer = window.setTimeout(() => setSettled(true), SCROLL_SETTLE_MS);
    return () => { window.clearTimeout(timer); setSettled(false); };
  }, [isLeader]);

  if (!video && !coverUrl) return null;
  const playing = video !== null && (active || (isLeader && settled)) && !prefersReducedMotion();

  return (
    <div ref={area} className="relative mt-3 aspect-video w-full overflow-hidden bg-secondary" aria-hidden={video ? undefined : true}>
      {coverUrl && <img src={coverUrl} alt="" loading="lazy" className="absolute inset-0 h-full w-full object-cover" />}
      {video && playing && (
        <iframe
          src={showcaseVideoEmbedUrl(video, 'preview')}
          title={`${name} video preview`}
          sandbox={VIDEO_IFRAME_SANDBOX}
          allow="autoplay; encrypted-media"
          referrerPolicy="strict-origin-when-cross-origin"
          tabIndex={-1}
          className="pointer-events-none absolute inset-0 h-full w-full border-0"
        />
      )}
      {video && !playing && (
        <span className="absolute inset-0 flex items-center justify-center" aria-hidden="true">
          <span className="flex h-14 w-14 items-center justify-center rounded-full bg-white/90 text-primary-700 shadow-elevated"><Play className="h-6 w-6 translate-x-0.5 fill-current" /></span>
        </span>
      )}
    </div>
  );
}
