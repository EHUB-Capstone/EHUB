// @ts-nocheck
export default function BoardSkeleton() {
  return (
    <div className="space-y-5 animate-pulse">
      <div className="rounded-xl border border-slate-200 bg-white p-5">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div className="space-y-2">
            <div className="h-6 w-56 rounded bg-slate-200" />
            <div className="h-3 w-80 max-w-full rounded bg-slate-100" />
          </div>
          <div className="h-9 w-32 rounded-lg bg-slate-200" />
        </div>
        <div className="mt-5 grid grid-cols-2 gap-3 md:grid-cols-5">
          {Array.from({ length: 5 }).map((_, index) => (
            <div key={index} className="h-20 rounded-lg border border-slate-100 bg-slate-50" />
          ))}
        </div>
        <div className="mt-5 flex flex-wrap gap-3 border-t border-slate-100 pt-5">
          <div className="h-10 min-w-[220px] flex-1 rounded-lg bg-slate-100" />
          <div className="h-10 w-28 rounded-lg bg-slate-100" />
          <div className="h-10 w-36 rounded-lg bg-slate-100" />
          <div className="h-10 w-32 rounded-lg bg-slate-100" />
        </div>
      </div>

      <div className="hidden gap-4 md:grid md:grid-cols-5">
        {Array.from({ length: 5 }).map((_, column) => (
          <div key={column} className="rounded-xl border border-slate-200 bg-slate-50">
            <div className="flex items-center justify-between border-b border-slate-200 bg-white p-3">
              <div className="h-4 w-24 rounded bg-slate-200" />
              <div className="h-5 w-8 rounded-full bg-slate-100" />
            </div>
            <div className="space-y-3 p-3">
              {Array.from({ length: 3 }).map((_, card) => (
                <div key={card} className="rounded-lg border border-slate-200 bg-white p-3">
                  <div className="mb-3 flex gap-2">
                    <div className="h-5 w-16 rounded-full bg-slate-100" />
                    <div className="h-5 w-14 rounded-full bg-slate-100" />
                  </div>
                  <div className="mb-2 h-4 w-4/5 rounded bg-slate-200" />
                  <div className="h-3 w-3/5 rounded bg-slate-100" />
                </div>
              ))}
            </div>
          </div>
        ))}
      </div>

      <div className="space-y-3 md:hidden">
        {Array.from({ length: 4 }).map((_, index) => (
          <div key={index} className="h-24 rounded-lg border border-slate-200 bg-white" />
        ))}
      </div>
    </div>
  );
}
