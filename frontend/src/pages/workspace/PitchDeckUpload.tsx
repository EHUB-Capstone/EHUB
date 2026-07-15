// @ts-nocheck
// frontend/src/pages/workspace/PitchDeckUpload.jsx
import { useState, useRef } from 'react';
import { UploadCloud, FileText, Download, Trash2, Loader2, Calendar } from 'lucide-react';
import toast from 'react-hot-toast';
import { workspaceApi } from '../../api/workspaceApi';

export default function PitchDeckUpload({ teamId, latestDeck, pitchDecks, isEditable, onRefresh }) {
  const [uploading, setUploading] = useState(false);
  const fileInputRef = useRef(null);

  const getWorkspaceLabel = (team) => {
    if (!team) return null;
    return [team.courseCode, team.semester, team.isArchived ? 'Archived' : null]
      .filter(Boolean)
      .join(' - ');
  };

  const isCurrentTeamDeck = (deck) => String(deck?.teamId?._id || deck?.teamId || '') === String(teamId || '');

  const handleFileChange = async (e) => {
    const file = e.target.files[0];
    if (!file) return;

    // Validate size (20MB)
    const maxSize = 20 * 1024 * 1024;
    if (file.size > maxSize) {
      toast.error('File size must be less than 20MB.');
      return;
    }

    // Validate extension
    const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();
    const allowed = ['.pdf', '.ppt', '.pptx'];
    if (!allowed.includes(ext)) {
      toast.error('Only PDF, PPT, and PPTX files are allowed.');
      return;
    }

    const formData = new FormData();
    formData.append('file', file);

    setUploading(true);
    try {
      await workspaceApi.uploadPitchDeck(teamId, formData);
      toast.success('Pitch deck uploaded successfully!');
      onRefresh();
    } catch (err) {
      console.error(err);
      toast.error(err.response?.data?.error || err.message || 'Upload failed');
    } finally {
      setUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  const handleDownload = (deckId) => {
    const url = workspaceApi.downloadPitchDeckUrl(deckId);
    // Open in a new tab to download
    window.open(url, '_blank');
  };

  const handleDelete = async (deckId) => {
    if (!window.confirm('Are you sure you want to delete/archive this pitch deck?')) return;
    try {
      await workspaceApi.deletePitchDeck(deckId);
      toast.success('Pitch deck archived successfully.');
      onRefresh();
    } catch (err) {
      toast.error(err.response?.data?.error || err.message || 'Failed to archive pitch deck');
    }
  };

  const formatBytes = (bytes, decimals = 2) => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const dm = decimals < 0 ? 0 : decimals;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
  };

  return (
    <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6 space-y-5">
      <div className="flex items-center justify-between border-b border-slate-100 pb-4">
        <div className="flex items-center gap-2">
          <UploadCloud className="w-5 h-5 text-primary" />
          <h2 className="text-lg font-bold text-slate-800">Pitch Deck</h2>
        </div>
        {latestDeck && (
          <span className="px-2 py-0.5 bg-primary-50 border border-primary-100 text-primary rounded-full text-xs font-bold">
            Ver {latestDeck.versionNumber} Active
          </span>
        )}
      </div>

      {/* Upload Box (Only for Student Members / Admin) */}
      {isEditable && (
        <div 
          onClick={() => !uploading && fileInputRef.current?.click()}
          className={`border-2 border-dashed border-slate-200 rounded-2xl p-6 text-center hover:bg-slate-50/50 hover:border-primary/50 transition-all cursor-pointer ${uploading ? 'pointer-events-none opacity-60' : ''}`}
        >
          <input 
            type="file" 
            ref={fileInputRef} 
            onChange={handleFileChange}
            accept=".pdf,.ppt,.pptx"
            className="hidden" 
          />
          {uploading ? (
            <div className="space-y-2">
              <Loader2 className="w-8 h-8 text-primary animate-spin mx-auto" />
              <p className="text-sm font-semibold text-slate-700">Uploading pitch deck...</p>
              <p className="text-xs text-slate-400">Please wait while the file is uploading.</p>
            </div>
          ) : (
            <div className="space-y-2">
              <UploadCloud className="w-8 h-8 text-slate-400 mx-auto" />
              <p className="text-sm font-semibold text-slate-700">Click to upload pitch deck</p>
              <p className="text-xs text-slate-400">Supported formats: PDF, PPT, PPTX (Max 20MB)</p>
            </div>
          )}
        </div>
      )}

      {/* Active Deck Detail */}
      {latestDeck ? (
        <div className="bg-slate-50 rounded-2xl p-5 border border-slate-100 flex items-start gap-4 justify-between">
          <div className="flex items-start gap-3 min-w-0">
            <div className="w-10 h-10 rounded-xl bg-primary-100 flex items-center justify-center shrink-0">
              <FileText className="w-5 h-5 text-primary" />
            </div>
            <div className="min-w-0">
              <h3 className="text-sm font-bold text-slate-800 truncate" title={latestDeck.originalName}>
                {latestDeck.originalName}
              </h3>
              <p className="text-xs text-slate-400 mt-1">{formatBytes(latestDeck.fileSize)} - Version {latestDeck.versionNumber}</p>
              <div className="flex items-center gap-3 mt-3 text-xs text-slate-400">
                <span className="flex items-center gap-1 shrink-0"><Calendar className="w-3.5 h-3.5" />{new Date(latestDeck.createdAt).toLocaleDateString()}</span>
                {getWorkspaceLabel(latestDeck.teamId) && (
                  <span className="font-semibold text-slate-500">{getWorkspaceLabel(latestDeck.teamId)}</span>
                )}
                {!isCurrentTeamDeck(latestDeck) && (
                  <span className="rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 font-semibold text-amber-700">
                    Inherited
                  </span>
                )}
              </div>
            </div>
          </div>

          <div className="flex gap-2">
            <button 
              onClick={() => handleDownload(latestDeck._id)}
              className="p-2 border border-slate-200 bg-white text-slate-600 rounded-xl hover:text-primary hover:border-primary/50 transition-all shadow-xs"
              title="Download File"
            >
              <Download className="w-4 h-4" />
            </button>
            {isEditable && isCurrentTeamDeck(latestDeck) && (
              <button 
                onClick={() => handleDelete(latestDeck._id)}
                className="p-2 border border-slate-200 bg-white text-slate-600 rounded-xl hover:text-red-600 hover:border-red-200 transition-all shadow-xs"
                title="Delete Deck"
              >
                <Trash2 className="w-4 h-4" />
              </button>
            )}
          </div>
        </div>
      ) : (
        <div className="text-center py-6 text-slate-400 text-sm">
          No pitch deck uploaded yet.
        </div>
      )}

      {/* History Decks (Previous versions) */}
      {pitchDecks && pitchDecks.length > 1 && (
        <div className="space-y-3 pt-3 border-t border-slate-100">
          <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider px-1">Upload History</h4>
          <div className="space-y-2 max-h-[160px] overflow-y-auto">
            {pitchDecks.slice(1).map(deck => (
              <div key={deck._id} className="flex items-center justify-between p-2.5 rounded-xl hover:bg-slate-50 border border-transparent hover:border-slate-100 transition-all">
                <div className="flex items-center gap-2.5 min-w-0">
                  <FileText className="w-4 h-4 text-slate-400 shrink-0" />
                  <div className="min-w-0">
                    <p className="text-xs font-semibold text-slate-700 truncate" title={deck.originalName}>
                      {deck.originalName}
                    </p>
                    <p className="text-[10px] text-slate-400">
                      {formatBytes(deck.fileSize)} - Version {deck.versionNumber}
                      {getWorkspaceLabel(deck.teamId) ? ` - ${getWorkspaceLabel(deck.teamId)}` : ''}
                    </p>
                  </div>
                </div>
                <div className="flex gap-1.5">
                  <button 
                    onClick={() => handleDownload(deck._id)}
                    className="p-1.5 border border-slate-200/50 bg-white text-slate-500 rounded-lg hover:text-primary transition-all"
                  >
                    <Download className="w-3.5 h-3.5" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
