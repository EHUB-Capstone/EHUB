// @ts-nocheck
import { CheckCircle2, ListTodo, BarChart3, Eye, Loader2 } from 'lucide-react';

const STATUS_CONFIG = {
  TODO: { label: 'To Do', color: 'bg-gray-400', text: 'text-gray-600' },
  IN_PROGRESS: { label: 'In Progress', color: 'bg-blue-500', text: 'text-blue-600' },
  REVIEW: { label: 'Review', color: 'bg-yellow-400', text: 'text-yellow-600' },
  DONE: { label: 'Done', color: 'bg-green-500', text: 'text-green-600' },
};

function StatCard({ icon: Icon, label, value, accent }) {
  return (
    <div className="bg-white border border-gray-200 rounded-xl p-4 flex items-center gap-3 min-w-0">
      <div className={`shrink-0 w-10 h-10 rounded-lg flex items-center justify-center ${accent}`}>
        <Icon size={20} className="text-white" />
      </div>
      <div className="min-w-0">
        <p className="text-xs font-medium text-gray-500 uppercase tracking-wide truncate">
          {label}
        </p>
        <p className="text-xl font-bold text-gray-900">{value}</p>
      </div>
    </div>
  );
}

function SkeletonCard() {
  return (
    <div className="bg-white border border-gray-200 rounded-xl p-4 animate-pulse">
      <div className="flex items-center gap-3">
        <div className="w-10 h-10 rounded-lg bg-gray-200" />
        <div className="space-y-2 flex-1">
          <div className="h-3 bg-gray-200 rounded w-20" />
          <div className="h-5 bg-gray-200 rounded w-12" />
        </div>
      </div>
    </div>
  );
}

export default function ProgressSummary({ progressData, loading }) {
  // Loading state
  if (loading) {
    return (
      <div className="space-y-6">
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          <SkeletonCard />
          <SkeletonCard />
          <SkeletonCard />
          <SkeletonCard />
        </div>
        <div className="bg-white border border-gray-200 rounded-xl p-6 animate-pulse space-y-4">
          <div className="h-4 bg-gray-200 rounded w-40" />
          <div className="h-4 bg-gray-200 rounded-full w-full" />
          <div className="h-4 bg-gray-200 rounded w-56 mt-6" />
          <div className="space-y-2">
            <div className="h-3 bg-gray-200 rounded w-full" />
            <div className="h-3 bg-gray-200 rounded w-3/4" />
          </div>
        </div>
      </div>
    );
  }

  // Empty / no-data state
  if (!progressData || (progressData.totalTasks ?? 0) === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-xl p-10 text-center">
        <ListTodo size={40} className="mx-auto text-gray-300 mb-3" />
        <p className="text-gray-500 font-medium">No tasks yet</p>
        <p className="text-xs text-gray-400 mt-1">
          Create tasks and milestones to start tracking progress.
        </p>
      </div>
    );
  }

  const {
    totalTasks = 0,
    doneTasks = 0,
    overallProgress = 0,
    tasksByStatus = {},
  } = progressData;

  const reviewTasks = tasksByStatus.REVIEW ?? 0;
  const progressPct = Math.min(Math.round(overallProgress), 100);

  return (
    <div className="space-y-6">
      {/* Stat cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard
          icon={ListTodo}
          label="Total Tasks"
          value={totalTasks}
          accent="bg-indigo-500"
        />
        <StatCard
          icon={CheckCircle2}
          label="Done"
          value={doneTasks}
          accent="bg-green-500"
        />
        <StatCard
          icon={BarChart3}
          label="Progress"
          value={`${progressPct}%`}
          accent="bg-blue-500"
        />
        <StatCard
          icon={Eye}
          label="In Review"
          value={reviewTasks}
          accent="bg-yellow-500"
        />
      </div>

      {/* Progress bar + status breakdown */}
      <div className="bg-white border border-gray-200 rounded-xl p-6 space-y-5">
        {/* Overall progress */}
        <div>
          <div className="flex items-center justify-between mb-2">
            <h3 className="text-sm font-semibold text-gray-700">Overall Progress</h3>
            <span className="text-sm font-bold text-gray-900">{progressPct}%</span>
          </div>
          <div className="w-full h-3 bg-gray-100 rounded-full overflow-hidden">
            <div
              className="h-full bg-green-500 rounded-full transition-all duration-500 ease-out"
              style={{ width: `${progressPct}%` }}
            />
          </div>
        </div>

        {/* Tasks by status */}
        <div>
          <h3 className="text-sm font-semibold text-gray-700 mb-3">Tasks by Status</h3>
          <div className="space-y-2.5">
            {Object.entries(STATUS_CONFIG).map(([status, cfg]) => {
              const count = tasksByStatus[status] ?? 0;
              const pct = totalTasks > 0 ? Math.round((count / totalTasks) * 100) : 0;

              return (
                <div key={status} className="flex items-center gap-3">
                  <span className={`shrink-0 w-2.5 h-2.5 rounded-full ${cfg.color}`} />
                  <span className="text-xs font-medium text-gray-600 w-24 truncate">
                    {cfg.label}
                  </span>
                  <div className="flex-1 h-2 bg-gray-100 rounded-full overflow-hidden">
                    <div
                      className={`h-full ${cfg.color} rounded-full transition-all duration-500 ease-out`}
                      style={{ width: `${pct}%` }}
                    />
                  </div>
                  <span className="text-xs font-semibold text-gray-700 w-8 text-right">
                    {count}
                  </span>
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
}
