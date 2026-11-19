// What the quick view finds without the AI that a reviewer may want to ask the team about: status lines and figures that disagree.
import { ArrowLeftRight, CheckCircle2 } from 'lucide-react';
import Badge from '../../../ui/Badge';
import { statusItemView } from '../../../../utils/checkpointSummary';
import { summaryText } from '../../../../utils/summaryText';
import type { FigureConflict, StatusItem } from '../../../../types/checkpointSummary';
import { Quoted, SectionLabel } from './SummaryParts';

/** Lines of the file that say where work stands (DONE, IN PROGRESS, PENDING…), quoted as written. */
export function StatusLines({ items }: { items: StatusItem[] }) {
  if (items.length === 0) return null;
  return (
    <div className="space-y-2">
      <SectionLabel icon={CheckCircle2}>{summaryText.file.statusLines}</SectionLabel>
      <ul className="space-y-1.5">
        {items.map((item) => {
          const view = statusItemView[item.status];
          return (
            <li key={`${item.status}-${item.request ?? ''}-${item.text}`} className="flex items-start gap-2 text-body-sm text-slate-700">
              <Badge variant={view.tone}>{view.label}</Badge>
              <span className="min-w-0 flex-1">
                {item.request && <span className="block font-medium text-slate-900">{item.request}</span>}
                <span className={item.request ? 'block text-slate-600' : undefined}>{item.text}</span>
              </span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

/** Figures the file gives twice with different values: worth asking the team which one is right. */
export function FigureConflicts({ conflicts, acrossFiles = false, earlier = false }: { conflicts: FigureConflict[]; acrossFiles?: boolean; earlier?: boolean }) {
  if (conflicts.length === 0) return null;
  const text = summaryText.figures;
  return (
    <div className="space-y-2">
      <SectionLabel icon={ArrowLeftRight}>{earlier ? text.earlierTitle : acrossFiles ? text.acrossFilesTitle : text.insideFileTitle}</SectionLabel>
      <p className="text-caption text-slate-500">{earlier ? text.earlierNote : acrossFiles ? text.acrossFilesNote : text.insideFileNote}</p>
      <ul className="space-y-2">
        {conflicts.map((conflict, position) => (
          <li key={`${conflict.subject}-${position}`} className="space-y-1">
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-body-sm font-medium text-slate-900">{conflict.subject}</span>
              {conflict.values.map((item) => <Badge key={`${item.value}-${item.source ?? ''}`} variant="warning">{item.value}</Badge>)}
            </div>
            {conflict.values.map((item) => <Quoted key={`${item.source ?? ''}-${item.quote}`} text={item.quote} source={item.source} />)}
          </li>
        ))}
      </ul>
    </div>
  );
}

