import assert from 'node:assert/strict';
import test from 'node:test';
import { VIDEO_IFRAME_SANDBOX, parseShowcaseVideo, showcaseVideoEmbedUrl } from '../src/utils/showcaseVideo.ts';
import { emptyLink, validateShowcaseForm, type ShowcaseFormValues } from '../src/utils/showcase.ts';

test('reduces supported YouTube and Vimeo links to a provider and id, matching the server', () => {
  const youtube = { provider: 'youtube', id: 'dQw4w9WgXcQ', watchUrl: 'https://www.youtube.com/watch?v=dQw4w9WgXcQ' };
  for (const url of [
    'https://youtu.be/dQw4w9WgXcQ',
    'https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=42s',
    'https://m.youtube.com/watch?v=dQw4w9WgXcQ',
    'https://youtube.com/shorts/dQw4w9WgXcQ',
    'https://www.youtube.com/embed/dQw4w9WgXcQ',
    '  https://youtu.be/dQw4w9WgXcQ  ',
  ]) assert.deepEqual(parseShowcaseVideo(url), youtube, url);
  assert.deepEqual(parseShowcaseVideo('https://vimeo.com/123456789'), { provider: 'vimeo', id: '123456789', watchUrl: 'https://vimeo.com/123456789' });
});

test('rejects other hosts, plain http, credentials, odd ports and malformed ids', () => {
  for (const url of [
    '', 'not a url', 'javascript:alert(1)', 'http://youtu.be/dQw4w9WgXcQ', 'https://user:pass@youtu.be/dQw4w9WgXcQ',
    'https://youtu.be.evil.example/dQw4w9WgXcQ', 'https://evil.example/youtube.com/watch?v=dQw4w9WgXcQ',
    'https://youtu.be:8443/dQw4w9WgXcQ', 'https://www.youtube.com/watch?v=short', 'https://www.youtube.com/watch?v=dQw4w9WgXcQ%22onload%3Dx',
    'https://www.youtube.com/playlist?list=PL12345678901', 'https://vimeo.com/channels/staffpicks', 'https://example.test/video.mp4',
  ]) assert.equal(parseShowcaseVideo(url), null, url);
  assert.equal(parseShowcaseVideo(null), null);
});

test('builds embed URLs from the id only and keeps the player sandboxed', () => {
  const youtube = parseShowcaseVideo('https://youtu.be/dQw4w9WgXcQ?si=ignored&x=<script>')!;
  const preview = showcaseVideoEmbedUrl(youtube, 'preview');
  assert.ok(preview.startsWith('https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?'));
  assert.ok(preview.includes('mute=1') && preview.includes('controls=0'));
  assert.ok(!preview.includes('ignored') && !preview.includes('script'));
  assert.ok(!showcaseVideoEmbedUrl(youtube, 'player').includes('mute=1'));
  assert.equal(showcaseVideoEmbedUrl(parseShowcaseVideo('https://vimeo.com/123')!, 'preview'), 'https://player.vimeo.com/video/123?autoplay=1&muted=1&loop=1&background=1');
  assert.ok(!VIDEO_IFRAME_SANDBOX.includes('allow-top-navigation') && !VIDEO_IFRAME_SANDBOX.includes('allow-forms'));
});

test('a Video link in the editor must be a YouTube or Vimeo link, other link types stay unrestricted', () => {
  const values = (type: 'Video' | 'Website', url: string): ShowcaseFormValues => ({
    highlights: '', developmentStage: 'Idea', showTeamMembers: true, links: [{ ...emptyLink(), linkType: type, label: 'Demo', url }],
  });
  const rowOf = (form: ShowcaseFormValues) => Object.values(validateShowcaseForm(form).linkRows)[0];
  assert.match(rowOf(values('Video', 'https://example.test/clip.mp4'))?.url ?? '', /YouTube or Vimeo/);
  assert.match(rowOf(values('Video', 'http://youtu.be/dQw4w9WgXcQ'))?.url ?? '', /YouTube or Vimeo/);
  assert.equal(rowOf(values('Video', 'https://youtu.be/dQw4w9WgXcQ')), undefined);
  assert.equal(rowOf(values('Video', 'https://vimeo.com/123456')), undefined);
  assert.equal(rowOf(values('Website', 'https://example.test/clip.mp4')), undefined);
});
