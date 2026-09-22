import { useCallback, useEffect, useRef, useState } from 'react';
import { AlertTriangle, BrainCircuit, CheckCircle2, LoaderCircle, RefreshCw, ShieldAlert } from 'lucide-react';
import { workspaceApi } from '../../api/workspaceApi';
import type { ProjectProposalAnalysis, ProjectProposalOverlapRisk } from '../../types/projectProposal';

type Props = {
  jobId: string;
};

const riskLabel: Record<ProjectProposalOverlapRisk, string> = {
  InsufficientData: 'Chưa đủ dữ liệu',
  Low: 'Thấp',
  Medium: 'Trung bình',
  High: 'Cao',
};

export default function ProposalAnalysisPanel({ jobId }: Props) {
  const [analysis, setAnalysis] = useState<ProjectProposalAnalysis | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const abortRef = useRef<AbortController | null>(null);

  const load = useCallback(async (showLoading = false) => {
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    if (showLoading) setLoading(true);
    setError(false);
    try {
      const response = await workspaceApi.getProposalAnalysis(jobId, controller.signal);
      if (!controller.signal.aborted) {
        setAnalysis(response.data);
      }
    } catch {
      if (!controller.signal.aborted) setError(true);
    } finally {
      if (!controller.signal.aborted) setLoading(false);
    }
  }, [jobId]);

  useEffect(() => {
    void load(true);
    return () => abortRef.current?.abort();
  }, [load]);

  useEffect(() => {
    if (!analysis || !['Pending', 'Processing'].includes(analysis.status)) return;
    const timer = window.setInterval(() => void load(), 3_000);
    return () => window.clearInterval(timer);
  }, [analysis, load]);

  if (loading) {
    return (
      <div className="flex items-center gap-3 rounded-2xl border border-indigo-100 bg-indigo-50 p-5 text-sm text-indigo-800" role="status">
        <LoaderCircle className="h-5 w-5 animate-spin" /> Đang tải trạng thái phân tích đề xuất...
      </div>
    );
  }

  if (error || !analysis) {
    return (
      <div className="rounded-2xl border border-red-200 bg-red-50 p-5">
        <div className="flex items-center gap-2 text-sm font-bold text-red-800"><ShieldAlert className="h-4 w-4" /> Không thể tải báo cáo phân tích</div>
        <p className="mt-2 text-sm text-red-700">Vui lòng thử lại. Dữ liệu proposal không bị ảnh hưởng.</p>
        <button type="button" onClick={() => void load(true)} className="mt-3 inline-flex items-center gap-1.5 text-sm font-semibold text-red-800 hover:underline">
          <RefreshCw className="h-4 w-4" /> Thử lại
        </button>
      </div>
    );
  }

  if (analysis.status === 'Pending' || analysis.status === 'Processing') {
    return (
      <div className="rounded-2xl border border-indigo-100 bg-indigo-50 p-5" role="status">
        <div className="flex items-center gap-2 text-sm font-bold text-indigo-900">
          <LoaderCircle className="h-4 w-4 animate-spin" />
          {analysis.status === 'Pending' ? 'Đang chờ phân tích' : 'Đang phân tích đề xuất'}
        </div>
        <p className="mt-2 text-sm leading-6 text-indigo-700">Hệ thống tự cập nhật trạng thái. Bạn có thể rời trang mà không làm gián đoạn công việc.</p>
      </div>
    );
  }

  if (analysis.status === 'Failed' || !analysis.report) {
    return (
      <div className="rounded-2xl border border-amber-200 bg-amber-50 p-5">
        <div className="flex items-center gap-2 text-sm font-bold text-amber-900"><AlertTriangle className="h-4 w-4" /> Phân tích chưa hoàn tất</div>
        <p className="mt-2 text-sm leading-6 text-amber-800">Lỗi phân tích không làm mất proposal và không ảnh hưởng quy trình giảng viên xét duyệt.</p>
      </div>
    );
  }

  return (
    <div className="rounded-2xl border border-indigo-200 bg-gradient-to-br from-indigo-50 to-white p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2 text-sm font-bold text-indigo-950"><BrainCircuit className="h-5 w-5" /> Báo cáo phân tích đề xuất</div>
          <p className="mt-1 text-xs text-indigo-600">Kết quả tham khảo, không tự động duyệt hoặc từ chối đề xuất.</p>
        </div>
        <span className="rounded-full border border-indigo-200 bg-white px-3 py-1 text-xs font-bold text-indigo-700">
          Mức giao thoa: {riskLabel[analysis.report.overlapRisk]}
        </span>
      </div>

      <p className="mt-4 whitespace-pre-line text-sm leading-6 text-slate-700">{analysis.report.summary}</p>

      <div className="mt-5 grid gap-4 md:grid-cols-2">
        <div className="rounded-xl border border-emerald-100 bg-emerald-50/70 p-4">
          <h3 className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-emerald-800"><CheckCircle2 className="h-4 w-4" /> Điểm khác biệt tiềm năng</h3>
          <ul className="mt-3 space-y-2 text-sm leading-5 text-slate-700">
            {analysis.report.potentialDifferentiators.map((item) => <li key={item}>• {item}</li>)}
          </ul>
        </div>
        <div className="rounded-xl border border-amber-100 bg-amber-50/70 p-4">
          <h3 className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-amber-800"><AlertTriangle className="h-4 w-4" /> Giới hạn cần lưu ý</h3>
          <ul className="mt-3 space-y-2 text-sm leading-5 text-slate-700">
            {analysis.report.limitations.map((item) => <li key={item}>• {item}</li>)}
          </ul>
        </div>
      </div>

      <p className="mt-4 text-xs text-slate-400">Hoàn tất {new Date(analysis.report.generatedAtUtc).toLocaleString()} · {analysis.report.provider}/{analysis.report.model}</p>
    </div>
  );
}
