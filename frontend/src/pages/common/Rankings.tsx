// @ts-nocheck
import { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { motion } from 'framer-motion';
import { rankingApi } from '../../api/rankingApi';
import { classApi } from '../../api/classApi';
import { useAuth } from '../../hooks/useAuth';
import RankingTable from '../../components/workspace/RankingTable';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import { Trophy, Medal, Star, Filter } from 'lucide-react';

const Rankings = () => {
  const { user } = useAuth();
  const [rankings, setRankings] = useState([]);
  const [classes, setClasses] = useState([]);
  const [selectedClass, setSelectedClass] = useState('');
  const [loading, setLoading] = useState(true);

  const isAdmin = user?.role === 'ADMIN';
  const isLecturerOrMentor = ['LECTURER', 'MENTOR'].includes(user?.role);

  const fetchRankings = async () => {
    setLoading(true);
    try {
      let res;
      if (selectedClass) {
        res = await rankingApi.getClass(selectedClass);
      } else if (isAdmin) {
        res = await rankingApi.getGlobal();
      } else if (isLecturerOrMentor) {
        res = await rankingApi.getMyTeams();
      } else {
        res = await rankingApi.getMyClass();
      }
      const data = res.data || res || [];
      setRankings(Array.isArray(data) ? data : []);
    } catch {
      toast.error('Failed to load rankings');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const loadClasses = async () => {
      if (isAdmin || isLecturerOrMentor) {
        try {
          const res = await classApi.getAll();
          const list = res.data?.classes || res.classes || res.data || [];
          setClasses(Array.isArray(list) ? list : []);
        } catch (err) {
          console.error('Failed to load classes', err);
        }
      }
    };
    loadClasses();
  }, [isAdmin, isLecturerOrMentor]);

  useEffect(() => {
    fetchRankings();
  }, [selectedClass]);

  if (loading && rankings.length === 0) return <LoadingSkeleton />;

  const top3 = rankings.slice(0, 3);
  const podiumOrder = [top3[1], top3[0], top3[2]].filter(Boolean);
  const podiumHeight = ['h-32', 'h-24', 'h-20'];
  const podiumRank = [1, 2, 3];
  const podiumIcon = [
    <Trophy key="1" className="w-7 h-7 text-amber-400" />,
    <Medal key="2" className="w-6 h-6 text-slate-300" />,
    <Medal key="3" className="w-6 h-6 text-amber-700" />,
  ];

  return (
    <div className="space-y-6">
      {/* Title */}
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="flex flex-col sm:flex-row justify-between items-start sm:items-end gap-4">
        <div>
          <h1 className="text-3xl font-bold text-slate-900 flex items-center gap-2">
            <span>🏆 Startup Rankings</span>
          </h1>
          <p className="text-slate-500 mt-1">
            {rankings.length} startups • Weighted by Evaluation (60%), Sprint (25%), Proposal (10%), Deck (5%)
          </p>
        </div>

        {/* Class Filter (Admin/Lecturer/Mentor only) */}
        {(isAdmin || isLecturerOrMentor) && classes.length > 0 && (
          <div className="flex items-center gap-2 bg-white border border-slate-200/60 px-3 py-1.5 rounded-xl shadow-sm shrink-0">
            <Filter className="w-4 h-4 text-slate-400" />
            <select
              value={selectedClass}
              onChange={e => setSelectedClass(e.target.value)}
              className="text-xs font-semibold text-slate-700 bg-transparent border-none focus:outline-none focus:ring-0 cursor-pointer pr-8"
            >
              <option value="">{isAdmin ? 'System Global Rankings' : 'All My Teams'}</option>
              {classes.map(c => (
                <option key={c._id} value={c._id}>{c.classCode} - {c.subjectCode}</option>
              ))}
            </select>
          </div>
        )}
      </motion.div>

      {rankings.length === 0 ? (
        <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-12 text-center text-slate-400">
          <Trophy className="w-12 h-12 mx-auto mb-3 text-slate-200" />
          <h3 className="font-semibold text-slate-700 text-sm">No Rankings Calculated</h3>
          <p className="text-xs text-slate-400 mt-1">No evaluations or sprint progress records exist to rank teams.</p>
        </div>
      ) : (
        <>
          {/* Podium for Top 3 */}
          {top3.length >= 1 && (
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.05 }}
              className="bg-gradient-to-br from-primary-900 via-primary-800 to-indigo-900 rounded-3xl p-6 sm:p-8 text-white shadow-xl relative overflow-hidden"
            >
              <div className="absolute top-0 right-0 w-64 h-64 bg-radial-gradient from-white/10 to-transparent rounded-full blur-2xl" />
              <h3 className="text-center text-white/50 text-xs font-black uppercase tracking-widest mb-6">Top Startups Podium</h3>

              <div className="flex items-end justify-center gap-4 sm:gap-8 pt-4">
                {podiumOrder.map((team, idx) => {
                  const actualIndex = top3.indexOf(team);
                  return (
                    <div key={team.teamId} className={`flex flex-col items-center gap-3 w-28 sm:w-36 ${idx === 1 ? 'order-first sm:order-none' : ''}`}>
                      <div className={`w-14 h-14 sm:w-16 sm:h-16 rounded-2xl bg-white/10 backdrop-blur-md flex items-center justify-center text-2xl font-black border border-white/20 shadow-lg`}>
                        {team.startupName?.charAt(0)}
                      </div>
                      <div className="text-center min-w-0 px-1">
                        <p className="font-bold text-xs sm:text-sm truncate">{team.startupName}</p>
                        <p className="text-white/60 text-[10px] sm:text-xs truncate">{team.teamName}</p>
                        <p className="text-sm sm:text-lg font-black text-amber-300 mt-1">
                          {team.finalScore} <span className="text-[10px] sm:text-xs text-white/50">pts</span>
                        </p>
                      </div>
                      {podiumIcon[actualIndex]}
                      <div className={`${podiumHeight[actualIndex]} w-20 sm:w-24 bg-white/5 backdrop-blur-xs rounded-t-2xl flex items-end justify-center pb-3 border-t-2 border-white/20 shadow-inner`}>
                        <span className="text-white/80 font-black text-sm sm:text-base">#{podiumRank[actualIndex]}</span>
                      </div>
                    </div>
                  );
                })}
              </div>
            </motion.div>
          )}

          {/* Details Table */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.1 }}
          >
            <RankingTable rankings={rankings} />
          </motion.div>
        </>
      )}
    </div>
  );
};

export default Rankings;
