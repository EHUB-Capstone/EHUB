import type { ReactNode } from 'react';
import { X } from 'lucide-react';
import Button from './Button';
import { useEffect } from 'react';

const modalSizes = {
  sm: 'max-w-sm',
  md: 'max-w-lg',
  lg: 'max-w-2xl',
  xl: 'max-w-4xl',
};

interface ModalProps {
  isOpen: boolean;
  onClose: () => void;
  title?: string;
  children?: ReactNode;
  submitText?: string;
  isSubmitting?: boolean;
  onSubmit?: () => void | Promise<void>;
  size?: keyof typeof modalSizes;
}

const Modal = ({ isOpen, onClose, title, children, submitText = 'Save', isSubmitting = false, onSubmit, size = 'md' }: ModalProps) => {
  // Prevent body scroll when modal is open
  useEffect(() => {
    if (isOpen) {
      document.body.style.overflow = 'hidden';
    }
    return () => {
      document.body.style.overflow = '';
    };
  }, [isOpen]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[60] flex items-end sm:items-center justify-center p-0 sm:p-6" role="dialog" aria-modal="true">
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-slate-900/40 backdrop-blur-sm"
        onClick={onClose}
        aria-hidden="true"
      />

      {/* Content */}
      <div className={`relative bg-white w-full ${modalSizes[size]} rounded-t-2xl sm:rounded-2xl shadow-float border border-slate-200/60 max-h-[90vh] flex flex-col animate-scale-in`}>
        {/* Header */}
        {title && (
          <div className="flex items-center justify-between px-5 sm:px-6 py-4 border-b border-slate-100 shrink-0">
            <h2 className="text-lg font-bold text-slate-900">{title}</h2>
            <button
              onClick={onClose}
              className="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition-all"
              aria-label="Close modal"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
        )}

        {/* Body */}
        <div className="flex-1 overflow-y-auto px-5 sm:px-6 py-5">
          {children}
        </div>

        {/* Footer */}
        {onSubmit && (
          <div className="border-t border-slate-100 px-5 sm:px-6 py-4 flex justify-end gap-3 shrink-0 bg-slate-50/50">
            <Button variant="outline" onClick={onClose} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button variant="gradient" onClick={onSubmit} isLoading={isSubmitting}>
              {submitText}
            </Button>
          </div>
        )}
      </div>
    </div>
  );
};

export default Modal;
