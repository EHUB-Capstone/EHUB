// @ts-nocheck
import { cn } from '../../utils/cn';

const ProgressBar = ({ value = 0, max = 100, color = 'primary', size = 'md', showLabel = false, className }) => {
  const percent = Math.min(Math.max((value / max) * 100, 0), 100);

  const colors = {
    primary: 'bg-primary',
    secondary: 'bg-secondary',
    cyan: 'bg-cyan-500',
    success: 'bg-success',
    warning: 'bg-warning',
    danger: 'bg-danger',
    gradient: 'bg-gradient-to-r from-primary to-secondary',
  };

  const sizes = {
    xs: 'h-1',
    sm: 'h-1.5',
    md: 'h-2',
    lg: 'h-3',
  };

  return (
    <div className={cn('w-full', className)}>
      {showLabel && (
        <div className="flex justify-between items-center mb-1.5">
          <span className="text-caption text-slate-500">Progress</span>
          <span className="text-caption font-semibold text-slate-700 font-mono">{Math.round(percent)}%</span>
        </div>
      )}
      <div className={cn('w-full bg-slate-100 rounded-full overflow-hidden', sizes[size])}>
        <div
          className={cn('h-full rounded-full transition-all duration-700 ease-out', colors[color])}
          style={{ width: `${percent}%` }}
        />
      </div>
    </div>
  );
};

export default ProgressBar;
