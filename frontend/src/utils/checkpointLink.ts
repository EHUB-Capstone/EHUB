import i18n from '../i18n/index.ts';
export const CHECKPOINT_LINK_NAME_MAX_LENGTH = 100;
export const CHECKPOINT_LINK_URL_MAX_LENGTH = 1_000;

export function normalizeCheckpointLinkUrl(value: string): string {
  const trimmed = value.trim();
  const candidate = trimmed.includes('://') ? trimmed : `https://${trimmed}`;
  try {
    const parsed = new URL(candidate);
    return parsed.pathname === '/' && !parsed.search && !parsed.hash ? parsed.origin : parsed.toString();
  } catch {
    return candidate;
  }
}

export function validateCheckpointLinkUrl(value: string): string | null {
  const candidate = value.trim();
  if (!candidate) return i18n.t('utils:checkpointLink.linkUrlIsRequired');
  if (candidate.length > CHECKPOINT_LINK_URL_MAX_LENGTH) return i18n.t('utils:checkpointLink.linkUrlMustNotExceedCharacters', { CHECKPOINT_LINK_URL_MAX_LENGTH });
  try {
    const parsed = new URL(normalizeCheckpointLinkUrl(candidate));
    if (parsed.protocol !== 'https:') return i18n.t('utils:checkpointLink.onlyPublicHttpsLinksAreAccepted');
    if (parsed.username || parsed.password) return i18n.t('utils:checkpointLink.linksContainingEmbeddedCredentialsAreNot');
    const host = parsed.hostname.replace(/^\[|\]$/g, '').replace(/\.$/, '').toLowerCase();
    if (!host || host === 'localhost' || host.endsWith('.localhost') || host.endsWith('.local') ||
      host.endsWith('.internal') || host.endsWith('.lan') || (!host.includes('.') && !host.includes(':')) || isPrivateIp(host)) {
      return i18n.t('utils:checkpointLink.onlyPublicHttpsLinksAreAccepted');
    }
    return null;
  } catch {
    return i18n.t('utils:checkpointLink.enterAValidPublicHttpsLink');
  }
}

function isPrivateIp(host: string): boolean {
  if (host === '::1' || host === '::' || /^f[cd][0-9a-f:]*$/i.test(host) || /^fe[89ab][0-9a-f:]*$/i.test(host)) return true;
  const parts = host.split('.');
  if (parts.length !== 4 || parts.some(part => !/^\d+$/.test(part) || Number(part) > 255)) return false;
  const [first, second] = parts.map(Number);
  return first === 0 || first === 10 || first === 127 || first >= 224 ||
    (first === 100 && second >= 64 && second <= 127) ||
    (first === 169 && second === 254) ||
    (first === 172 && second >= 16 && second <= 31) ||
    (first === 192 && second === 168) ||
    (first === 198 && (second === 18 || second === 19));
}
