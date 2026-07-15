// @ts-nocheck
import React from 'react';
import { FileSearch } from 'lucide-react';
import { cn } from '../../utils/cn';
import Button from './Button';

const EmptyState = ({ icon: Icon = FileSearch, title = 'No data found', description = 'There is nothing to display at the moment.', action, className }) => {
  return (
    <div className={cn('flex flex-col items-center justify-center py-16 px-6 text-center', className)}>
      <div className="w-16 h-16 rounded-2xl bg-slate-100 flex items-center justify-center mb-5">
        <Icon className="w-7 h-7 text-slate-400" />
      </div>
      <h3 className="text-subheading text-slate-700 mb-2">{title}</h3>
      <p className="text-body text-slate-500 max-w-sm mb-6">{description}</p>
      {action && typeof action === 'object' && !React.isValidElement(action) ? (
        <Button variant="primary" onClick={action.onClick} isLoading={action.isLoading}>
          {action.label}
        </Button>
      ) : (
        action
      )}
    </div>
  );
};

export default EmptyState;
