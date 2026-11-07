/** Shared shape of the small action buttons in a My Showcase card; the tone says which one matters most. */
const ROW_ACTION_BASE =
  'inline-flex min-h-9 items-center gap-1.5 rounded-lg border px-3 text-body-sm font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600';

export const SHOWCASE_ROW_ACTION = {
  /** Leads to the public page: the call to action of a published card. */
  brand: `${ROW_ACTION_BASE} border-primary-200 bg-primary-50 text-primary-800 hover:border-primary-300 hover:bg-primary-100`,
  /** Secondary information that expands in place. */
  navy: `${ROW_ACTION_BASE} border-secondary-200 bg-secondary-50 text-secondary hover:bg-secondary-100`,
};
