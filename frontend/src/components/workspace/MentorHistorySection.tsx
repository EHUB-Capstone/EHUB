import { History } from 'lucide-react';
import type { MentorHistoryItem } from '../../types/teamManagement';
import { MENTOR_KIND_STYLES } from '../../utils/mentorKindStyles';
import { describeMentorHistoryEnd, groupMentorHistoryBySemester } from '../../utils/mentorHistory';
import MentorKindTag from '../admin/MentorKindTag';

interface MentorHistorySectionProps {
  items: MentorHistoryItem[];
}

/**
 * Teams the signed-in mentor mentored in the past. It is information only: an ended assignment does not
 * give access to the team's workspace, so these cards are deliberately not links.
 */
export default function MentorHistorySection({ items }: MentorHistorySectionProps) {
  if (items.length === 0) return null;
  const groups = groupMentorHistoryBySemester(items);

  return (
    <section aria-labelledby="mentor-history-title" className="space-y-4">
      <div className="flex items-center gap-3">
        <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-slate-100 text-slate-500"><History className="h-4 w-4" /></span>
        <div>
          <h2 id="mentor-history-title" className="text-lg font-bold text-slate-900">Previous teams</h2>
          <p className="text-xs text-slate-500">Teams you mentored before. This list is for reference; the team workspace is no longer open to you.</p>
        </div>
        <span className="h-px flex-1 bg-slate-200" />
        <span className="shrink-0 rounded-full bg-slate-100 px-2.5 py-1 text-xs font-semibold text-slate-500">{items.length}</span>
      </div>

      {groups.map(group => (
        <div key={group.semester} className="space-y-2">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">{group.semester || 'Unknown semester'}</p>
          <ul className="grid grid-cols-1 gap-3 md:grid-cols-2">
            {group.items.map(item => (
              <li key={item.assignmentId} className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-sm">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="truncate text-base font-bold text-slate-900">{item.teamName}</p>
                    {item.projectName && item.projectName !== item.teamName && (
                      <p className="mt-0.5 truncate text-xs text-slate-500" title={item.projectName}>{item.projectName}</p>
                    )}
                    <p className="mt-1 text-xs font-medium text-slate-500">{item.classCode} · {item.subjectCode}</p>
                  </div>
                  <MentorKindTag type={item.slot} />
                </div>
                <p className="mt-3 border-t border-slate-100 pt-3 text-[11px] text-slate-500">
                  {describeMentorHistoryEnd(item)}
                </p>
                <span className="sr-only">{MENTOR_KIND_STYLES[item.slot].label}</span>
              </li>
            ))}
          </ul>
        </div>
      ))}
    </section>
  );
}
