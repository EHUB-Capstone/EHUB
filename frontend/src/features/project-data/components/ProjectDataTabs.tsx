import { useRef, type KeyboardEvent } from 'react';
import { PROJECT_DATA_TABS, type ProjectDataTab } from '../../../types/projectData';
import { cn } from '../../../utils/cn';

const LABELS: Record<ProjectDataTab, string> = { overview: 'Overview', analytics: 'Analytics' };

export const projectDataTabId = (tab: ProjectDataTab) => `project-data-tab-${tab}`;
export const projectDataPanelId = (tab: ProjectDataTab) => `project-data-panel-${tab}`;

interface ProjectDataTabsProps {
  value: ProjectDataTab;
  onChange: (tab: ProjectDataTab) => void;
}

/** Overview (the table) and Analytics (distribution charts). Arrow keys, Home and End move between tabs. */
export default function ProjectDataTabs({ value, onChange }: ProjectDataTabsProps) {
  const refs = useRef<Partial<Record<ProjectDataTab, HTMLButtonElement | null>>>({});

  const onKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    const index = PROJECT_DATA_TABS.indexOf(value);
    let next = index;
    if (event.key === 'ArrowRight') next = (index + 1) % PROJECT_DATA_TABS.length;
    else if (event.key === 'ArrowLeft') next = (index - 1 + PROJECT_DATA_TABS.length) % PROJECT_DATA_TABS.length;
    else if (event.key === 'Home') next = 0;
    else if (event.key === 'End') next = PROJECT_DATA_TABS.length - 1;
    else return;
    event.preventDefault();
    const tab = PROJECT_DATA_TABS[next];
    onChange(tab);
    refs.current[tab]?.focus();
  };

  return (
    <div role="tablist" aria-label="Project data views" className="mb-4 flex gap-1 border-b border-slate-200">
      {PROJECT_DATA_TABS.map(tab => {
        const active = tab === value;
        return (
          <button
            key={tab}
            ref={element => { refs.current[tab] = element; }}
            type="button"
            role="tab"
            id={projectDataTabId(tab)}
            aria-selected={active}
            aria-controls={projectDataPanelId(tab)}
            tabIndex={active ? 0 : -1}
            onClick={() => onChange(tab)}
            onKeyDown={onKeyDown}
            className={cn(
              '-mb-px border-b-2 px-4 py-2 text-body-sm font-semibold transition-colors',
              active ? 'border-primary text-primary-700' : 'border-transparent text-slate-500 hover:text-slate-800',
            )}
          >
            {LABELS[tab]}
          </button>
        );
      })}
    </div>
  );
}
