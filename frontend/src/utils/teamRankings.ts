import type { TeamRankingItem, TeamRankingViewItem } from '../types/rankings';

// Ranks are supplied by the authorized backend; clients never need raw scores.
export function rankTeamResults(items: TeamRankingItem[]): TeamRankingViewItem[] {
  return [...items].sort((left, right) => left.courseCode.localeCompare(right.courseCode)
    || (left.rank ?? Number.MAX_SAFE_INTEGER) - (right.rank ?? Number.MAX_SAFE_INTEGER)
    || left.teamName.localeCompare(right.teamName, undefined, { numeric: true }));
}

export function filterTeamRankingRows(
  rows: TeamRankingViewItem[], filters: { search: string; teamId: string; status: string },
): TeamRankingViewItem[] {
  const search = filters.search.trim().toLowerCase();
  return rows.filter(item =>
    (!search || [item.projectName, item.projectDescription, item.semesterGroupName, item.teamName,
      item.teamCode, item.classCode, item.courseCode].some(value => value.toLowerCase().includes(search)))
    && (!filters.teamId || item.teamId === filters.teamId)
    && (!filters.status || item.status === filters.status));
}
