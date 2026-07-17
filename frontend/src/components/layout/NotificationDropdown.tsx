import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Bell, Check, Calendar, Award, Kanban, Brain, MessageSquare, ShieldAlert } from 'lucide-react';
import { notificationApi } from '../../api/notificationApi';
import toast from 'react-hot-toast';

const NotificationDropdown = () => {
  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef(null);
  const navigate = useNavigate();

  const fetchNotifications = async () => {
    try {
      const res = await notificationApi.getAll();
      const list = res.data || res || [];
      setNotifications(Array.isArray(list) ? list : []);

      const countRes = await notificationApi.getUnreadCount();
      setUnreadCount(countRes.data?.count ?? countRes.count ?? 0);
    } catch (err) {
      console.error('Failed to fetch notifications:', err);
    }
  };

  useEffect(() => {
    fetchNotifications();
    // Poll every 30 seconds
    const interval = setInterval(fetchNotifications, 30000);
    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    const clickOutside = (e) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target)) {
        setIsOpen(false);
      }
    };
    if (isOpen) {
      document.addEventListener('mousedown', clickOutside);
    }
    return () => document.removeEventListener('mousedown', clickOutside);
  }, [isOpen]);

  const handleMarkAllRead = async () => {
    try {
      await notificationApi.markAllRead();
      setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
      setUnreadCount(0);
      toast.success('Marked all as read');
    } catch {
      toast.error('Failed to mark all as read');
    }
  };

  const handleNotificationClick = async (n) => {
    setIsOpen(false);
    if (!n.isRead) {
      try {
        await notificationApi.markRead(n._id);
        setNotifications(prev => prev.map(item => item._id === n._id ? { ...item, isRead: true } : item));
        setUnreadCount(prev => Math.max(0, prev - 1));
      } catch (err) {
        console.error('Failed to mark notification as read:', err);
      }
    }
    if (n.link) {
      navigate(n.link);
    }
  };

  const getIcon = (type) => {
    switch (type) {
      case 'WORKSHOP':
      case 'SEMINAR':
        return <Calendar className="w-4 h-4 text-indigo-500" />;
      case 'EVALUATION':
        return <Brain className="w-4 h-4 text-emerald-500" />;
      case 'MENTORING':
        return <Award className="w-4 h-4 text-amber-500" />;
      case 'MILESTONE':
      case 'TASK':
        return <Kanban className="w-4 h-4 text-sky-500" />;
      case 'TEAM':
      case 'CLASS':
        return <MessageSquare className="w-4 h-4 text-purple-500" />;
      default:
        return <ShieldAlert className="w-4 h-4 text-slate-500" />;
    }
  };

  return (
    <div className="relative" ref={dropdownRef}>
      <button
        onClick={() => {
          setIsOpen(!isOpen);
          if (!isOpen) fetchNotifications();
        }}
        className="relative w-9 h-9 rounded-xl flex items-center justify-center text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition-all focus:outline-none"
        aria-label="Notifications"
      >
        <Bell className="w-[18px] h-[18px]" />
        {unreadCount > 0 && (
          <span className="absolute top-1.5 right-1.5 min-w-[16px] h-4 px-1 rounded-full bg-red-500 text-white font-bold text-[9px] flex items-center justify-center border border-white ring-2 ring-white animate-pulse">
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </button>

      {isOpen && (
        <div className="absolute right-0 mt-2 w-80 sm:w-96 bg-white rounded-2xl shadow-float border border-slate-100 overflow-hidden z-50 py-1 origin-top-right animate-scale-in">
          {/* Header */}
          <div className="flex items-center justify-between px-4 py-3 border-b border-slate-100 bg-slate-50/50">
            <h3 className="font-semibold text-slate-800 text-sm flex items-center gap-1.5">
              <span>Notifications</span>
              {unreadCount > 0 && (
                <span className="bg-red-100 text-red-600 text-[11px] font-bold px-1.5 py-0.5 rounded-full">
                  {unreadCount} new
                </span>
              )}
            </h3>
            {unreadCount > 0 && (
              <button
                onClick={handleMarkAllRead}
                className="text-xs font-semibold text-primary hover:text-primary-700 flex items-center gap-1 transition-colors"
              >
                <Check className="w-3.5 h-3.5" /> Mark all read
              </button>
            )}
          </div>

          {/* List */}
          <div className="max-h-[360px] overflow-y-auto divide-y divide-slate-50">
            {notifications.length === 0 ? (
              <div className="py-8 px-4 text-center text-slate-400 text-sm">
                <Bell className="w-8 h-8 mx-auto mb-2 text-slate-200" />
                No notifications yet
              </div>
            ) : (
              notifications.map((n) => (
                <div
                  key={n._id}
                  onClick={() => handleNotificationClick(n)}
                  className={`flex gap-3 px-4 py-3.5 hover:bg-slate-50/80 cursor-pointer transition-colors relative group ${!n.isRead ? 'bg-primary-50/20' : ''}`}
                >
                  <div className="w-8 h-8 rounded-xl bg-slate-100 flex items-center justify-center shrink-0 mt-0.5">
                    {getIcon(n.type)}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className={`text-[13px] leading-snug text-slate-800 ${!n.isRead ? 'font-semibold' : 'font-medium'}`}>
                      {n.title}
                    </p>
                    <p className="text-[12px] text-slate-500 mt-1 line-clamp-2 leading-relaxed">
                      {n.message}
                    </p>
                    <span className="text-[10px] text-slate-400 font-semibold block mt-1.5">
                      {new Date(n.createdAt).toLocaleDateString()} at {new Date(n.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                    </span>
                  </div>
                  {!n.isRead && (
                    <span className="absolute top-4 right-4 w-2 h-2 bg-primary rounded-full" />
                  )}
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
};

export default NotificationDropdown;
