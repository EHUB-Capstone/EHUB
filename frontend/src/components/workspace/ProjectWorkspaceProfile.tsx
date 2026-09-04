import { CalendarDays, Clock3, GraduationCap, Layers3, Tag } from 'lucide-react';

type Props = {
  project: {
    projectName?: string;
    description?: string;
    startupField?: string;
    technologyStack?: string[];
    keywords?: string[];
    status?: string;
    createdAtUtc?: string;
    updatedAtUtc?: string | null;
  };
  classInfo: {
    classCode?: string;
    subjectCode?: string;
    subjectName?: string;
    semesterCode?: string;
  };
  activities?: Array<{
    id?: string;
    action?: string;
    summary?: string;
    actorName?: string;
    occurredAtUtc?: string;
  }>;
};

const formatDateTime = (value?: string | null) => {
  if (!value) return 'Not available';
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? 'Not available'
    : new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(parsed);
};

const TagList = ({ values, empty }: { values?: string[]; empty: string }) => (
  values?.length ? (
    <div className="flex flex-wrap gap-1.5">
      {values.map((value) => (
        <span key={value} className="rounded-lg border border-slate-200 bg-slate-50 px-2 py-1 text-xs font-medium text-slate-600">
          {value}
        </span>
      ))}
    </div>
  ) : <p className="text-sm italic text-slate-400">{empty}</p>
);

export default function ProjectWorkspaceProfile({ project, classInfo, activities = [] }: Props) {
  return (
    <>
      <section className="overflow-hidden rounded-2xl border border-slate-200/70 bg-white shadow-sm">
        <div className="border-b border-slate-100 px-5 py-4">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <div className="flex items-center gap-2">
                <h2 className="text-lg font-bold text-slate-900">{project.projectName}</h2>
                <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide text-emerald-700">
                  {project.status || 'Draft'}
                </span>
              </div>
              <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-slate-600">{project.description}</p>
            </div>
            <div className="rounded-xl border border-primary-100 bg-primary-50 px-3 py-2 text-right">
              <p className="text-[10px] font-bold uppercase tracking-wider text-primary-500">Startup field</p>
              <p className="mt-0.5 text-sm font-semibold text-primary">{project.startupField || 'Not specified'}</p>
            </div>
          </div>
        </div>

        <div className="grid gap-4 px-5 py-4 md:grid-cols-2">
          <div>
            <p className="mb-2 flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-slate-400">
              <Layers3 className="h-3.5 w-3.5" /> Technology stack
            </p>
            <TagList values={project.technologyStack} empty="No technologies listed." />
          </div>
          <div>
            <p className="mb-2 flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-slate-400">
              <Tag className="h-3.5 w-3.5" /> Keywords
            </p>
            <TagList values={project.keywords} empty="No keywords listed." />
          </div>
        </div>

        <div className="grid border-t border-slate-100 bg-slate-50/60 sm:grid-cols-3">
          <div className="border-b border-slate-100 px-5 py-3 sm:border-b-0 sm:border-r">
            <p className="text-[10px] font-bold uppercase tracking-wider text-slate-400">Class</p>
            <p className="mt-1 text-sm font-semibold text-slate-700">{classInfo.classCode || '—'}</p>
          </div>
          <div className="border-b border-slate-100 px-5 py-3 sm:border-b-0 sm:border-r">
            <p className="flex items-center gap-1 text-[10px] font-bold uppercase tracking-wider text-slate-400">
              <GraduationCap className="h-3 w-3" /> Subject
            </p>
            <p className="mt-1 text-sm font-semibold text-slate-700">
              {[classInfo.subjectCode, classInfo.subjectName].filter(Boolean).join(' · ') || '—'}
            </p>
          </div>
          <div className="px-5 py-3">
            <p className="flex items-center gap-1 text-[10px] font-bold uppercase tracking-wider text-slate-400">
              <CalendarDays className="h-3 w-3" /> Semester
            </p>
            <p className="mt-1 text-sm font-semibold text-slate-700">{classInfo.semesterCode || '—'}</p>
          </div>
        </div>
      </section>

      <section className="rounded-2xl border border-slate-200/70 bg-white p-5 shadow-sm">
        <div className="mb-4 flex items-center justify-between gap-3">
          <div>
            <h3 className="font-bold text-slate-800">Project activity</h3>
            <p className="mt-0.5 text-xs text-slate-400">Latest workspace profile changes.</p>
          </div>
          {project.updatedAtUtc && (
            <span className="text-xs font-medium text-slate-400">Updated {formatDateTime(project.updatedAtUtc)}</span>
          )}
        </div>
        {activities.length === 0 ? (
          <div className="rounded-xl border border-dashed border-slate-200 py-6 text-center text-sm text-slate-400">
            No project activity recorded yet.
          </div>
        ) : (
          <ol className="space-y-0">
            {activities.map((activity, index) => (
              <li key={activity.id || index} className="relative flex gap-3 pb-5 last:pb-0">
                {index < activities.length - 1 && <span className="absolute left-[15px] top-8 h-[calc(100%-1.5rem)] w-px bg-slate-200" />}
                <span className="relative z-10 flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-primary-100 bg-primary-50 text-primary">
                  <Clock3 className="h-3.5 w-3.5" />
                </span>
                <div className="min-w-0 pt-0.5">
                  <p className="text-sm font-semibold text-slate-700">{activity.summary || 'Project workspace updated.'}</p>
                  <p className="mt-0.5 text-xs text-slate-400">
                    {activity.actorName || 'System'} · {formatDateTime(activity.occurredAtUtc)}
                  </p>
                </div>
              </li>
            ))}
          </ol>
        )}
      </section>
    </>
  );
}
