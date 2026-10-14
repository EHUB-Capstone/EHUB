import { AlertOctagon, RefreshCw } from 'lucide-react';
import Button from './Button';

interface ErrorStateProps {
  title?: string;
  message?: string;
  onRetry?: () => void;
}

const ErrorState = ({ title = 'Something went wrong', message = 'Failed to load data. Please try again.', onRetry }: ErrorStateProps) => {
  return (
    <div role="alert" className="flex flex-col items-center justify-center p-12 text-center h-full w-full bg-white rounded-2xl border border-slate-200 shadow-card">
      <div className="w-16 h-16 bg-danger-50 text-danger-dark flex items-center justify-center rounded-2xl mb-4">
        <AlertOctagon className="w-8 h-8" aria-hidden="true" />
      </div>
      <h3 className="text-heading text-secondary mb-2">{title}</h3>
      <p className="text-body-lg text-slate-600 max-w-md mx-auto mb-6">{message}</p>
      {onRetry && (
        <Button variant="outline" icon={RefreshCw} onClick={onRetry}>
          Try Again
        </Button>
      )}
    </div>
  );
};

export default ErrorState;
