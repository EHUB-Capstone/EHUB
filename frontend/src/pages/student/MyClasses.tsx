import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { GraduationCap, User, Mail, Calendar, ArrowRight, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { classApi } from '../../api/classApi';
import EmptyState from '../../components/ui/EmptyState';

const semesterLabel = (sem) => {
  if (sem === 'SP') return 'Spring';
  if (sem === 'SU') return 'Summer';
  if (sem === 'FA') return 'Fall';
  return sem;
};

export default function MyClasses() {
  const [classes, setClasses] = useState([]);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    const fetchClasses = async () => {
      try {
        const res: any = await classApi.getMyClasses();
        const data = res?.data?.classes || res?.classes || [];
        setClasses(data);
      } catch (err) {
        toast.error(err?.message || 'Failed to load your classes');
      } finally {
        setLoading(false);
      }
    };
    fetchClasses();
  }, []);

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[50vh]">
        <Loader2 className="w-8 h-8 text-primary animate-spin" />
        <p className="text-sm text-slate-400 mt-2 font-medium">Loading your classes...</p>
      </div>
    );
  }

  return (
    <div className="space-y-6 max-w-6xl mx-auto">
      {/* Page Header */}
      <div>
        <h1 className="text-2xl font-bold text-slate-900 tracking-tight">My Classes</h1>
        <p className="text-sm text-slate-500 mt-1">View the startup classes you are currently enrolled in.</p>
      </div>

      {classes.length === 0 ? (
        <div className="bg-white rounded-2xl border border-slate-200/60 p-8 shadow-sm">
          <EmptyState
            icon={GraduationCap}
            title="You are not enrolled in any class yet"
            description="Please contact your lecturer or administrator if you think this is a mistake."
          />
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {classes.map((cls) => {
            const lectName  = cls?.lectureId?.name || 'Not assigned yet';
            const lectEmail = cls?.lectureId?.email || '';
            const lectAvatar = cls?.lectureId?.avatar;

            return (
              <div
                key={cls.id || cls._id}
                className="bg-white rounded-2xl border border-slate-200/60 shadow-sm hover:shadow-md hover:border-slate-300 transition-all flex flex-col justify-between overflow-hidden group"
              >
                {/* Header Gradient Accent */}
                <div className="h-2 bg-gradient-to-r from-primary-400 to-secondary" />

                <div className="p-6 flex-1 space-y-4">
                  {/* Subject and Semester info */}
                  <div className="flex justify-between items-start">
                    <div>
                      <span className="px-2.5 py-1 bg-primary-50 text-primary text-xs font-semibold rounded-lg uppercase tracking-wider">
                        {cls.subjectCode}
                      </span>
                      <h2 className="text-xl font-bold text-slate-900 mt-2 group-hover:text-primary transition-colors">
                        {cls.classCode}
                      </h2>
                    </div>
                    <div className="flex items-center gap-1.5 text-xs font-semibold text-slate-500 bg-slate-50 border border-slate-100 rounded-lg px-2 py-1">
                      <Calendar className="w-3.5 h-3.5" />
                      <span>{semesterLabel(cls.semester)} {cls.year}</span>
                    </div>
                  </div>

                  <p className="text-sm text-slate-400 font-medium">
                    {cls.description || 'No description provided.'}
                  </p>

                  {/* Lecturer Info Card */}
                  <div className="pt-4 border-t border-slate-100 flex items-center gap-3">
                    {lectAvatar ? (
                      <img
                        src={lectAvatar}
                        alt={lectName}
                        className="w-10 h-10 rounded-full object-cover border border-slate-100 shrink-0"
                      />
                    ) : (
                      <div className="w-10 h-10 rounded-full bg-gradient-to-br from-slate-100 to-slate-200 border border-slate-200 flex items-center justify-center shrink-0">
                        <User className="w-5 h-5 text-slate-400" />
                      </div>
                    )}
                    <div className="min-w-0">
                      <p className="text-xs text-slate-400 font-semibold uppercase tracking-wider">Lecturer</p>
                      <p className="text-sm font-semibold text-slate-800 truncate">{lectName}</p>
                      {lectEmail && (
                        <p className="text-xs text-slate-400 truncate flex items-center gap-1 mt-0.5">
                          <Mail className="w-3 h-3 shrink-0" />
                          {lectEmail}
                        </p>
                      )}
                    </div>
                  </div>

                  {/* Mentors List */}
                  {cls.mentors && cls.mentors.length > 0 && (
                    <div className="pt-2 border-t border-slate-100 flex items-center gap-2 text-xs text-slate-500">
                      <span className="font-semibold text-slate-400 shrink-0">Mentors:</span>
                      <span className="truncate font-medium">{cls.mentors.map(m => m.fullName || 'Unknown').join(', ')}</span>
                    </div>
                  )}
                </div>

                {/* Footer action */}
                <div className="px-6 py-4 bg-slate-50/50 border-t border-slate-100 flex justify-between items-center group-hover:bg-slate-50 transition-colors">
                  <span className="text-xs text-slate-400 font-medium">Click to see your team or classmates</span>
                  <button
                    onClick={() => navigate(`/student/classes/${cls._id || cls.id}`)}
                    className="flex items-center gap-1.5 px-3 py-1.5 bg-white border border-slate-200 text-slate-700 rounded-xl text-xs font-semibold hover:bg-slate-50 shadow-xs active:scale-[0.98] transition-all"
                  >
                    Enter Class <ArrowRight className="w-3.5 h-3.5" />
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
