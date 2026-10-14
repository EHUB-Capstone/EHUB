import React, { type ReactNode } from 'react';
import { FileSearch, type LucideIcon } from 'lucide-react';
import { cn } from '../../utils/cn';
import Button from './Button';

interface EmptyStateAction {
  label: string;
  onClick: () => void;
  isLoading?: boolean;
}

interface EmptyStateProps {
  icon?: LucideIcon;
  title?: string;
  description?: string;
  action?: ReactNode | EmptyStateAction;
  className?: string;
  size?: 'sm' | 'md';
}

const EmptyState = ({ icon: Icon = FileSearch, title = 'No data found', description = 'There is nothing to display at the moment.', action, className }: EmptyStateProps) => {
  const actionConfig = action && typeof action === 'object' && !React.isValidElement(action) && 'label' in action
    ? action as EmptyStateAction
    : null;

  return (
    <div className={cn('flex flex-col items-center justify-center py-16 px-6 text-center', className)}>
      <div className="w-16 h-16 rounded-2xl bg-primary-50 flex items-center justify-center mb-5">
        <Icon className="w-7 h-7 text-primary-700" aria-hidden="true" />
      </div>
      <h3 className="text-subheading text-secondary mb-2">{title}</h3>
      <p className="text-body text-slate-600 max-w-sm mb-6">{description}</p>
      {actionConfig ? (
        <Button variant="primary" onClick={actionConfig.onClick} isLoading={actionConfig.isLoading}>
          {actionConfig.label}
        </Button>
      ) : (
        action as ReactNode
      )}
    </div>
  );
};

export default EmptyState;
