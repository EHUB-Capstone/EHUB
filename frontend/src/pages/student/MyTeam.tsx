// @ts-nocheck
import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Users, Shield, MessageSquare, AlertCircle, Calendar, Star, Loader2, Sparkles } from 'lucide-react';
import toast from 'react-hot-toast';
import { classApi } from '../../api/classApi';
import EmptyState from '../../components/ui/EmptyState';
import { getDisplayTeamName } from '../../utils/teamDisplay';
import { unwrapApiData } from '../../utils/classMappers';
import { getTeamMembers, normalizeManagedTeam } from '../../utils/teamManagement';

const majorColor = (major) => {
  const palette = [
    'bg-blue-50 text-blue-700 border-blue-100',
    'bg-purple-50 text-purple-700 border-purple-100',
    'bg-cyan-50 text-cyan-700 border-cyan-100',
    'bg-orange-50 text-orange-700 border-orange-100',
    'bg-pink-50 text-pink-700 border-pink-100',
    'bg-teal-50 text-teal-700 border-teal-100',
  ];
  let h = 0;
  for (const c of (major || '')) h = c.charCodeAt(0) + ((h << 5) - h);
  return palette[Math.abs(h) % palette.length];
};

export default function MyTeam() {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    const fetchTeam = async () => {
      try {
        const res = await classApi.getMyTeam();
        const payload = unwrapApiData(res);
        if (!payload?.team) {
          setData(payload || null);
        } else {
          const team = normalizeManagedTeam(payload.team);
          setData({ ...payload, team, members: getTeamMembers(team) });
        }
      } catch (err) {
        toast.error(err?.message || 'Failed to load your team details');
      } finally {
        setLoading(false);
      }
    };
    fetchTeam();
  }, []);

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[50vh]">
        <Loader2 className="w-8 h-8 text-primary animate-spin" />
        <p className="text-sm text-slate-400 mt-2 font-medium">Loading your team details...</p>
      </div>
    );
  }

  // Safe checks
  const team      = data?.team;
  const cls       = data?.class;
  const members   = Array.isArray(data?.members) ? data.members : [];
  const chatGroup = data?.chatGroup || data?.team?.chatGroupId || data?.team?.chatGroup;
  const lecturer  = cls?.lectureId;
  const mentor    = team?.currentMentorAssignment?.mentor;
  const displayTeamName = getDisplayTeamName(team) || 'Unnamed Team';

  if (!team) {
    return (
      <div className="max-w-4xl mx-auto space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">My Team</h1>
          <p className="text-sm text-slate-500 mt-1">View and collaborate with your startup team members.</p>
        </div>

        <div className="bg-white rounded-2xl border border-slate-200/60 p-8 shadow-sm">
          <EmptyState
            icon={Users}
            title="You have not been assigned to a team yet"
            description="Once your lecturer generates teams or assigns you to one, your team card and members will show up here."
          />
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex justify-between items-start flex-wrap gap-4">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">My Team</h1>
          <p className="text-sm text-slate-500 mt-1">
            Collaborate with your startup team in class <span className="font-semibold text-slate-700">{cls?.classCode || '—'}</span>.
          </p>
        </div>

        <div className="flex items-center gap-1.5 text-xs font-semibold text-slate-500 bg-slate-50 border border-slate-200/40 rounded-xl px-3 py-1.5">
          <Calendar className="w-3.5 h-3.5 text-slate-400" />
          <span>Semester: {cls?.semester || '—'} {cls?.year || ''}</span>
        </div>
      </div>

      {/* Main Grid Layout */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {/* Left 2 Columns: Team Info & Members List */}
        <div className="md:col-span-2 space-y-6">
          {/* Team Profile Glass Card */}
          <div className="bg-gradient-to-br from-white to-slate-50/50 rounded-2xl border border-slate-200/60 p-6 shadow-sm space-y-4 relative overflow-hidden group">
            <div className="absolute right-0 top-0 w-32 h-32 bg-primary-100/10 rounded-full blur-2xl group-hover:bg-primary-100/20 transition-all duration-500" />
            
            <div className="flex items-center gap-4">
              <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-primary to-secondary flex items-center justify-center shadow-sm">
                <Users className="w-6 h-6 text-white" />
              </div>
              <div>
                <h2 className="text-2xl font-bold text-slate-900 leading-tight">{displayTeamName}</h2>
                <p className="text-xs text-slate-400 font-mono mt-0.5 tracking-wider">{team.teamCode}</p>
              </div>
            </div>

            {team.description && (
              <p className="text-sm text-slate-500 italic bg-white border border-slate-100 rounded-xl p-3">
                &ldquo;{team.description}&rdquo;
              </p>
            )}

            {/* Chat status */}
            <div className="flex items-center justify-between p-3 rounded-xl border border-slate-100 bg-white/80 backdrop-blur-xs">
              <div className="flex items-center gap-2">
                <div className={`w-2.5 h-2.5 rounded-full ${chatGroup ? 'bg-green-500 animate-pulse' : 'bg-slate-300'}`} />
                <span className="text-xs font-semibold text-slate-600">
                  {chatGroup ? 'Team Chat Active' : 'Team Chat not configured'}
                </span>
              </div>
              {chatGroup && (
                <button
                  onClick={() => navigate(`/chat?groupId=${chatGroup._id}`)}
                  className="flex items-center gap-1.5 px-3 py-1.5 bg-primary text-white hover:bg-primary-600 active:scale-95 transition-all text-xs font-bold rounded-lg cursor-pointer shadow-xs"
                >
                  <MessageSquare className="w-3.5 h-3.5" />
                  Chat Now
                </button>
              )}
            </div>

            {/* Startup Workspace Link */}
            <div className="pt-2">
              <button
                onClick={() => navigate('/student/workspace')}
                className="w-full flex items-center justify-center gap-2 px-4 py-2.5 bg-gradient-to-r from-primary to-secondary text-white hover:opacity-95 hover:scale-[1.01] active:scale-95 transition-all text-sm font-bold rounded-xl cursor-pointer shadow-sm"
              >
                <Sparkles className="w-4 h-4 animate-pulse" />
                Open Startup Workspace
              </button>
            </div>
          </div>

          {/* Members List */}
          <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm overflow-hidden">
            <div className="p-4 border-b border-slate-100 bg-slate-50/50">
              <h3 className="font-bold text-slate-800 flex items-center gap-2 text-sm">
                <Users className="w-4 h-4 text-primary" /> Team Members ({members.length})
              </h3>
            </div>

            <div className="divide-y divide-slate-100">
              {members.map((m) => {
                const isSelected = m._id === data?.student?._id;
                return (
                  <div
                    key={m._id}
                    className={`p-4 flex items-center justify-between gap-4 transition-colors hover:bg-slate-50/50 ${
                      isSelected ? 'bg-primary-50/20' : ''
                    }`}
                  >
                    <div className="flex items-center gap-3">
                      <div className="w-9 h-9 rounded-full bg-gradient-to-br from-secondary-300 to-secondary flex items-center justify-center text-white text-xs font-bold shrink-0">
                        {m.fullName?.charAt(0)?.toUpperCase() || '?'}
                      </div>
                      <div className="min-w-0">
                        <div className="flex items-center gap-2">
                          <span className="font-semibold text-slate-800 text-sm truncate">{m.fullName}</span>
                          {isSelected && (
                            <span className="px-1.5 py-0.5 bg-primary-100 text-primary text-[10px] font-bold rounded-md uppercase">
                              You
                            </span>
                          )}
                        </div>
                        <p className="text-xs text-slate-400 font-mono mt-0.5">{m.rollNumber} · {m.email}</p>
                      </div>
                    </div>

                    <div className="flex items-center gap-2 shrink-0">
                      {m.major && (
                        <span className={`text-[10px] font-bold uppercase px-2 py-0.5 rounded-full border ${majorColor(m.major)}`}>
                          {m.major}
                        </span>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>

        {/* Right 1 Column: Lecturer & Mentor Info */}
        <div className="space-y-6">
          {/* Lecturer Assign card */}
          {lecturer && (
            <div className="bg-white rounded-2xl border border-slate-200/60 p-5 shadow-sm space-y-4">
              <div className="flex items-center gap-2">
                <Shield className="w-4 h-4 text-secondary" />
                <h3 className="font-bold text-slate-800 text-xs uppercase tracking-wider">Class Lecturer</h3>
              </div>
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-full bg-secondary-100 flex items-center justify-center text-secondary font-bold text-sm shrink-0">
                  {lecturer.name?.charAt(0)?.toUpperCase() || 'L'}
                </div>
                <div className="min-w-0">
                  <p className="font-bold text-slate-800 text-sm truncate">{lecturer.name}</p>
                  <p className="text-xs text-slate-400 truncate mt-0.5">{lecturer.email}</p>
                </div>
              </div>
            </div>
          )}

          {/* Mentor Assign card */}
          <div className="bg-white rounded-2xl border border-slate-200/60 p-5 shadow-sm space-y-4">
            <div className="flex items-center gap-2">
              <Star className="w-4 h-4 text-primary" />
              <h3 className="font-bold text-slate-800 text-xs uppercase tracking-wider">Startup Mentor</h3>
            </div>
            {mentor ? (
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-full bg-primary-100 flex items-center justify-center text-primary font-bold text-sm shrink-0">
                  {mentor.fullName?.charAt(0)?.toUpperCase() || 'M'}
                </div>
                <div className="min-w-0">
                  <p className="font-bold text-slate-800 text-sm truncate">{mentor.fullName}</p>
                  <p className="text-xs text-slate-400 truncate mt-0.5">{mentor.email}</p>
                </div>
              </div>
            ) : (
              <div className="flex items-start gap-2 bg-amber-50 rounded-xl p-3 border border-amber-100">
                <AlertCircle className="w-4 h-4 text-amber-500 shrink-0 mt-0.5" />
                <p className="text-xs text-amber-700">
                  No mentor assigned to your team yet. Your lecturer will assign one soon.
                </p>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
