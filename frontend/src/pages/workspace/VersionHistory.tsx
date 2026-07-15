// @ts-nocheck
// frontend/src/pages/workspace/VersionHistory.jsx
import { useState } from 'react';
import { History, Eye, RotateCcw, Calendar, User } from 'lucide-react';
import toast from 'react-hot-toast';
import { workspaceApi } from '../../api/workspaceApi';
import Modal from '../../components/ui/Modal';
import Button from '../../components/ui/Button';

// Zero-dependency pure JS Word-level LCS Diffing Component
function DiffText({ oldText = '', newText = '' }) {
  const o = oldText || '';
  const n = newText || '';
  
  if (o === n) {
    return <span className="whitespace-pre-wrap">{n || '—'}</span>;
  }

  // Tokenize text into words & white-spaces so we retain format
  const words1 = o.split(/(\s+)/);
  const words2 = n.split(/(\s+)/);
  
  const n1 = words1.length;
  const n2 = words2.length;
  
  // DP table for LCS
  const dp = Array.from({ length: n1 + 1 }, () => Array(n2 + 1).fill(0));
  
  for (let i = 1; i <= n1; i++) {
    for (let j = 1; j <= n2; j++) {
      if (words1[i - 1] === words2[j - 1]) {
        dp[i][j] = dp[i - 1][j - 1] + 1;
      } else {
        dp[i][j] = Math.max(dp[i - 1][j], dp[i][j - 1]);
      }
    }
  }
  
  const result = [];
  let i = n1;
  let j = n2;
  
  while (i > 0 || j > 0) {
    if (i > 0 && j > 0 && words1[i - 1] === words2[j - 1]) {
      result.push({ value: words1[i - 1], type: 'equal' });
      i--;
      j--;
    } else if (j > 0 && (i === 0 || dp[i][j - 1] >= dp[i - 1][j])) {
      result.push({ value: words2[j - 1], type: 'added' });
      j--;
    } else {
      result.push({ value: words1[i - 1], type: 'removed' });
      i--;
    }
  }
  
  result.reverse();

  return (
    <span className="whitespace-pre-wrap">
      {result.map((part, index) => {
        if (part.type === 'added') {
          return (
            <ins 
              key={index} 
              className="bg-emerald-50 text-emerald-800 font-semibold px-1 py-0.5 rounded border border-emerald-200/50 no-underline inline-block sm:inline"
            >
              {part.value}
            </ins>
          );
        }
        if (part.type === 'removed') {
          return (
            <del 
              key={index} 
              className="bg-red-50 text-red-800 line-through px-1 py-0.5 rounded border border-red-200/50 opacity-70 inline-block sm:inline"
            >
              {part.value}
            </del>
          );
        }
        return <span key={index}>{part.value}</span>;
      })}
    </span>
  );
}

