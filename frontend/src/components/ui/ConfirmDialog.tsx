// @ts-nocheck
import Modal from './Modal';
import Button from './Button';
import { AlertTriangle } from 'lucide-react';

const ConfirmDialog = ({
  isOpen,
  onClose,
  onConfirm,
  title,
  description,
  isSubmitting,
  confirmText = 'Delete',
  cancelText = 'Cancel',
}) => {
  return (
    <Modal isOpen={isOpen} onClose={onClose} title="">
      <div className="flex flex-col items-center text-center space-y-4 pt-4">
        <div className="w-16 h-16 bg-red-100 rounded-full flex items-center justify-center mb-2">
          <AlertTriangle className="w-8 h-8 text-red-500" />
        </div>
        <h3 className="text-xl font-bold text-slate-900">{title}</h3>
        <p className="text-slate-500">{description}</p>
        
        <div className="flex gap-3 w-full pt-4">
          <Button variant="outline" className="flex-1" onClick={onClose} disabled={isSubmitting}>
            {cancelText}
          </Button>
          <Button
            variant="primary"
            className="flex-1 bg-red-500 hover:bg-red-600 focus:ring-red-500/20"
            onClick={onConfirm}
            isLoading={isSubmitting}
          >
            {confirmText}
          </Button>
        </div>
      </div>
    </Modal>
  );
};

export default ConfirmDialog;
