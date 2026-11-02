import { forwardRef, useId, useImperativeHandle, useState } from 'react';
import { X } from 'lucide-react';
import EvaluationPanel from '../workspace/EvaluationPanel';

export interface GradingTarget {
  teamId: string;
  classCode: string;
  teamName: string;
  checkpointNumber: number;
  checkpointTitle: string;
  isReadOnly?: boolean;
}

export interface CheckpointGradingDialogHandle {
  open: (target: GradingTarget) => void;
}

interface CheckpointGradingDialogProps {
  titlePrefix?: string;
  // Called after the dialog closed, so the page can refresh its data in the background.
  onClosed?: () => void;
}

// The open/closed state lives here, not in the page: opening the dialog must not re-render the (large) page behind it.
const CheckpointGradingDialog = forwardRef<CheckpointGradingDialogHandle, CheckpointGradingDialogProps>(({
  titlePrefix = '', onClosed,
}, ref) => {
  const [target, setTarget] = useState<GradingTarget | null>(null);
  const titleId = useId();
  useImperativeHandle(ref, () => ({ open: setTarget }), []);

  if (!target) return null;

  const close = () => {
    setTarget(null);
    onClosed?.();
  };

  return (
    <div
      className="fixed inset-0 z-[70] flex animate-fade-in-fast items-center justify-center bg-slate-950/50 p-3 sm:p-6"
      role="dialog"
      aria-modal="true"
      aria-labelledby={titleId}
    >
      <div className="flex max-h-[94vh] w-full max-w-5xl animate-scale-in flex-col overflow-hidden rounded-2xl border border-slate-200 bg-slate-50 shadow-2xl">
        <div className="flex items-center justify-between gap-4 border-b border-slate-200 bg-white px-5 py-4">
          <div className="min-w-0">
            <p className="text-overline font-bold uppercase tracking-wider text-primary-700">{target.classCode} · {target.teamName}</p>
            <h2 id={titleId} className="truncate text-lg font-black text-slate-900">
              {titlePrefix}Checkpoint {target.checkpointNumber}: {target.checkpointTitle}
            </h2>
          </div>
          <button
            type="button"
            onClick={close}
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700"
            aria-label="Close grading dialog"
          >
            <X className="h-5 w-5" />
          </button>
        </div>
        <div className="overflow-y-auto">
          <EvaluationPanel
            teamId={target.teamId}
            proposalId={undefined}
            pitchDeckId={undefined}
            checkpointNumber={target.checkpointNumber}
            embedded
            isReadOnly={target.isReadOnly}
          />
        </div>
      </div>
    </div>
  );
});

CheckpointGradingDialog.displayName = 'CheckpointGradingDialog';
export default CheckpointGradingDialog;
