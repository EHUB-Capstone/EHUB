/**
 * The only video providers a showcase may point to. This mirrors ShowcaseVideoLinks on the server: a link is reduced to a
 * provider and a video id, and every embed URL is built from that id alone, so nothing else in a student-supplied link
 * ever reaches an iframe.
 */
export interface ShowcaseVideo {
  provider: 'youtube' | 'vimeo';
  id: string;
  /** Canonical watch URL; the same value the server returns on a project card. */
  watchUrl: string;
}

const YOUTUBE_ID = /^[A-Za-z0-9_-]{11}$/;
const VIMEO_ID = /^[0-9]{1,12}$/;

export function parseShowcaseVideo(rawUrl: string | null | undefined): ShowcaseVideo | null {
  if (!rawUrl) return null;
  let url: URL;
  try { url = new URL(rawUrl.trim()); } catch { return null; }
  if (url.protocol !== 'https:' || url.port !== '' || url.username !== '' || url.password !== '') return null;

  const host = url.hostname.toLowerCase().replace(/^www\./, '');
  const segments = url.pathname.split('/').filter(Boolean);

  let youtubeId: string | null = null;
  if (host === 'youtu.be' && segments.length === 1) youtubeId = segments[0];
  else if ((host === 'youtube.com' || host === 'm.youtube.com') && segments.length === 1 && segments[0] === 'watch') youtubeId = url.searchParams.get('v');
  else if ((host === 'youtube.com' || host === 'm.youtube.com') && segments.length === 2 && (segments[0] === 'shorts' || segments[0] === 'embed')) youtubeId = segments[1];
  if (youtubeId && YOUTUBE_ID.test(youtubeId)) return { provider: 'youtube', id: youtubeId, watchUrl: `https://www.youtube.com/watch?v=${youtubeId}` };

  if (host === 'vimeo.com' && segments.length === 1 && VIMEO_ID.test(segments[0])) return { provider: 'vimeo', id: segments[0], watchUrl: `https://vimeo.com/${segments[0]}` };
  return null;
}

export const isSupportedShowcaseVideo = (rawUrl: string | null | undefined): boolean => parseShowcaseVideo(rawUrl) !== null;

/** "preview" is a silent looping clip for the feed; "player" starts with sound and shows the provider's controls. */
export function showcaseVideoEmbedUrl(video: ShowcaseVideo, mode: 'preview' | 'player'): string {
  if (video.provider === 'youtube') {
    const base = `https://www.youtube-nocookie.com/embed/${video.id}?autoplay=1&playsinline=1&rel=0`;
    return mode === 'preview' ? `${base}&mute=1&controls=0&loop=1&playlist=${video.id}` : base;
  }
  const base = `https://player.vimeo.com/video/${video.id}?autoplay=1`;
  return mode === 'preview' ? `${base}&muted=1&loop=1&background=1` : base;
}

/** Players are third-party pages: keep them in a sandbox that still lets the video run. */
export const VIDEO_IFRAME_SANDBOX = 'allow-scripts allow-same-origin allow-presentation';
