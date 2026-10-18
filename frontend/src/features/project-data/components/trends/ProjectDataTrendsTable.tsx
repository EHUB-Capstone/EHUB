import type { ProjectDataTrendRow } from '../../../../types/projectData';
import {
  TREND_LEVEL_LABELS,
  formatCountChange,
  formatScore,
  scoreBarWidth,
  trendRowKey,
  type TrendLevel,
} from '../../../../utils/projectTrends';
import { ScoreChange } from './TrendDelta';

function Count({ value, change }: { value: number; change: number | null }) {
  return (
    <span>
      {value.toLocaleString()}
      {change !== null && change !== 0 && <span className="ml-1 text-caption text-slate-500">({formatCountChange(change)})</span>}
    </span>
  );
}

/** Every number behind the charts, so no value is reachable only by hovering. */
export default function ProjectDataTrendsTable({ rows, level }: { rows: ProjectDataTrendRow[]; level: TrendLevel }) {
  const showSubject = level !== 'semester';
  const showClass = level === 'class';

  return (
    <div className="mt-3 overflow-x-auto">
      <table className="w-full min-w-[800px] text-left text-body-sm">
        <caption className="sr-only">Average score and outcomes by {TREND_LEVEL_LABELS[level].toLowerCase()} and semester</caption>
        <thead>
          <tr className="border-b border-slate-200 text-caption text-slate-500">
            <th scope="col" className="py-2 pr-3 font-medium">Semester</th>
            {showSubject && <th scope="col" className="py-2 pr-3 font-medium">Subject</th>}
            {showClass && <th scope="col" className="py-2 pr-3 font-medium">Class</th>}
            <th scope="col" className="py-2 pr-3 text-right font-medium">Groups</th>
            <th scope="col" className="py-2 pr-3 text-right font-medium">Graded</th>
            <th scope="col" className="py-2 pr-3 font-medium">Avg score</th>
            <th scope="col" className="py-2 pr-3 font-medium">Change</th>
            <th scope="col" className="py-2 pr-3 text-right font-medium">Potential</th>
            <th scope="col" className="py-2 pr-3 text-right font-medium">Internally funded</th>
            <th scope="col" className="py-2 pr-3 text-right font-medium">Externally funded</th>
            <th scope="col" className="py-2 text-right font-medium">Awarded</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(row => (
            <tr key={trendRowKey(row)} className="border-b border-slate-100 last:border-0">
              <th scope="row" className="py-2 pr-3 font-semibold text-secondary">{row.semesterCode}</th>
              {showSubject && <td className="py-2 pr-3 text-slate-700">{row.subjectCode}</td>}
              {showClass && <td className="py-2 pr-3 text-slate-700">{row.classCode}</td>}
              <td className="py-2 pr-3 text-right">{row.totalGroups.toLocaleString()}</td>
              <td className="py-2 pr-3 text-right">{row.gradedGroups.toLocaleString()}</td>
              <td className="py-2 pr-3">
                <div className="flex items-center gap-2">
                  <span className="w-10 shrink-0 font-semibold text-secondary">{formatScore(row.averageScore)}</span>
                  <span className="block h-2 w-20 rounded-full bg-slate-100" aria-hidden="true">
                    <span className="block h-2 rounded-full bg-primary" style={{ width: `${scoreBarWidth(row.averageScore)}%` }} />
                  </span>
                </div>
              </td>
              <td className="py-2 pr-3"><ScoreChange change={row.averageScoreChange} /></td>
              <td className="py-2 pr-3 text-right"><Count value={row.potentialGroups} change={row.potentialChange} /></td>
              <td className="py-2 pr-3 text-right"><Count value={row.internallyFundedGroups} change={row.internallyFundedChange} /></td>
              <td className="py-2 pr-3 text-right"><Count value={row.externallyFundedGroups} change={row.externallyFundedChange} /></td>
              <td className="py-2 text-right"><Count value={row.awardedGroups} change={row.awardedChange} /></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
