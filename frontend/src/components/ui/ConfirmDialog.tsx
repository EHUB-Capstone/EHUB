import Modal from './Modal';
import Button, { type ButtonVariant } from './Button';
import { AlertTriangle } from 'lucide-react';

interface ConfirmDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => void | Promise<void>;
  title: string;
  description: string;
  isSubmitting?: boolean;
  confirmText?: string;
  cancelText?: string;
  confirmVariant?: ButtonVariant;
  reason?: string;
  onReasonChange?: (value: string) => void;
  reasonLabel?: string;
  reasonRequired?: boolean;
  confirmDisabled?: boolean;
}

const ConfirmDialog = ({
  isOpen,
  onClose,
  onConfirm,
  title,
  description,
  isSubmitting,
  confirmText = 'Delete',
  cancelText = 'Cancel',
  confirmVariant = 'danger',
  reason,
  onReasonChange,
  reasonLabel = 'Reason',
  reasonRequired = false,
  confirmDisabled = false,
}: ConfirmDialogProps) => {
  const reasonInvalid = reasonRequired && (reason?.trim().length ?? 0) < 3;
  return (
    <Modal isOpen={isOpen} onClose={onClose} title="">
      <div className="flex flex-col items-center text-center space-y-4 pt-4">
        <div className="w-16 h-16 bg-red-100 rounded-full flex items-center justify-center mb-2">
          <AlertTriangle className="w-8 h-8 text-red-500" />
        </div>
        <h3 className="text-xl font-bold text-slate-900">{title}</h3>
        <p className="text-slate-500">{description}</p>
        {onReasonChange && (
          <label className="w-full text-left">
            <span className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-500">{reasonLabel}</span>
            <textarea
              value={reason ?? ''}
              onChange={(event) => onReasonChange(event.target.value)}
              rows={3}
              maxLength={500}
              placeholder="Enter a clear reason for the audit trail"
              className="w-full resize-none rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15"
            />
            {reasonInvalid && <span className="mt-1 block text-xs text-amber-600">Reason must contain at least 3 characters.</span>}
          </label>
        )}
        
        <div className="flex gap-3 w-full pt-4">
          <Button variant="outline" className="flex-1" onClick={onClose} disabled={isSubmitting}>
            {cancelText}
          </Button>
          <Button
            variant={confirmVariant}
            className="flex-1"
            onClick={onConfirm}
            isLoading={isSubmitting}
            disabled={isSubmitting || reasonInvalid || confirmDisabled}
          >
            {confirmText}
          </Button>
        </div>
      </div>
    </Modal>
  );
};

export default ConfirmDialog;
