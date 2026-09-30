import type {
  TeamRankingItem,
  TeamRankingScoreScope,
  TeamRankingStatus,
  TeamRankingViewItem,
} from '../types/rankings';

const collator = new Intl.Collator(undefined, { numeric: true, sensitivity: 'base' });

function checkpointNumber(scope: TeamRankingScoreScope): number | null {
  if (!scope.startsWith('checkpoint:')) return null;
  const value = Number(scope.slice('checkpoint:'.length));
  return Number.isInteger(value) ? value : null;
}

function scopedResult(item: TeamRankingItem, scope: TeamRankingScoreScope): {
  score: number | null;
  status: TeamRankingStatus;
} {
  const number = checkpointNumber(scope);
  if (number === null) return { score: item.courseTotal, status: item.status };
  const checkpoint = item.checkpoints.find(entry => entry.number === number);
  if (checkpoint?.score === null || checkpoint?.score === undefined) {
    return { score: null, status: 'INCOMPLETE' };
  }
  return {
    score: checkpoint.score,
    status: checkpoint.status === 'PUBLISHED' ? 'PUBLISHED' : 'READY_TO_PUBLISH',
  };
}

export function rankTeamResults(
  items: TeamRankingItem[],
  scope: TeamRankingScoreScope,
  classId = '',
): TeamRankingViewItem[] {
  const scoped = items
    .filter(item => !classId || item.classId === classId)
    .map(item => {
      const result = scopedResult(item, scope);
      return { ...item, rank: null, rankingScore: result.score, rankingStatus: result.status };
    })
    .sort((left, right) => {
      const leftRankable = left.rankingScore !== null;
      const rightRankable = right.rankingScore !== null;
      if (leftRankable !== rightRankable) return leftRankable ? -1 : 1;
      if (leftRankable && rightRankable && left.rankingScore !== right.rankingScore) {
        return Number(right.rankingScore) - Number(left.rankingScore);
      }
      const classCompare = collator.compare(left.classCode, right.classCode);
      return classCompare || collator.compare(left.teamName, right.teamName);
    });

  let previousScore: number | null = null;
  let previousRank = 0;
  return scoped.map((item, index) => {
    if (item.rankingScore === null) return item;
    const rank = previousScore !== null && item.rankingScore === previousScore
      ? previousRank
      : index + 1;
    previousScore = item.rankingScore;
    previousRank = rank;
    return { ...item, rank };
  });
}

export function filterTeamRankingRows(
  rows: TeamRankingViewItem[],
  filters: { search: string; teamId: string; status: string },
): TeamRankingViewItem[] {
  const search = filters.search.trim().toLowerCase();
  return rows.filter(item => (
    (!search || [item.projectName, item.projectDescription, item.semesterGroupName, item.teamName, item.teamCode, item.classCode, item.courseCode]
      .some(value => value.toLowerCase().includes(search))) &&
    (!filters.teamId || item.teamId === filters.teamId) &&
    (!filters.status || item.rankingStatus === filters.status)
  ));
}
