import i18n from 'i18next';
import { getLanguageOption } from './config.ts';

// Locale-aware formatting that follows the active UI language. These are plain
// functions (not hooks) so utils, chart formatters and toasts can use them too;
// components re-render on a language change through useTranslation().

export type DateInput = Date | string | number | null | undefined;

export function getIntlLocale(): string {
  return getLanguageOption(i18n.resolvedLanguage ?? i18n.language).intlLocale;
}

export function toValidDate(value: DateInput): Date | null {
  if (value === null || value === undefined || value === '') return null;
  const date = value instanceof Date ? value : new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

export function formatDate(
  value: DateInput,
  options?: Intl.DateTimeFormatOptions,
  fallback = '',
): string {
  const date = toValidDate(value);
  return date ? new Intl.DateTimeFormat(getIntlLocale(), options).format(date) : fallback;
}

const DATE_TIME_DEFAULTS: Intl.DateTimeFormatOptions = {
  year: 'numeric', month: 'numeric', day: 'numeric', hour: 'numeric', minute: 'numeric', second: 'numeric',
};
const TIME_DEFAULTS: Intl.DateTimeFormatOptions = { hour: 'numeric', minute: 'numeric', second: 'numeric' };

// Without options these behave like Date#toLocaleString() / toLocaleTimeString(), but in the active UI language.
export function formatDateTime(
  value: DateInput,
  options?: Intl.DateTimeFormatOptions,
  fallback = '',
): string {
  return formatDate(value, options ?? DATE_TIME_DEFAULTS, fallback);
}

export function formatTime(
  value: DateInput,
  options?: Intl.DateTimeFormatOptions,
  fallback = '',
): string {
  return formatDate(value, options ?? TIME_DEFAULTS, fallback);
}

export function formatNumber(
  value: number | null | undefined,
  options?: Intl.NumberFormatOptions,
  fallback = '',
): string {
  if (typeof value !== 'number' || !Number.isFinite(value)) return fallback;
  return new Intl.NumberFormat(getIntlLocale(), options).format(value);
}

// `value` is a ratio-free percentage such as 42 or 42.5 (not 0.425).
export function formatPercent(value: number | null | undefined, fractionDigits = 0, fallback = ''): string {
  if (typeof value !== 'number' || !Number.isFinite(value)) return fallback;
  return `${formatNumber(value, { maximumFractionDigits: fractionDigits, minimumFractionDigits: 0 })}%`;
}

const RELATIVE_UNITS: Array<[Intl.RelativeTimeFormatUnit, number]> = [
  ['year', 365 * 24 * 60 * 60 * 1000],
  ['month', 30 * 24 * 60 * 60 * 1000],
  ['day', 24 * 60 * 60 * 1000],
  ['hour', 60 * 60 * 1000],
  ['minute', 60 * 1000],
];

export function formatRelativeTime(value: DateInput, now: Date = new Date(), fallback = ''): string {
  const date = toValidDate(value);
  if (!date) return fallback;
  const formatter = new Intl.RelativeTimeFormat(getIntlLocale(), { numeric: 'auto' });
  const diff = date.getTime() - now.getTime();
  for (const [unit, size] of RELATIVE_UNITS) {
    if (Math.abs(diff) >= size) return formatter.format(Math.round(diff / size), unit);
  }
  return formatter.format(0, 'minute');
}
