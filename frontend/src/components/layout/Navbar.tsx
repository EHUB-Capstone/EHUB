import { useAuth } from '../../hooks/useAuth';
import { Search, ChevronDown, User, LogOut, Menu, Settings, Moon, Sun, AlertTriangle } from 'lucide-react';
import { useState, useRef, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import Badge from '../ui/Badge';
import NotificationDropdown from './NotificationDropdown';
import { useTheme } from '../../context/ThemeContext';
import { notificationApi } from '../../api/notificationApi';

const Navbar = ({ onMenuClick }) => {
  const { user, logout } = useAuth();
  const { isDark, toggleTheme } = useTheme();
  const navigate = useNavigate();
  const [showProfileMenu, setShowProfileMenu] = useState(false);
  const [classConflictAlert, setClassConflictAlert] = useState(null);
  const menuRef = useRef(null);

  // Close menu on outside click
  useEffect(() => {
    const handleClickOutside = (e) => {
      if (menuRef.current && !menuRef.current.contains(e.target)) {
        setShowProfileMenu(false);
      }
    };
    if (showProfileMenu) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [showProfileMenu]);

  const roleLabels = {
    ADMIN: 'Administrator',
    LECTURER: 'Lecturer',
    MENTOR: 'Mentor',
    STUDENT: 'Student',
  };

  const roleBadgeVariant = {
    ADMIN: 'Approved',
    LECTURER: 'Submitted',
    MENTOR: 'Review',
    STUDENT: 'Reviewed',
  };

  const rawRole = user?.role?.toUpperCase() || 'STUDENT';
  const role = rawRole === 'USER' ? 'STUDENT' : rawRole;

  useEffect(() => {
    if (role !== 'LECTURER') return;

    const loadClassConflictAlert = async () => {
      try {
        const res = await notificationApi.getAll();
        const list = res.data || res || [];
        const alert = Array.isArray(list)
          ? list.find(n => !n.isRead && n.data?.action === 'CLASS_CODE_CONFLICT_REVIEW')
          : null;
        if (alert) setClassConflictAlert(alert);
      } catch (err) {
        console.error('Failed to load class conflict alert:', err);
      }
    };

    loadClassConflictAlert();
  }, [role]);

  const closeClassConflictAlert = async (goToClasses = false) => {
    const alert = classConflictAlert;
    setClassConflictAlert(null);

    if (alert?._id) {
      try {
        await notificationApi.markRead(alert._id);
      } catch (err) {
        console.error('Failed to mark class conflict alert as read:', err);
      }
    }

    if (goToClasses) {
      navigate('/lecturer/classes');
    }
  };

  if (!user) return null;

  return (
    <header className="bg-white/85 dark:bg-[#111827]/88 backdrop-blur-xl border-b border-slate-200/60 dark:border-white/10 sticky top-0 z-30 h-16 flex items-center justify-between px-4 sm:px-6 gap-4 transition-colors duration-200">
      {/* Left: Hamburger + Search */}
      <div className="flex items-center gap-3 flex-1 min-w-0">
        <button
          onClick={onMenuClick}
          className="w-9 h-9 rounded-xl flex items-center justify-center text-slate-500 hover:text-slate-700 hover:bg-slate-100 transition-all lg:hidden shrink-0"
          aria-label="Open sidebar menu"
        >
          <Menu className="w-5 h-5" />
        </button>

        <div className="flex-1 max-w-md hidden sm:block">
          <div className="relative group">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400 group-focus-within:text-primary transition-colors" />
            <input
              className="w-full bg-slate-50/80 border border-slate-200 rounded-xl pl-10 pr-4 py-2 text-[13px] text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary focus:bg-white transition-all"
              placeholder="Search teams, ideas, students..."
              type="text"
              aria-label="Search"
            />
            <kbd className="absolute right-3 top-1/2 -translate-y-1/2 hidden md:inline-flex h-5 items-center gap-1 rounded border border-slate-200 bg-white px-1.5 text-[10px] font-medium text-slate-400">
              ⌘K
            </kbd>
          </div>
        </div>
      </div>

      {/* Right Section */}
      <div className="flex items-center gap-2 sm:gap-3 shrink-0">
        <button
          type="button"
          onClick={toggleTheme}
          className="flex h-9 w-9 items-center justify-center rounded-xl bg-slate-100 text-slate-600 transition-all hover:bg-slate-200 hover:text-slate-900"
          aria-label={isDark ? 'Switch to light theme' : 'Switch to dark theme'}
          title={isDark ? 'Light theme' : 'Dark theme'}
        >
          {isDark ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
        </button>

        {/* Notification */}
        <NotificationDropdown />

        <div className="w-px h-6 bg-slate-200 hidden sm:block" />

        {/* Profile */}
        <div className="relative" ref={menuRef}>
          <button
            className="flex items-center gap-2 sm:gap-3 hover:bg-slate-50 py-1.5 px-2 sm:px-2.5 rounded-xl transition-all group"
            onClick={() => setShowProfileMenu(!showProfileMenu)}
            aria-expanded={showProfileMenu}
            aria-haspopup="true"
          >
            <div className="hidden sm:flex flex-col items-end">
              <span className="text-[13px] font-semibold text-slate-800 leading-tight">{user.name || 'User'}</span>
              <Badge variant={roleBadgeVariant[role]} size="xs">
                {roleLabels[role] || role}
              </Badge>
            </div>
            <div className="w-9 h-9 rounded-xl bg-gradient-to-br from-primary-100 to-primary-200 overflow-hidden border border-primary-100 flex items-center justify-center shrink-0">
              {user.avatar ? (
                <img src={user.avatar} alt="Avatar" className="w-full h-full object-cover" />
              ) : (
                <span className="font-display font-bold text-primary text-[13px]">{user.name?.charAt(0)?.toUpperCase() || 'U'}</span>
              )}
            </div>
            <ChevronDown className={cn('w-3.5 h-3.5 text-slate-400 transition-transform duration-200', showProfileMenu && 'rotate-180')} />
          </button>

          {showProfileMenu && (
            <div className="absolute right-0 top-full mt-2 w-52 bg-white rounded-xl shadow-float border border-slate-100 overflow-hidden py-1 animate-scale-in origin-top-right">
              <div className="px-4 py-3 border-b border-slate-100 sm:hidden">
                <p className="text-sm font-semibold text-slate-900">{user.name || 'User'}</p>
                <p className="text-xs text-slate-500 mt-0.5">{user.email || ''}</p>
              </div>
              <button
                className="w-full text-left px-4 py-2.5 text-sm text-slate-700 hover:bg-slate-50 hover:text-primary transition-colors flex items-center gap-2.5"
                onClick={() => { setShowProfileMenu(false); navigate('/settings'); }}
              >
                <Settings className="w-4 h-4" /> Settings
              </button>
              <button
                className="w-full text-left px-4 py-2.5 text-sm text-slate-700 hover:bg-slate-50 hover:text-primary transition-colors flex items-center gap-2.5"
                onClick={() => { setShowProfileMenu(false); navigate('/settings'); }}
              >
                <User className="w-4 h-4" /> Profile
              </button>
              <div className="h-px w-full bg-slate-100 my-1" />
              <button
                className="w-full text-left px-4 py-2.5 text-sm text-danger hover:bg-danger-50 transition-colors flex items-center gap-2.5 font-medium"
                onClick={() => { logout(); navigate('/login'); }}
              >
                <LogOut className="w-4 h-4" /> Sign Out
              </button>
            </div>
          )}
        </div>
      </div>

      {classConflictAlert && (
        <div className="fixed inset-0 z-[80] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
          <div className="w-full max-w-md rounded-2xl bg-white border border-amber-200 shadow-float p-5">
            <div className="flex items-start gap-3">
              <div className="w-10 h-10 rounded-xl bg-amber-100 flex items-center justify-center shrink-0">
                <AlertTriangle className="w-5 h-5 text-amber-600" />
              </div>
              <div>
                <h2 className="text-lg font-bold text-slate-900">Please verify your class code</h2>
                <p className="text-sm text-slate-600 mt-1">{classConflictAlert.message}</p>
                {classConflictAlert.data?.classCode && (
                  <p className="text-xs font-semibold text-slate-500 mt-2">
                    {classConflictAlert.data.classCode} - {classConflictAlert.data.semester} {classConflictAlert.data.year}
                  </p>
                )}
              </div>
            </div>

            <div className="flex gap-2 mt-5">
              <button
                type="button"
                onClick={() => closeClassConflictAlert(false)}
                className="flex-1 px-4 py-2.5 rounded-xl border border-slate-200 text-sm text-slate-700 hover:bg-slate-50 transition-all"
              >
                Dismiss
              </button>
              <button
                type="button"
                onClick={() => closeClassConflictAlert(true)}
                className="flex-1 px-4 py-2.5 rounded-xl bg-primary text-white text-sm font-medium hover:bg-primary-700 transition-all"
              >
                Check classes
              </button>
            </div>
          </div>
        </div>
      )}
    </header>
  );
};

// Small helper (avoids importing cn for just one usage in template literal)
function cn(...args) {
  return args.filter(Boolean).join(' ');
}

export default Navbar;
