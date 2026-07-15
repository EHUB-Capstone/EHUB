// @ts-nocheck
import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { cn } from '../../utils/cn';
import logo from '../../assets/logo.png';
import {
  LayoutDashboard, Users, GraduationCap, Trophy, CalendarDays,
  Kanban, Brain, Video, Rocket, LogOut, Plus, X, MessageSquare, Database, BookOpen
} from 'lucide-react';

const iconMap = {
  dashboard: LayoutDashboard,
  group: Users,
  school: GraduationCap,
  leaderboard: Trophy,
  calendar_month: CalendarDays,
  view_kanban: Kanban,
  analytics: Brain,
  event: CalendarDays,
  military_tech: Trophy,
  rocket_launch: Rocket,
  task_alt: Kanban,
  video_chat: Video,
  chat: MessageSquare,
  database: Database,
  book_open: BookOpen,
};

const Sidebar = ({ mobileOpen, onMobileClose }) => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const rawRole = user?.role?.toUpperCase();
  const role = rawRole === 'USER' ? 'STUDENT' : rawRole;

  const navItems = {
    ADMIN: [
      { path: '/admin', icon: 'dashboard', label: 'Overview' },
      { path: '/admin/subjects', icon: 'book_open', label: 'Subject Management' },
      { path: '/admin/users', icon: 'group', label: 'Users' },
      { path: '/admin/classes', icon: 'school', label: 'Classes' },
      { path: '/lecturer/data-bank', icon: 'database', label: 'Data Bank' },
      { path: '/workspace', icon: 'view_kanban', label: 'Startup Workspace' },
      { path: '/workshops', icon: 'calendar_month', label: 'Workshops' },
      { path: '/chat', icon: 'chat', label: 'Group Chat' },
      { path: '/rankings', icon: 'leaderboard', label: 'Rankings' },
      { path: '/sessions', icon: 'calendar_month', label: 'Schedules' },
    ],
    LECTURER: [
      { path: '/lecturer', icon: 'dashboard', label: 'Dashboard' },
      { path: '/lecturer/classes', icon: 'school', label: 'My Classes' },
      { path: '/lecturer/data-bank', icon: 'database', label: 'Data Bank' },
      { path: '/workspace', icon: 'view_kanban', label: 'Startup Workspace' },
      { path: '/workshops', icon: 'calendar_month', label: 'Workshops' },
      { path: '/chat', icon: 'chat', label: 'Group Chat' },
      { path: '/executionboard', icon: 'view_kanban', label: 'Execution Board' },
      { path: '/evaluations', icon: 'analytics', label: 'AI Reports' },
      { path: '/sessions', icon: 'event', label: 'Sessions' },
      { path: '/rankings', icon: 'military_tech', label: 'Rankings' },
    ],
    MENTOR: [
      { path: '/mentor', icon: 'dashboard', label: 'Dashboard' },
      { path: '/workspace', icon: 'view_kanban', label: 'Startup Workspace' },
      { path: '/lecturer/classes', icon: 'school', label: 'My Classes' },
      { path: '/workshops', icon: 'calendar_month', label: 'Workshops' },
      { path: '/chat', icon: 'chat', label: 'Group Chat' },
      { path: '/sessions', icon: 'event', label: 'Sessions' },
      { path: '/rankings', icon: 'military_tech', label: 'Rankings' },
    ],
    STUDENT: [
      { path: '/student/classes', icon: 'school', label: 'My Classes' },
      { path: '/student/team', icon: 'group', label: 'My Team' },
      { path: '/student/workspace', icon: 'view_kanban', label: 'Startup Workspace' },
      { path: '/student', icon: 'dashboard', label: 'Dashboard' },
      { path: '/workshops', icon: 'calendar_month', label: 'Workshops' },
      { path: '/rankings', icon: 'military_tech', label: 'Rankings' },
      { path: '/chat', icon: 'chat', label: 'Group Chat' },
      { path: '/student/idea/new', icon: 'rocket_launch', label: 'My Idea' },
      { path: '/executionboard', icon: 'task_alt', label: 'Execution Board' },
      { path: '/sessions', icon: 'video_chat', label: 'Mentoring' },
    ],
  };

  const items = navItems[role] || navItems.STUDENT;

  const handleNavClick = () => {
    if (onMobileClose) onMobileClose();
  };

  return (
    <aside
      className={cn(
        'fixed left-0 top-0 h-screen z-50 flex flex-col bg-white border-r border-slate-200/80 transition-transform duration-300 ease-out w-[260px]',
        mobileOpen ? 'translate-x-0' : '-translate-x-full',
        'lg:translate-x-0'
      )}
    >
      {/* Brand */}
      <div className="flex items-center justify-between h-16 border-b border-slate-100 px-4">
        <div className="flex items-center gap-3">
          <img src={logo} alt="EHub" className="w-9 h-9 rounded-xl object-contain shrink-0" />
          <div className="min-w-0">
            <h1 className="text-[15px] font-bold leading-tight tracking-tight"><span className="text-[#F08A5D]">E</span><span className="text-[#1E5E9F]">HUB</span></h1>
            <p className="text-[10px] text-slate-400 font-semibold uppercase tracking-[0.12em]">Startup Portal</p>
          </div>
        </div>
        <button
          onClick={onMobileClose}
          className="w-8 h-8 rounded-lg hover:bg-slate-100 flex items-center justify-center text-slate-400 hover:text-slate-600 transition-colors lg:hidden"
          aria-label="Close sidebar"
        >
          <X className="w-4 h-4" />
        </button>
      </div>

      {/* Navigation */}
      <nav className="flex-1 py-4 px-3 space-y-1 overflow-y-auto scrollbar-thin">
        <p className="text-[10px] font-semibold text-slate-400 uppercase tracking-wider px-3 mb-3 mt-1">Navigation</p>
        {items.map((item) => {
          const IconComp = iconMap[item.icon] || LayoutDashboard;
          return (
            <NavLink
              key={item.path}
              to={item.path}
              end={item.path === '/admin' || item.path === '/lecturer' || item.path === '/mentor' || item.path === '/student'}
              onClick={handleNavClick}
              className={({ isActive }) => cn(
                'flex items-center gap-3 rounded-xl transition-all duration-200 group relative px-3 py-2.5',
                isActive
                  ? 'bg-primary-50 text-primary font-semibold shadow-xs'
                  : 'text-slate-500 hover:text-slate-900 hover:bg-slate-50'
              )}
            >
              {({ isActive }) => (
                <>
                  {isActive && (
                    <div className="absolute left-0 top-1/2 -translate-y-1/2 w-[3px] h-5 bg-primary rounded-r-full" />
                  )}
                  <IconComp className={cn('w-[18px] h-[18px] shrink-0 transition-colors', isActive ? 'text-primary' : 'text-slate-400 group-hover:text-slate-600')} />
                  <span className="text-[13px] truncate">{item.label}</span>
                </>
              )}
            </NavLink>
          );
        })}
      </nav>

      {/* Bottom Section */}
      <div className="border-t border-slate-100 p-3 space-y-2">
        {role === 'STUDENT' && (
          <button
            onClick={() => { navigate('/student/idea/new'); handleNavClick(); }}
            className="w-full bg-gradient-to-r from-primary to-secondary text-white font-semibold py-2.5 rounded-xl flex items-center justify-center gap-2 shadow-sm hover:shadow-glow-primary transition-all active:scale-[0.98] text-[13px]"
          >
            <Plus className="w-4 h-4" />
            New Idea
          </button>
        )}

        <button
          onClick={handleLogout}
          className="w-full flex items-center gap-3 rounded-xl text-danger hover:bg-danger-50 transition-all text-[13px] font-medium px-3 py-2.5"
        >
          <LogOut className="w-[18px] h-[18px]" />
          <span>Sign Out</span>
        </button>
      </div>
    </aside>
  );
};

export default Sidebar;
