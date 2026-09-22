import { useEffect, useState } from 'react';
import { Columns2, FileWarning } from 'lucide-react';
import type {
  ProjectProposalAnalysisMatch,
  ProjectProposalContent,
} from '../../types/projectProposal';

type Props = {
  currentProposal: ProjectProposalContent;
  matches: ProjectProposalAnalysisMatch[];
};

type ComparisonSection = {
  label: string;
  scoreLabel?: string;
  score?: (match: ProjectProposalAnalysisMatch) => number;
  render: (proposal: ProjectProposalContent) => Array<{ label?: string; value: string }>;
};

const coreSections: ComparisonSection[] = [
  {
    label: 'Vấn đề',
    scoreLabel: 'Problem similarity',
    score: (match) => match.problemSimilarity,
    render: (proposal) => [{ value: proposal.problem }],
  },
  {
    label: 'Giải pháp',
    scoreLabel: 'Solution similarity',
    score: (match) => match.solutionSimilarity,
    render: (proposal) => [{ value: proposal.solution }],
  },
  {
    label: 'Khách hàng mục tiêu',
    scoreLabel: 'Target similarity',
    score: (match) => match.targetCustomerSimilarity,
    render: (proposal) => [{ value: proposal.targetCustomers }],
  },
  {
    label: 'Giá trị và cách tiếp cận',
    scoreLabel: 'Value & approach similarity',
    score: (match) => match.valueAndApproachSimilarity,
    render: (proposal) => [
      { label: 'Giá trị đề xuất', value: proposal.valueProposition },
      { label: 'Mô hình kinh doanh', value: proposal.businessModel },
      { label: 'Công nghệ', value: proposal.technology },
    ],
  },
];

const supportingSections: ComparisonSection[] = [
  { label: 'Thị trường', render: (proposal) => [{ value: proposal.marketSize }] },
  { label: 'Đối thủ và lợi thế', render: (proposal) => [{ value: proposal.competitors }] },
  { label: 'Doanh thu', render: (proposal) => [{ value: proposal.revenueModel }] },
  { label: 'Marketing và bán hàng', render: (proposal) => [{ value: proposal.marketingStrategy }] },
  { label: 'Kế hoạch tài chính', render: (proposal) => [{ value: proposal.financialPlan }] },
  { label: 'Lộ trình', render: (proposal) => [{ value: proposal.roadmap }] },
  { label: 'Đội ngũ', render: (proposal) => [{ value: proposal.teamIntroduction }] },
];

const similarityPercent = (value: number) => `${Math.max(0, value * 100).toFixed(1)}%`;

function ProposalValues({ values }: { values: Array<{ label?: string; value: string }> }) {
  return (
    <div className="space-y-3">
      {values.map((item, index) => (
        <div key={`${item.label || 'value'}-${index}`}>
          {item.label && <p className="text-[11px] font-bold uppercase tracking-wide text-slate-400">{item.label}</p>}
          <p className="mt-1 whitespace-pre-line text-sm leading-6 text-slate-700">
            {item.value.trim() || <span className="italic text-slate-400">Chưa cung cấp.</span>}
          </p>
        </div>
      ))}
    </div>
  );
}

function ComparisonRows({
  sections,
  currentProposal,
  match,
}: {
  sections: ComparisonSection[];
  currentProposal: ProjectProposalContent;
  match: ProjectProposalAnalysisMatch;
}) {
  const candidate = match.candidateProposal!;
  return (
    <div className="divide-y divide-slate-100">
      {sections.map((section) => (
        <section key={section.label} className="py-4 first:pt-0 last:pb-0">
          <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
            <h5 className="text-xs font-bold uppercase tracking-wider text-slate-600">{section.label}</h5>
            {section.score && section.scoreLabel && (
              <span className="rounded-full bg-indigo-50 px-2.5 py-1 text-[11px] font-semibold text-indigo-700" title={section.scoreLabel}>
                {section.scoreLabel}: {similarityPercent(section.score(match))}
              </span>
            )}
          </div>
          <div className="grid overflow-hidden rounded-xl border border-slate-200 md:grid-cols-2 md:divide-x md:divide-y-0">
            <div className="border-b border-slate-200 bg-white p-4 md:border-b-0">
              <ProposalValues values={section.render(currentProposal)} />
            </div>
            <div className="bg-slate-50/70 p-4">
              <ProposalValues values={section.render(candidate)} />
            </div>
          </div>
        </section>
      ))}
    </div>
  );
}

