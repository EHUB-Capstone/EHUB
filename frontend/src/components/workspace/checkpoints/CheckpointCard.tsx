// @ts-nocheck
import {
  Users,
  BarChart2,
  Layers,
  TrendingUp,
  FileText,
  ArrowRight,
  CheckCircle2,
} from 'lucide-react';

const ICONS = { Users, BarChart2, Layers, TrendingUp };

export default function CheckpointCard({
  checkpoint,
  submissionStats,
  onOpen,
  isLast = false,
}) {
  const Icon = ICONS[checkpoint.icon] || FileText;
  const stat = submissionStats[checkpoint.number] || {};
  const fileCount = stat.count || 0;
  const reqFilled = stat.reqFilled || 0;
  const reqTotal = stat.reqTotal || checkpoint.requirements?.length || 0;
  const latest = stat.latest || null;
  const hasFiles = fileCount > 0;
  const hasRequirements = reqFilled > 0;
  const hasSubmission = hasFiles || hasRequirements;

  return (
    <div className="relative pl-12 sm:pl-16">
      {!isLast && (
        <div
          className={`absolute bottom-[-16px] left-[19px] top-10 w-0.5 sm:left-[27px] ${
            hasSubmission ? 'bg-orange-300' : 'bg-slate-200'
          }`}
          aria-hidden
        />
      )}

      <div
        className={`absolute left-0 top-5 z-10 flex h-10 w-10 items-center justify-center rounded-full border-4 border-white text-sm font-bold shadow-sm sm:h-14 sm:w-14 sm:text-lg ${
          hasSubmission
            ? 'bg-orange-500 text-white ring-2 ring-orange-200'
            : 'bg-slate-100 text-slate-500 ring-2 ring-slate-200'
        }`}
      >
        {hasSubmission ? (
          <CheckCircle2 className="h-5 w-5 sm:h-6 sm:w-6" />
        ) : (
          checkpoint.number
        )}
      </div>

      <button
        type="button"
        onClick={onOpen}
        className={`group w-full rounded-xl border p-4 text-left transition-all focus:outline-none focus-visible:ring-2 focus-visible:ring-orange-400 focus-visible:ring-offset-2 sm:p-5 ${
          hasSubmission
            ? 'border-orange-200/80 bg-orange-50/40 hover:border-orange-300 hover:bg-orange-50/70 hover:shadow-md'
            : 'border-slate-200 bg-white hover:border-orange-200 hover:shadow-md'
        }`}
      >
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center">
          <div
            className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg ${
              hasSubmission
                ? 'bg-orange-100 text-orange-600'
                : 'bg-slate-100 text-slate-500 group-hover:bg-orange-50 group-hover:text-orange-600'
            }`}
          >
            <Icon className="h-5 w-5" />
          </div>

          <div className="min-w-0 flex-1">
            <p className="text-[10px] font-bold uppercase text-slate-400">
              Checkpoint {checkpoint.number}
            </p>
            <h3 className="mt-0.5 text-sm font-bold text-slate-900 transition-colors group-hover:text-orange-600 sm:text-base">
              {checkpoint.title}
            </h3>
            <p className="mt-1 text-xs leading-relaxed text-slate-500">
              {checkpoint.shortDescription}
            </p>

            <div className="mt-3 flex flex-wrap items-center gap-2">
              {hasRequirements && (
                <span className="rounded-md bg-emerald-100 px-2.5 py-1 text-[10px] font-bold text-emerald-700">
                  {reqFilled}/{reqTotal} requirements
                </span>
              )}
              {hasFiles && (
                <span className="rounded-md bg-orange-100 px-2.5 py-1 text-[10px] font-bold text-orange-700">
                  {fileCount} file{fileCount !== 1 ? 's' : ''}
                </span>
              )}
              {!hasSubmission && (
                <span className="rounded-md bg-slate-100 px-2.5 py-1 text-[10px] font-medium text-slate-500">
                  Not submitted
                </span>
              )}
              {hasFiles && latest && (
                <span className="flex min-w-0 max-w-full items-center gap-1 text-[10px] text-slate-400 sm:max-w-[240px]">
                  <FileText className="h-3 w-3 shrink-0 text-orange-400" />
                  <span className="truncate">{latest.originalName}</span>
                </span>
              )}
            </div>
          </div>

          <span className="inline-flex shrink-0 items-center gap-1 self-end text-xs font-bold text-slate-500 group-hover:text-orange-600 sm:self-center">
            Open
            <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5" />
          </span>
        </div>
      </button>
    </div>
  );
}