export default function VersionHistory({ proposalId, versions, isEditable, onRefresh }) {
  const [selectedVer, setSelectedVer] = useState(null);
  const [restoring, setRestoring] = useState(false);
  const [compareMode, setCompareMode] = useState(false);

  const getWorkspaceLabel = (team) => {
    if (!team) return null;
    return [team.courseCode, team.semester, team.isArchived ? 'Archived' : null]
      .filter(Boolean)
      .join(' - ');
  };

  const handleRestore = async (versionId, versionNumber) => {
    if (!proposalId) return;
    if (!window.confirm(`Are you sure you want to restore the proposal to Version ${versionNumber}? This will create a new draft.`)) return;
    setRestoring(true);
    try {
      await workspaceApi.restoreProposalVersion(proposalId, versionId);
      toast.success(`Proposal successfully restored to Version ${versionNumber}!`);
      onRefresh();
    } catch (err) {
      console.error(err);
      toast.error(err.response?.data?.error || err.message || 'Restore failed');
    } finally {
      setRestoring(false);
    }
  };

  const showDetail = (ver) => {
    setSelectedVer(ver);
    setCompareMode(false); // Reset compare mode whenever new version is viewed
  };

  // Find immediately preceding version in list for comparison (versions is sorted descending)
  const selectedIndex = versions && selectedVer
    ? versions.findIndex(v => v._id === selectedVer._id)
    : -1;
  const prevVersion = selectedIndex !== -1 && versions && selectedIndex + 1 < versions.length
    ? versions[selectedIndex + 1]
    : null;

  const showDiff = compareMode && prevVersion;

  return (
    <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-5 space-y-4">
      <div className="flex items-center justify-between border-b border-slate-100 pb-3">
        <div className="flex items-center gap-2">
          <History className="w-4 h-4 text-primary" />
          <h3 className="font-bold text-slate-800">Version History</h3>
        </div>
        <span className="text-xs bg-slate-100 text-slate-500 font-semibold px-2 py-0.5 rounded">
          {versions ? versions.length : 0} saves
        </span>
      </div>

      {versions && versions.length > 0 ? (
        <div className="space-y-3 max-h-[300px] overflow-y-auto pr-1">
          {versions.map((ver) => (
            <div 
              key={ver._id} 
              className="flex items-center justify-between p-2.5 rounded-xl bg-slate-50/40 border border-slate-100 hover:bg-slate-50 transition-all"
            >
              <div className="min-w-0 flex-1 pr-2">
                <div className="flex items-center gap-2">
                  <span className="text-xs font-bold text-primary">v{ver.versionNumber}</span>
                  <span className="text-[10px] text-slate-400 font-semibold flex items-center gap-0.5">
                    <Calendar className="w-3 h-3" /> {new Date(ver.createdAt).toLocaleDateString()}
                  </span>
                </div>
                <p className="text-xs font-medium text-slate-700 mt-1 truncate" title={ver.changeNote}>
                  {ver.changeNote || 'No change note'}
                </p>
                <p className="text-[10px] text-slate-400 mt-0.5 flex items-center gap-0.5">
                  <User className="w-2.5 h-2.5" /> {ver.changedBy?.name || 'Unknown'}
                </p>
                {getWorkspaceLabel(ver.teamId) && (
                  <p className="text-[10px] text-slate-500 mt-1 font-semibold">
                    {getWorkspaceLabel(ver.teamId)}
                  </p>
                )}
              </div>

              <div className="flex gap-1 shrink-0">
                <button
                  onClick={() => showDetail(ver)}
                  className="p-1.5 border border-slate-200/50 bg-white text-slate-500 rounded-lg hover:text-primary transition-all"
                  title="View Snapshot"
                >
                  <Eye className="w-3.5 h-3.5" />
                </button>
                {isEditable && proposalId && (
                  <button
                    disabled={restoring}
                    onClick={() => handleRestore(ver._id, ver.versionNumber)}
                    className="p-1.5 border border-slate-200/50 bg-white text-slate-500 rounded-lg hover:text-primary transition-all disabled:opacity-50"
                    title="Restore Version"
                  >
                    <RotateCcw className="w-3.5 h-3.5" />
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="text-center py-4 text-slate-400 text-sm italic">
          No version history yet.
        </div>
      )}

      {/* Snapshot Modal Detail */}
      <Modal
        isOpen={selectedVer !== null}
        onClose={() => setSelectedVer(null)}
        title={selectedVer ? `Proposal Snapshot v${selectedVer.versionNumber}` : ''}
        size="lg"
      >
        {selectedVer && (
          <div className="space-y-4">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-100 pb-4">
              <div>
                <p className="text-xs text-slate-400">Created by <span className="font-semibold text-slate-700">{selectedVer.changedBy?.name}</span> ({selectedVer.changedBy?.email})</p>
                <p className="text-xs text-slate-400 mt-0.5">Date: {new Date(selectedVer.createdAt).toLocaleString()}</p>
                {getWorkspaceLabel(selectedVer.teamId) && (
                  <p className="text-xs text-slate-400 mt-0.5">Workspace: {getWorkspaceLabel(selectedVer.teamId)}</p>
                )}
              </div>

              <div className="flex items-center gap-2">
                {prevVersion && (
                  <button
                    type="button"
                    onClick={() => setCompareMode(!compareMode)}
                    className={`flex items-center gap-1.5 px-3 py-1.5 rounded-xl border text-xs font-bold transition-all shadow-sm ${
                      compareMode 
                        ? 'bg-primary-50 text-primary border-primary-200 hover:bg-primary-100' 
                        : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-50'
                    }`}
                  >
                    <div className={`w-2 h-2 rounded-full ${compareMode ? 'bg-primary animate-pulse' : 'bg-slate-300'}`} />
                    Compare with v{prevVersion.versionNumber}
                  </button>
                )}
                <span className="text-xs bg-slate-50 border border-slate-200 text-slate-600 px-3 py-1.5 rounded-xl font-bold">
                  Note: {selectedVer.changeNote || 'No change note'}
                </span>
              </div>
            </div>

            <div className="space-y-4 text-slate-700 text-sm max-h-[55vh] overflow-y-auto pr-1">
              <div>
                <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-0.5">Startup Name</h4>
                <p className="font-bold text-slate-900 mt-0.5 text-base">
                  {showDiff ? (
                    <DiffText oldText={prevVersion.snapshot.startupName} newText={selectedVer.snapshot.startupName} />
                  ) : (
                    selectedVer.snapshot.startupName || '—'
                  )}
                </p>
              </div>
              <div>
                <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-0.5">Tagline</h4>
                <p className="italic text-slate-600 mt-0.5">
                  {showDiff ? (
                    <DiffText oldText={prevVersion.snapshot.tagline} newText={selectedVer.snapshot.tagline} />
                  ) : (
                    selectedVer.snapshot.tagline || '—'
                  )}
                </p>
              </div>
              <hr className="border-slate-100" />
              {[
                { key: 'problem', label: 'Problem' },
                { key: 'solution', label: 'Solution' },
                { key: 'targetCustomers', label: 'Target Customers' },
                { key: 'valueProposition', label: 'Value Proposition' },
                { key: 'marketSize', label: 'Market Size' },
                { key: 'competitors', label: 'Competitors' },
                { key: 'businessModel', label: 'Business Model' },
                { key: 'revenueModel', label: 'Revenue Model' },
                { key: 'marketingStrategy', label: 'Marketing Strategy' },
                { key: 'technology', label: 'Technology Stack' },
                { key: 'financialPlan', label: 'Financial Plan' },
                { key: 'roadmap', label: 'Roadmap' },
                { key: 'teamIntroduction', label: 'Team Introduction' },
              ].map(({ key, label }) => {
                const curVal = selectedVer.snapshot[key];
                const oldVal = prevVersion ? prevVersion.snapshot[key] : '';
                return (
                  <div key={key} className="bg-slate-50 p-3.5 rounded-xl border border-slate-100/60">
                    <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-1">{label}</h4>
                    {showDiff ? (
                      <div className="text-slate-700 leading-relaxed">
                        <DiffText oldText={oldVal} newText={curVal} />
                      </div>
                    ) : (
                      <p className="text-slate-700 whitespace-pre-line leading-relaxed">{curVal || '—'}</p>
                    )}
                  </div>
                );
              })}
            </div>

            <div className="pt-3 border-t border-slate-100 flex justify-end gap-2">
              <Button variant="outline" size="sm" onClick={() => setSelectedVer(null)}>
                Close
              </Button>
              {isEditable && proposalId && (
                <Button 
                  variant="primary" 
                  size="sm"
                  onClick={() => {
                    const verNum = selectedVer.versionNumber;
                    const verId = selectedVer._id;
                    setSelectedVer(null);
                    handleRestore(verId, verNum);
                  }}
                >
                  Restore this version
                </Button>
              )}
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
}
