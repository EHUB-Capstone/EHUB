import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const nginxConfig = readFileSync(new URL('../nginx.conf', import.meta.url), 'utf8').replace(/\r\n/g, '\n');

// Text of one `location` block, from its header line to its closing brace.
function locationBlock(header: string): string {
  const start = nginxConfig.indexOf(header);
  assert.notEqual(start, -1, `missing ${header}`);
  return nginxConfig.slice(start, nginxConfig.indexOf('\n    }', start));
}

const GENERIC_MJS = 'location ~* \\.mjs$ {';
const ASSET_MJS = 'location ~* ^/assets/.+\\.mjs$ {';

test('frontend nginx serves .mjs (the pdf.js worker) as JavaScript so document preview works', () => {
  // nginx 1.27 has no .mjs entry in mime.types; without these blocks the worker is sent as
  // application/octet-stream and, with nosniff, browsers refuse to run it.
  assert.match(locationBlock(GENERIC_MJS), /default_type text\/javascript;/);
  assert.match(locationBlock(ASSET_MJS), /default_type text\/javascript;/);
  assert.match(nginxConfig, /add_header X-Content-Type-Options "nosniff" always;/);
});

test('frontend nginx does not replace the default MIME table with a server-level types block', () => {
  const withoutComments = nginxConfig.replace(/#.*$/gm, '');
  assert.doesNotMatch(withoutComments, /^\s*types\s*\{/m);
});

test('hashed build assets are cached for a year and keep the security headers', () => {
  for (const header of ['location /assets/ {', ASSET_MJS]) {
    const block = locationBlock(header);
    // No "always": the header must not be attached to a 404/5xx that the browser would then keep for a year.
    assert.match(block, /add_header Cache-Control "public, max-age=31536000, immutable";/);
    assert.match(block, /add_header X-Content-Type-Options "nosniff" always;/);
    assert.match(block, /try_files \$uri =404;/);
  }
  // The assets .mjs rule must come first: the generic one would otherwise win and skip the cache header.
  assert.ok(nginxConfig.indexOf(ASSET_MJS) < nginxConfig.indexOf(GENERIC_MJS));
});

test('the entry point and SPA routes are always revalidated and keep nosniff', () => {
  const block = locationBlock('location / {');
  assert.match(block, /add_header Cache-Control "no-cache" always;/);
  assert.match(block, /add_header X-Content-Type-Options "nosniff" always;/);
  assert.match(block, /try_files \$uri \$uri\/ \/index\.html;/);
});

test('the /assets/ prefix is not "^~", which would skip the .mjs regex rule and serve the worker as octet-stream', () => {
  assert.doesNotMatch(nginxConfig, /location \^~ \/assets\//);
});
