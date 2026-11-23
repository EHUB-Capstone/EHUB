import { AlertOctagon, RefreshCw } from 'lucide-react';
import Button from './Button';
import { useTranslation } from 'react-i18next';

interface ErrorStateProps {
  title?: string;
  message?: string;
  onRetry?: () => void;
}

const ErrorState = ({ title, message, onRetry }: ErrorStateProps) => {
  const { t } = useTranslation();
  return (
    <div role="alert" className="flex flex-col items-center justify-center p-12 text-center h-full w-full bg-white rounded-2xl border border-slate-200 shadow-card">
      <div className="w-16 h-16 bg-danger-50 text-danger-dark flex items-center justify-center rounded-2xl mb-4">
        <AlertOctagon className="w-8 h-8" aria-hidden="true" />
      </div>
      <h3 className="text-heading text-secondary mb-2">{title ?? t('states.somethingWrong')}</h3>
      <p className="text-body-lg text-slate-600 max-w-md mx-auto mb-6">{message ?? t('states.loadFailed')}</p>
      {onRetry && (
        <Button variant="outline" icon={RefreshCw} onClick={onRetry}>
          {t('actions.retry')}
        </Button>
      )}
    </div>
  );
};

export default ErrorState;
