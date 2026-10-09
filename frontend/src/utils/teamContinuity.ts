import type { TeamContinuationItem, TeamContinuationOutcome, TeamContinuationSummary } from '../types/classes';

const OUTCOME_LABELS: Record<TeamContinuationOutcome, string> = {
  Created: 'Continued',
  MembersAdded: 'Members added',
  NoChange: 'No change',
  NotEligible: 'Not eligible',
  Dissolved: 'Dissolved earlier',
  ContinuedElsewhere: 'Continued in another class',
};

export function continuationOutcomeLabel(outcome: TeamContinuationOutcome): string {
  return OUTCOME_LABELS[outcome] ?? outcome;
}

/** Outcomes that need a lecturer's attention rather than just information. */
export function continuationNeedsAttention(item: TeamContinuationItem): boolean {
  return item.outcome === 'NotEligible' || item.outcome === 'Dissolved' || item.outcome === 'ContinuedElsewhere';
}

export function hasContinuationActivity(summary: TeamContinuationSummary | null | undefined): boolean {
  return Boolean(summary && summary.items.length > 0);
}

export function continuationHeadline(summary: TeamContinuationSummary, applied: boolean): string {
  const source = summary.sourceSemesterCode ? ` from ${summary.sourceSemesterCode}` : '';
  const parts: string[] = [];
  if (summary.createdCount > 0) {
    parts.push(`${summary.createdCount} team${summary.createdCount > 1 ? 's' : ''}${source} ${applied ? 'continued' : 'will be continued'}`);
  }
  if (summary.membersAddedCount > 0) {
    parts.push(`${summary.membersAddedCount} team${summary.membersAddedCount > 1 ? 's' : ''} ${applied ? 'received' : 'will receive'} late members`);
  }
  if (summary.notEligibleCount > 0) {
    parts.push(`${summary.notEligibleCount} not eligible yet`);
  }
  return parts.length > 0 ? parts.join(', ') : `No team${source} can be continued`;
}

export function semesterTermLabel(semesterCode: string, classCode: string): string {
  return `${semesterCode} · ${classCode}`;
}