export default function ProposalSideBySideComparison({ currentProposal, matches }: Props) {
  const [selectedProposalVersionId, setSelectedProposalVersionId] = useState(matches[0]?.proposalVersionId || '');

  useEffect(() => {
    if (!matches.some((match) => match.proposalVersionId === selectedProposalVersionId)) {
      setSelectedProposalVersionId(matches[0]?.proposalVersionId || '');
    }
  }, [matches, selectedProposalVersionId]);

  const selectedMatch = matches.find((match) => match.proposalVersionId === selectedProposalVersionId) || matches[0];
  if (!selectedMatch) return null;

  return (
    <section className="mt-5 rounded-xl border border-indigo-200 bg-white p-4" aria-labelledby="proposal-side-by-side-title">
      <div className="flex items-start gap-2">
        <Columns2 className="mt-0.5 h-5 w-5 shrink-0 text-indigo-600" />
        <div>
          <h3 id="proposal-side-by-side-title" className="text-sm font-bold text-slate-900">So sánh nội dung song song</h3>
          <p className="mt-1 text-xs leading-5 text-slate-500">Đối chiếu snapshot tại thời điểm nộp. Nội dung này chỉ hỗ trợ giảng viên xem xét, không thay thế quyết định học thuật.</p>
        </div>
      </div>

      <div className="mt-4 flex gap-2 overflow-x-auto pb-1" role="group" aria-label="Chọn proposal đối sánh">
        {matches.map((match) => (
          <button
            key={match.proposalVersionId}
            type="button"
            aria-pressed={selectedMatch.proposalVersionId === match.proposalVersionId}
            onClick={() => setSelectedProposalVersionId(match.proposalVersionId)}
            className={`shrink-0 rounded-xl border px-3 py-2 text-left text-xs transition ${selectedMatch.proposalVersionId === match.proposalVersionId ? 'border-indigo-400 bg-indigo-50 text-indigo-900 ring-2 ring-indigo-100' : 'border-slate-200 bg-white text-slate-600 hover:border-indigo-200'}`}
          >
            <span className="block font-bold">#{match.rank} {match.startupName || match.title || 'Proposal chưa có tên'}</span>
            <span className="mt-0.5 block text-[11px] opacity-75">Hybrid {similarityPercent(match.hybridSimilarity)}</span>
          </button>
        ))}
      </div>

      {!selectedMatch.candidateProposal ? (
        <div className="mt-4 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900" role="status">
          <FileWarning className="mt-0.5 h-4 w-4 shrink-0" /> Snapshot lịch sử không tương thích nên chưa thể hiển thị nội dung song song. Điểm số và evidence đã lưu vẫn được giữ nguyên.
        </div>
      ) : (
        <>
          <div className="mt-4 grid overflow-hidden rounded-xl border border-slate-200 text-xs md:grid-cols-2 md:divide-x">
            <div className="bg-indigo-50 p-3 text-indigo-900">
              <p className="font-bold">Đề xuất hiện tại: {currentProposal.startupName || currentProposal.title || 'Chưa có tên'}</p>
              {currentProposal.title && <p className="mt-1 text-[11px] opacity-80">{currentProposal.title}</p>}
              {currentProposal.tagline && <p className="mt-1 text-[11px] italic opacity-70">“{currentProposal.tagline}”</p>}
            </div>
            <div className="border-t border-slate-200 bg-slate-100 p-3 text-slate-700 md:border-t-0">
              <p className="font-bold">Đề xuất đối sánh: {selectedMatch.candidateProposal.startupName || selectedMatch.candidateProposal.title || 'Chưa có tên'}</p>
              {selectedMatch.candidateProposal.title && <p className="mt-1 text-[11px] opacity-80">{selectedMatch.candidateProposal.title}</p>}
              {selectedMatch.candidateProposal.tagline && <p className="mt-1 text-[11px] italic opacity-70">“{selectedMatch.candidateProposal.tagline}”</p>}
            </div>
          </div>

          <div className="mt-4">
            <ComparisonRows sections={coreSections} currentProposal={currentProposal} match={selectedMatch} />
          </div>

          <details className="mt-4 rounded-xl border border-slate-200 bg-slate-50/40 p-4">
            <summary className="cursor-pointer text-xs font-bold uppercase tracking-wider text-slate-700">Xem các trường hỗ trợ khác</summary>
            <div className="mt-4">
              <ComparisonRows sections={supportingSections} currentProposal={currentProposal} match={selectedMatch} />
            </div>
          </details>

          {selectedMatch.evidence.length > 0 && (
            <div className="mt-4 rounded-xl border border-amber-100 bg-amber-50/60 p-4 text-xs text-slate-700">
              <h4 className="font-bold text-amber-900">Evidence đã được backend xác minh</h4>
              <ul className="mt-2 space-y-2">
                {selectedMatch.evidence.map((item, index) => (
                  <li key={`${item.source}-${index}-${item.quote}`}>
                    <span className="font-semibold text-amber-800">{item.source === 'Current' ? 'Đề xuất hiện tại' : 'Đề xuất đối sánh'}:</span> “{item.quote}”
                  </li>
                ))}
              </ul>
            </div>
          )}
        </>
      )}
    </section>
  );
}
