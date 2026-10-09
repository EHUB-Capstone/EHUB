import { cn } from '../../utils/cn';

interface LoadingSkeletonProps {
  className?: string;
  lines?: number;
  variant?: 'text' | 'card' | 'table';
}

const LoadingSkeleton = ({ className, lines = 1, variant = 'text' }: LoadingSkeletonProps) => {
  if (variant === 'card') {
    return (
      <div className={cn('bg-white rounded-2xl border border-slate-200/60 p-6 animate-pulse', className)}>
        <div className="flex justify-between items-start mb-4">
          <div className="h-3 w-24 bg-slate-200 rounded" />
          <div className="w-9 h-9 bg-slate-100 rounded-xl" />
        </div>
        <div className="h-8 w-20 bg-slate-200 rounded mb-2" />
        <div className="h-3 w-32 bg-slate-100 rounded" />
      </div>
    );
  }

  if (variant === 'table') {
    return (
      <div className={cn('animate-pulse space-y-3', className)}>
        {Array.from({ length: lines }).map((_, i) => (
          <div key={i} className="flex gap-4 items-center py-3 px-4">
            <div className="w-8 h-8 bg-slate-100 rounded-lg" />
            <div className="flex-1 space-y-2">
              <div className="h-3 w-3/4 bg-slate-200 rounded" />
              <div className="h-2.5 w-1/2 bg-slate-100 rounded" />
            </div>
            <div className="h-3 w-16 bg-slate-100 rounded" />
          </div>
        ))}
      </div>
    );
  }

  return (
    <div className={cn('animate-pulse space-y-2.5', className)}>
      {Array.from({ length: lines }).map((_, i) => (
        <div key={i} className={cn('h-3 bg-slate-200 rounded', i === lines - 1 && lines > 1 ? 'w-3/4' : 'w-full')} />
      ))}
    </div>
  );
};

export default LoadingSkeleton;
