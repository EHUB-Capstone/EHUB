import i18n from './index.ts';

// Backend/enum values stay raw in data and API calls; their display text is looked up here.
// `key` is a translation path such as `projectData:achievements`; unknown values fall back to the raw value.
export function enumLabel(key: string, value: string | null | undefined): string {
  if (value === null || value === undefined || value === '') return '';
  return i18n.t(`${key}.${value}`, { defaultValue: String(value) });
}

export const achievementLabel = (value: string | null | undefined): string => enumLabel('projectData:achievements', value);
