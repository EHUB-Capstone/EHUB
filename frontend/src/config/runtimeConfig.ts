const normalizeOrigin = (value: string): string =>
  value.trim().replace(/\/api\/?$/i, '').replace(/\/+$/, '');

const browserOrigin = typeof window === 'undefined' ? '' : window.location.origin;

export const runtimeConfig = Object.freeze({
  apiBasePath: '/api',
  realtime: Object.freeze({
    enabled: import.meta.env.VITE_ENABLE_REALTIME === 'true',
    origin: normalizeOrigin(import.meta.env.VITE_REALTIME_ORIGIN || browserOrigin),
  }),
});
