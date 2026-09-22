import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { RefreshCw } from 'lucide-react';
import { featureApi } from '../api/featureApi';

type Props = {
  children: ReactNode;
};

export function RuntimeAiFeatureRoute({ children }: Props) {
  const [enabled, setEnabled] = useState<boolean | null>(null);
  const [failed, setFailed] = useState(false);
  const abortRef = useRef<AbortController | null>(null);

  const load = useCallback(async () => {
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    setFailed(false);
    setEnabled(null);
    try {
      const response = await featureApi.getAvailability(controller.signal);
      if (!controller.signal.aborted) setEnabled(Boolean(response.data?.aiEnabled));
    } catch {
      if (!controller.signal.aborted) setFailed(true);
    }
  }, []);

  useEffect(() => {
    void load();
    return () => abortRef.current?.abort();
  }, [load]);

  if (failed) {
    return (
      <div className="mx-auto mt-12 max-w-lg rounded-2xl border border-amber-200 bg-amber-50 p-6 text-center">
        <p className="text-sm text-amber-900">Không thể kiểm tra trạng thái tính năng AI.</p>
        <button type="button" onClick={() => void load()} className="mt-3 inline-flex items-center gap-1.5 text-sm font-semibold text-amber-900 hover:underline">
          <RefreshCw className="h-4 w-4" /> Thử lại
        </button>
      </div>
    );
  }

  if (enabled === null) {
    return <div className="flex min-h-56 items-center justify-center text-sm text-slate-500" role="status">Đang kiểm tra tính năng AI...</div>;
  }

  return enabled ? children : <Navigate to="/student/workspace" replace />;
}
