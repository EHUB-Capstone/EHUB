// @ts-nocheck
import { X, Calendar, Clock, MapPin, Link2, Monitor, Globe, Users } from 'lucide-react';
import React from 'react';

const WorkshopPreviewModal = ({
  isOpen,
  onClose,
  onConfirm,
  workshopData,
  availableClasses,
  isEditing,
  loading
}) => {
  if (!isOpen) return null;

  const {
    title,
    description,
    type,
    targetAudience,
    onlineClassIds,
    offlineClassIds,
    startDate,
    endDate,
    startTime,
    endTime,
    location,
    meetingLink,
    format
  } = workshopData;

  const getClassName = (id) => {
    const cls = availableClasses.find(c => c._id === id);
    return cls ? `${cls.classCode} - ${cls.subjectCode}` : 'Unknown Class';
  };

  const getDerivedFormatBadge = () => {
    switch (format) {
      case 'ONLINE': return <span className="bg-indigo-100 text-indigo-700 px-2 py-0.5 rounded text-xs font-bold">ONLINE</span>;
      case 'OFFLINE': return <span className="bg-amber-100 text-amber-700 px-2 py-0.5 rounded text-xs font-bold">OFFLINE</span>;
      case 'HYBRID': return <span className="bg-emerald-100 text-emerald-700 px-2 py-0.5 rounded text-xs font-bold">HYBRID</span>;
      default: return null;
    }
  };

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-[60] flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl w-full max-w-2xl border border-slate-200/60 shadow-2xl flex flex-col max-h-[90vh]">

        {/* Header */}
        <div className="flex justify-between items-center px-6 py-4 border-b border-slate-100 shrink-0">
          <h2 className="text-xl font-bold text-slate-800">
            Workshop Summary
          </h2>
          <button
            onClick={onClose}
            className="p-1.5 hover:bg-slate-100 rounded-lg text-slate-400 hover:text-slate-600 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-6 space-y-6">

          {/* Basic Info */}
          <div>
            <div className="flex items-center gap-2 mb-1">
              <h3 className="text-lg font-bold text-slate-900">{title}</h3>
              {getDerivedFormatBadge()}
              <span className="bg-slate-100 text-slate-600 px-2 py-0.5 rounded text-xs font-semibold">{type}</span>
            </div>
            {description && <p className="text-sm text-slate-600 mt-2 whitespace-pre-wrap">{description}</p>}
          </div>

          {/* Time & Place */}
          <div className="bg-slate-50 rounded-xl p-4 border border-slate-100 grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="space-y-3">
              <div className="flex items-start gap-2">
                <Calendar className="w-4 h-4 text-slate-400 mt-0.5" />
                <div>
                  <p className="text-xs font-semibold text-slate-500 uppercase">Date</p>
                  <p className="text-sm text-slate-800">{startDate} to {endDate}</p>
                </div>
              </div>
              <div className="flex items-start gap-2">
                <Clock className="w-4 h-4 text-slate-400 mt-0.5" />
                <div>
                  <p className="text-xs font-semibold text-slate-500 uppercase">Time</p>
                  <p className="text-sm text-slate-800">{startTime} - {endTime}</p>
                </div>
              </div>
            </div>
            <div className="space-y-3">
              {(format === 'ONLINE' || format === 'HYBRID') && (
                <div className="flex items-start gap-2">
                  <Link2 className="w-4 h-4 text-indigo-500 mt-0.5" />
                  <div>
                    <p className="text-xs font-semibold text-indigo-600/80 uppercase">Meeting URL</p>
                    <p className="text-sm text-slate-800 truncate max-w-[200px]" title={meetingLink}>
                      {meetingLink || 'Not provided'}
                    </p>
                  </div>
                </div>
              )}
              {(format === 'OFFLINE' || format === 'HYBRID') && (
                <div className="flex items-start gap-2">
                  <MapPin className="w-4 h-4 text-amber-500 mt-0.5" />
                  <div>
                    <p className="text-xs font-semibold text-amber-600/80 uppercase">Location</p>
                    <p className="text-sm text-slate-800">{location || 'Not provided'}</p>
                  </div>
                </div>
              )}
            </div>
          </div>

          {/* Participants */}
          {targetAudience === 'CLASS' || targetAudience === 'TEAM' || targetAudience === 'LECTURER' || targetAudience === 'MENTOR' ? (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">

              {/* Online Classes */}
              <div className="border border-indigo-100 rounded-xl bg-indigo-50/30 overflow-hidden flex flex-col">
                <div className="bg-indigo-50 px-3 py-2 border-b border-indigo-100 flex justify-between items-center shrink-0">
                  <div className="flex items-center gap-1.5 text-xs font-bold text-indigo-700 uppercase">
                    <Monitor className="w-3.5 h-3.5" /> Online Classes
                  </div>
                  <span className="bg-indigo-200 text-indigo-800 text-[10px] px-1.5 py-0.5 rounded-full font-bold">
                    {onlineClassIds.length}
                  </span>
                </div>
                <div className="p-3 max-h-[150px] overflow-y-auto space-y-1.5">
                  {onlineClassIds.length > 0 ? (
                    onlineClassIds.map(id => (
                      <div key={id} className="text-xs text-slate-700 flex items-center gap-2">
                        <span className="w-1.5 h-1.5 rounded-full bg-indigo-400"></span>
                        {getClassName(id)}
                      </div>
                    ))
                  ) : (
                    <p className="text-xs text-slate-400 italic text-center py-2">No online classes</p>
                  )}
                </div>
              </div>

              {/* Offline Classes */}
              <div className="border border-amber-100 rounded-xl bg-amber-50/30 overflow-hidden flex flex-col">
                <div className="bg-amber-50 px-3 py-2 border-b border-amber-100 flex justify-between items-center shrink-0">
                  <div className="flex items-center gap-1.5 text-xs font-bold text-amber-700 uppercase">
                    <Globe className="w-3.5 h-3.5" /> Offline Classes
                  </div>
                  <span className="bg-amber-200 text-amber-800 text-[10px] px-1.5 py-0.5 rounded-full font-bold">
                    {offlineClassIds.length}
                  </span>
                </div>
                <div className="p-3 max-h-[150px] overflow-y-auto space-y-1.5">
                  {offlineClassIds.length > 0 ? (
                    offlineClassIds.map(id => (
                      <div key={id} className="text-xs text-slate-700 flex items-center gap-2">
                        <span className="w-1.5 h-1.5 rounded-full bg-amber-400"></span>
                        {getClassName(id)}
                      </div>
                    ))
                  ) : (
                    <p className="text-xs text-slate-400 italic text-center py-2">No offline classes</p>
                  )}
                </div>
              </div>

            </div>
          ) : (
            <div className="bg-slate-50 p-4 rounded-xl border border-slate-100 flex items-center gap-2 text-sm text-slate-600">
              <Users className="w-4 h-4" />
              Audience Scope: <span className="font-semibold">{targetAudience}</span>
            </div>
          )}

        </div>

        {/* Footer */}
        <div className="flex justify-end gap-3 px-6 py-4 border-t border-slate-100 bg-slate-50/50 shrink-0 rounded-b-2xl">
          <button
            type="button"
            onClick={onClose}
            disabled={loading}
            className="px-5 py-2.5 border border-slate-200 rounded-xl text-sm font-semibold text-slate-600 hover:bg-slate-100 transition-colors disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={onConfirm}
            disabled={loading}
            className="bg-primary text-white font-semibold px-6 py-2.5 rounded-xl text-sm hover:shadow-glow-primary active:scale-[0.98] transition-all disabled:opacity-50"
          >
            {loading ? 'Processing...' : (isEditing ? 'Confirm Update Workshop' : 'Confirm Create Workshop')}
          </button>
        </div>
      </div>
    </div>
  );
};

export default WorkshopPreviewModal;
