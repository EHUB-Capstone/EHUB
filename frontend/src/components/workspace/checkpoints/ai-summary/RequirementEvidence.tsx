// The requirements and rubric criteria of a checkpoint with what the files say about each. One component for the list of a single
// file and for the list combined over all files, so the two cannot drift apart.
import Badge from '../../../ui/Badge';
import { displayNumbers, sortByWeight, summariseEvidence, summaryFindingView } from '../../../../utils/checkpointSummary';
import { summaryText } from '../../../../utils/summaryText';
import type { CriterionCue, RequirementFinding } from '../../../../types/checkpointSummary';
import { Cues, Quoted } from './SummaryParts';

/** What both kinds of finding (one file's, and the combined one) have in common. */
export interface EvidenceItem {
  index: number;
  kind: string;
  title: string;
  finding: RequirementFinding;
  evidence: string | null;
  evidencePage?: number | null;
  weight?: number | null;
  cues?: CriterionCue[];
  /** Combined findings only: the file the quote comes from. */
  source?: string | null;
  sourceFileId?: string | null;
}

const KIND_ORDER = ['Criterion', 'Requirement'];

/** Live and product criteria are listed apart, so "nothing found" refers only to what a document can show. */
function OtherCriteria({ items, numbers }: { items: EvidenceItem[]; numbers: Map<number, number> }) {
  if (items.length === 0) return null;
  return (
    <ul className="space-y-1">
      {items.map((item) => {
        const view = summaryFindingView[item.finding];
        return (
          <li key={item.index} className="flex flex-wrap items-center gap-2 text-body-sm text-slate-700" title={view.hint}>
            <span>{numbers.get(item.index)}. {item.title}</span>
            {item.weight != null && <span className="text-caption text-slate-500">{item.weight}%</span>}
            <Badge variant={view.tone}>{view.label}</Badge>
          </li>
        );
      })}
    </ul>
  );
}

/** Titles of the items a file does not show, in one group with one explanation instead of one sentence per item. */
function MissingGroup({ heading, hint, items, numbers }: { heading: string; hint: string; items: EvidenceItem[]; numbers: Map<number, number> }) {
  if (items.length === 0) return null;
  return (
    <div className="space-y-1.5">
      <p className="text-caption font-semibold text-slate-600">{heading}</p>
      <ul className="flex flex-wrap gap-1.5">
        {items.map((item) => (
          <li key={item.index} className="rounded-md border border-slate-200 bg-slate-50 px-2 py-0.5 text-caption text-slate-600">
            {numbers.get(item.index)}. {item.title}
          </li>
        ))}
      </ul>
      <p className="text-caption text-slate-500">{hint}</p>
    </div>
  );
}

function FoundRow({ item, number, fileId, fileName }: { item: EvidenceItem; number: number | undefined; fileId?: string; fileName?: string }) {
  const finding = summaryFindingView[item.finding];
  return (
    <li className="space-y-1">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-body-sm font-semibold text-slate-900">{number}. {item.title}</span>
        {item.weight != null && <span className="text-caption text-slate-500">{item.weight}%</span>}
        <Badge variant={finding.tone}>{finding.label}</Badge>
      </div>
      {item.evidence
        ? <Quoted text={item.evidence} source={item.source} fileId={item.sourceFileId ?? fileId} fileName={fileName} page={item.evidencePage} />
        : <p className="text-caption text-slate-500">{finding.hint}</p>}
      <Cues cues={item.cues ?? []} />
    </li>
  );
}

interface RequirementEvidenceProps {
  items: EvidenceItem[];
  /** "file": the list of one file; "files": the list combined over every file read. */
  scope: 'file' | 'files';
  /** One file's list quotes from that file. */
  fileId?: string;
  fileName?: string;
}

export function RequirementEvidence({ items, scope, fileId, fileName }: RequirementEvidenceProps) {
  const text = summaryText.requirements;
  const numbers = displayNumbers(items);
  const view = summariseEvidence(items);

  if (view.nothingFound) {
    return (
      <div className="space-y-2">
        <p className="text-body-sm text-slate-600">{text.nothingFound(scope, view.searchable.map((item) => item.title))}</p>
        <OtherCriteria items={view.others} numbers={numbers} />
      </div>
    );
  }

  const groups = view.groupedByKind
    ? KIND_ORDER.map((kind) => ({ kind, rows: sortByWeight(view.found.filter((item) => item.kind === kind)) }))
    : [{ kind: '', rows: view.found }];

  return (
    <div className="space-y-3">
      <p className="text-caption text-slate-500">{text.foundOf(view.found.length, view.searchable.length)}</p>
      {groups.filter((group) => group.rows.length > 0).map((group) => (
        <div key={group.kind || 'all'} className="space-y-2">
          {view.groupedByKind && (
            <p className="text-caption font-semibold text-slate-600">{group.kind === 'Criterion' ? text.rubricCriteria : text.checkpointRequirements}</p>
          )}
          <ul className="space-y-3">
            {group.rows.map((item) => <FoundRow key={item.index} item={item} number={numbers.get(item.index)} fileId={fileId} fileName={fileName} />)}
          </ul>
        </div>
      ))}
      <MissingGroup heading={text.notFoundGroup(view.notFound.length)} hint={summaryFindingView.NotFound.hint} numbers={numbers} items={view.notFound} />
      <MissingGroup heading={text.notCheckedGroup(view.notChecked.length)} hint={summaryFindingView.NotChecked.hint} numbers={numbers} items={view.notChecked} />
      <OtherCriteria items={view.others} numbers={numbers} />
    </div>
  );
}
