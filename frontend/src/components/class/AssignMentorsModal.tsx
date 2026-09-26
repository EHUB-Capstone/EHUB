import { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { X, Loader2, Star, Search, Check, Users } from 'lucide-react';
import { classApi } from '../../api/classApi';
import { teamApi } from '../../api/teamApi';
import { unwrapApiData } from '../../utils/classMappers';
import { parseApiError } from '../../utils/apiError';
import { normalizeManagedTeam } from '../../utils/teamManagement';
import type { ManagedTeam, MentorAssignment, MentorCandidate } from '../../types/teamManagement';
import type { ApiEnvelope } from '../../types/classes';

interface AssignMentorsModalProps {
  classId: string;
  currentMentors?: unknown[];
  onClose: () => void;
  onAssigned: () => Promise<void> | void;
}

interface MentorOption {
  _id: string;
  name: string;
  email: string;
  organization?: string | null;
  mentorType: 'Enterprise' | 'Academic';
  activeTeamCount: number;
}

export default function AssignMentorsModal({ classId, currentMentors: _currentMentors = [], onClose, onAssigned }: AssignMentorsModalProps) {
  const [mentors, setMentors] = useState<MentorOption[]>([]);
  const [teams, setTeams] = useState<ManagedTeam[]>([]);
  const [selectedMentorId, setSelectedMentorId] = useState('');
  const [selectedTeamId, setSelectedTeamId] = useState('');
  const [searchTerm, setSearchTerm] = useState('');
  const [loading, setLoading] = useState(true);
  const [teamsLoading, setTeamsLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [endingTeamId, setEndingTeamId] = useState('');

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [mentorRes, teamRes] = await Promise.all([
          classApi.getMentorCandidates(classId),
          classApi.getTeams(classId),
        ]);

        const mentorCandidates = unwrapApiData<MentorCandidate[]>(mentorRes as ApiEnvelope<MentorCandidate[]> | MentorCandidate[]) || [];
        const teamData = unwrapApiData(teamRes) || [];
        const mentorList = mentorCandidates.map(candidate => ({
          _id: candidate.mentor.mentorProfileId,
          name: candidate.mentor.fullName,
          email: candidate.mentor.email,
          organization: candidate.mentor.organization,
          mentorType: candidate.mentor.mentorType,
          activeTeamCount: candidate.activeTeamCount,
        }));
        const teamList = (Array.isArray(teamData) ? teamData : []).map(normalizeManagedTeam);

        setMentors(mentorList);
        setTeams(teamList);
      } catch {
        toast.error('Failed to load mentors or teams');
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, [classId]);

  const handleMentorChange = async (mentorId: string) => {
    setSelectedMentorId(mentorId);
    setSelectedTeamId('');

    if (!mentorId) return;

    setTeamsLoading(true);
    try {
      const res = await classApi.getTeams(classId);
      const teamData = unwrapApiData(res) || [];
      const teamList = (Array.isArray(teamData) ? teamData : []).map(normalizeManagedTeam);
      setTeams(teamList);
    } catch {
      toast.error('Failed to load teams');
    } finally {
      setTeamsLoading(false);
    }
  };

  const handleSubmit = async () => {
    if (!selectedMentorId) {
      toast.error('Please choose a mentor');
      return;
    }
    if (!selectedTeamId) {
      toast.error('Please choose a team');
      return;
    }

    setSubmitting(true);
    try {
      await teamApi.assignMentor(selectedTeamId, selectedMentorId);
      toast.success('Mentor assigned to team successfully!');
      onAssigned();
    } catch (e) {
      toast.error(parseApiError(e, 'Failed to assign mentor to team').message);
    } finally {
      setSubmitting(false);
    }
  };

  const handleEndAssignment = async (team: ManagedTeam, assignment: MentorAssignment) => {
    const reason = window.prompt(`Reason for ending ${assignment.mentor.fullName}'s ${assignment.slot} assignment?`);
    if (!reason) return;
    setEndingTeamId(assignment.assignmentId);
    try {
      await teamApi.endMentorAssignment(team._id, assignment.assignmentId, reason);
      toast.success('Mentor assignment ended');
      setTeams(current => current.map(item => item._id === team._id
        ? {
            ...item,
            currentMentorAssignments: (item.currentMentorAssignments || []).filter(currentAssignment => currentAssignment.assignmentId !== assignment.assignmentId),
            currentMentorAssignment: item.currentMentorAssignment?.assignmentId === assignment.assignmentId ? null : item.currentMentorAssignment,
          }
        : item));
      await onAssigned();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to end mentor assignment').message);
    } finally {
      setEndingTeamId('');
    }
  };

  const filtered = mentors.filter(m =>
    m.name?.toLowerCase().includes(searchTerm.toLowerCase()) ||
    m.email?.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const selectedMentor = mentors.find(m => m._id === selectedMentorId);
  const filteredTeams = teams.filter(team =>
    team.teamName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
    team.teamCode?.toLowerCase().includes(searchTerm.toLowerCase()) ||
    team.groupName?.toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-xs animate-fade-in" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-float w-full max-w-lg overflow-hidden animate-scale-in">
        {/* Header */}
        <div className="flex items-center justify-between p-5 border-b border-slate-100">
          <div>
            <h2 className="text-lg font-bold text-slate-900">Assign Mentor to Team</h2>
            <p className="text-xs text-slate-400 mt-0.5 font-medium">Choose a mentor first, then select a team</p>
          </div>
          <button onClick={onClose} className="p-2 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition-all">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <div className="p-5 space-y-4">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
            <input
              type="text"
              placeholder={selectedMentorId ? 'Search teams...' : 'Search mentors...'}
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="w-full pl-9 pr-4 py-2 border border-slate-200 rounded-xl text-xs outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            />
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-10">
              <Loader2 className="w-6 h-6 text-primary animate-spin" />
            </div>
          ) : (
            <div className="space-y-4">
              {teams.some(team => (team.currentMentorAssignments?.length || 0) > 0) && (
                <div>
                  <label className="mb-1.5 block text-xs font-semibold text-slate-600">Current assignments</label>
                  <div className="space-y-1.5 rounded-xl border border-slate-100 bg-slate-50/50 p-2.5">
                    {teams.flatMap(team => (team.currentMentorAssignments || []).map(assignment => (
                      <div key={assignment.assignmentId} className="flex items-center justify-between gap-3 rounded-lg bg-white px-3 py-2 text-xs">
                        <span className="min-w-0 truncate"><strong>{team.teamName}</strong> · {assignment.mentor.fullName} <span className="text-slate-400">({assignment.slot})</span></span>
                        <button type="button" disabled={endingTeamId === assignment.assignmentId} onClick={() => handleEndAssignment(team, assignment)} className="shrink-0 font-semibold text-red-600 hover:text-red-700 disabled:opacity-50">
                          {endingTeamId === assignment.assignmentId ? 'Ending…' : 'End'}
                        </button>
                      </div>
                    )))}
                  </div>
                </div>
              )}
              <div>
                <label className="block text-xs font-semibold text-slate-600 mb-1.5">Select Mentor</label>
                <div className="max-h-56 overflow-y-auto space-y-1.5 border border-slate-100 rounded-xl p-2.5 bg-slate-50/50">
                  {filtered.length === 0 ? (
                    <p className="text-xs text-slate-400 text-center py-6">No mentors found</p>
                  ) : (
                    filtered.map(m => {
                      const isChecked = selectedMentorId === m._id;
                      return (
                        <button
                          key={m._id}
                          type="button"
                          onClick={() => handleMentorChange(m._id)}
                          className={`w-full flex items-center justify-between p-2.5 rounded-xl border text-left transition-all ${
                            isChecked
                              ? 'bg-primary-50/40 border-primary/20 shadow-xs'
                              : 'bg-white border-slate-200/60 hover:bg-slate-50'
                          }`}
                        >
                          <div className="flex items-center gap-2.5 min-w-0">
                            <div className="w-8 h-8 rounded-full bg-amber-100 flex items-center justify-center text-amber-500 font-bold text-xs shrink-0">
                              {m.name?.charAt(0)?.toUpperCase() || 'M'}
                            </div>
                            <div className="min-w-0">
                              <p className="text-xs font-semibold text-slate-800 truncate">{m.name}</p>
                              <p className="text-[10px] text-slate-400 truncate">{m.email} · {m.mentorType} · {m.activeTeamCount} teams this semester</p>
                            </div>
                          </div>
                          {isChecked && (
                            <div className="w-5 h-5 rounded-full bg-primary flex items-center justify-center shrink-0">
                              <Check className="w-3 h-3 text-white" />
                            </div>
                          )}
                        </button>
                      );
                    })
                  )}
                </div>
              </div>

              {selectedMentorId && (
                <div>
                  <label className="block text-xs font-semibold text-slate-600 mb-1.5">Select Team</label>
                  {teamsLoading ? (
                    <div className="flex items-center justify-center py-6 border border-slate-100 rounded-xl bg-slate-50/50">
                      <Loader2 className="w-5 h-5 text-primary animate-spin" />
                    </div>
                  ) : (
                    <div className="max-h-56 overflow-y-auto space-y-1.5 border border-slate-100 rounded-xl p-2.5 bg-slate-50/50">
                      {filteredTeams.length === 0 ? (
                        <p className="text-xs text-slate-400 text-center py-6">No teams found</p>
                      ) : (
                        filteredTeams.map(team => {
                          const isChecked = selectedTeamId === team._id;
                          return (
                            <button
                              key={team._id}
                              type="button"
                              onClick={() => setSelectedTeamId(team._id)}
                              className={`w-full flex items-center justify-between p-2.5 rounded-xl border text-left transition-all ${
                                isChecked
                                  ? 'bg-primary-50/40 border-primary/20 shadow-xs'
                                  : 'bg-white border-slate-200/60 hover:bg-slate-50'
                              }`}
                            >
                              <div className="min-w-0 flex items-center gap-2.5">
                                <Users className="w-4 h-4 text-slate-400 shrink-0" />
                                <div className="min-w-0">
                                  <p className="text-xs font-semibold text-slate-800 truncate">{team.teamName || 'Unnamed Team'}</p>
                                  <p className="text-[10px] text-slate-400 truncate">{team.teamCode || team.groupName || '—'}</p>
                                </div>
                              </div>
                              {isChecked && (
                                <div className="w-5 h-5 rounded-full bg-primary flex items-center justify-center shrink-0">
                                  <Check className="w-3 h-3 text-white" />
                                </div>
                              )}
                            </button>
                          );
                        })
                      )}
                    </div>
                  )}
                </div>
              )}

              {selectedMentor && (
                <div className="flex items-center gap-2 text-xs text-slate-500 bg-slate-50 border border-slate-100 rounded-xl p-3">
                  <Star className="w-4 h-4 text-amber-500" />
                  <span className="font-medium">Selected mentor:</span>
                  <span className="text-slate-700 font-semibold">{selectedMentor.name}</span>
                </div>
              )}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="flex gap-3 p-5 pt-0">
          <button onClick={onClose} className="flex-1 px-4 py-2 border border-slate-200 rounded-xl text-xs font-semibold text-slate-500 hover:bg-slate-50 transition-all">
            Cancel
          </button>
          <button
            onClick={handleSubmit}
            disabled={submitting || loading || !selectedMentorId || !selectedTeamId}
            className="flex-1 px-4 py-2 bg-primary text-white rounded-xl text-xs font-semibold hover:bg-primary-700 disabled:opacity-50 transition-all flex items-center justify-center gap-1.5 shadow-sm"
          >
            {submitting ? <><Loader2 className="w-3.5 h-3.5 animate-spin" /> Saving...</> : 'Assign Mentor'}
          </button>
        </div>
      </div>
    </div>
  );
}
