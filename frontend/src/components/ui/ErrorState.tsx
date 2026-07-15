// @ts-nocheck
import { AlertOctagon, RefreshCw } from 'lucide-react';
import Button from './Button';

const ErrorState = ({ title = 'Something went wrong', message = 'Failed to load data. Please try again.', onRetry }) => {
  return (
    <div className="flex flex-col items-center justify-center p-12 text-center h-full w-full bg-white rounded-2xl border border-slate-200/60 shadow-sm">
      <div className="w-16 h-16 bg-red-50 text-red-500 flex items-center justify-center rounded-2xl mb-4">
        <AlertOctagon className="w-8 h-8" />
      </div>
      <h3 className="text-xl font-bold text-slate-900 mb-2">{title}</h3>
      <p className="text-slate-500 max-w-md mx-auto mb-6">{message}</p>
      {onRetry && (
        <Button variant="outline" icon={RefreshCw} onClick={onRetry}>
          Try Again
        </Button>
      )}
    </div>
  );
};

export default ErrorState;
