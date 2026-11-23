# i18n (English / Vietnamese)

Library: `i18next` + `react-i18next`. Default language is English; the choice is kept in
`localStorage['ehub-language']` and restored on refresh / next sign-in. `LanguageProvider`
(`src/context/LanguageContext.tsx`) owns it and is independent from `ThemeContext` and `AuthContext`.

## Layout

```
src/i18n/
  config.ts      supported languages, default, storage helpers
  index.ts       i18next init + lazy namespace backend
  resources.ts   GENERATED: bundled (shared) namespaces + lazy loaders — do not hand-edit
  format.ts      formatDate / formatDateTime / formatTime / formatNumber / formatPercent / formatRelativeTime
  labels.ts      enumLabel / achievementLabel for backend enum values shown as text
  messages.ts    localize validation messages and API error codes
  locales/<lang>/<namespace>.json
```

Namespaces: `common`, `nav`, `errors`, `utils`, `auth`, `home` are bundled; `admin`, `class`, `workspace`,
`checkpoint`, `evaluation`, `mentoring`, `showcase`, `ai`, `projectData`, `community`, `profile`, `dashboard`
are separate chunks, loaded the first time a screen uses them (the route `<Suspense>` shows the spinner) and
prefetched in the background after sign-in.

## Writing text

- Components: `const { t } = useTranslation('<namespace>')`, then `t('section.key')`; other namespaces: `t('common:actions.save')`.
- Outside React (utils, constants, toasts in helpers): `import i18n from '…/i18n'` and `i18n.t('ns:key')`.
  Inside module-level objects use a getter (`get label() { return i18n.t('ns:key'); }`) so it follows the language.
- Plurals: `t('key', { count })` with `key_one` / `key_other` (Vietnamese only needs `_other`). Never build words with `'s'`.
- Dates / numbers: use `format.ts`, never `toLocaleString('en-GB')` or a hardcoded locale.
- Backend enum values stay raw in data and API calls; translate them where they are displayed (`enumLabel`).
- Server text (messages without a known error code, user-written content) is shown as received.

## Adding a language

1. Add an entry to `SUPPORTED_LANGUAGES` in `config.ts` and a flag in `components/ui/flags.tsx`.
2. Copy `locales/en/` to `locales/<code>/`, translate, and regenerate `resources.ts`.
3. Add its `react-day-picker` locale in `SemesterDateRangePicker.tsx`.

## Guard rails (run by `npm test`)

- `i18nParity` — every language has the same namespaces/keys/placeholders as `en`, no empty values.
- `i18nKeys` — every literal `t('…')` key (and the prefix of every dynamic key) exists in `en`.
- `noHardcodedText` — no readable literal text in JSX, text attributes or `toast.*` calls.
