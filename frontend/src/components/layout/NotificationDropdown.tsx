import { useState, useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import { useNavigate } from 'react-router-dom';
import { Bell, Check, Calendar, Award, Kanban, Brain, MessageSquare, ShieldAlert, UserCheck, ExternalLink } from 'lucide-react';
import { getNotificationId, normalizeNotification, notificationApi } from '../../api/notificationApi';
import toast from 'react-hot-toast';
import { classApi } from '../../api/classApi';
import { toClassViewModel, unwrapApiData } from '../../utils/classMappers';
import {
  buildLecturerDirectionOverviewLink,
  getLegacyNotificationClassId,
  isProjectDirectionSubmittedNotification,
} from '../../utils/notificationNavigation';
import { subscribeProjectDirectionRealtime } from '../../api/projectDirectionRealtime';
import { checkpointDeadlineExtensionApi } from '../../api/checkpointDeadlineExtensionApi';

const NotificationDropdown = () => {
  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [isOpen, setIsOpen] = useState(false);
  const [extensionNotification, setExtensionNotification] = useState(null);
  const [extensionReason, setExtensionReason] = useState('');
  const [extensionSaving, setExtensionSaving] = useState(false);
  const dropdownRef = useRef(null);
  const navigate = useNavigate();

  const fetchNotifications = async () => {
    try {
      const res = await notificationApi.getAll();
      const list = res.data || res || [];
      const normalized = Array.isArray(list) ? list.map(normalizeNotification) : [];
      const hydrated = await Promise.all(normalized.map(async (notification) => {
        if (!isOverdueLeaderNotification(notification) || notification.data?.deadlineExtensionRequest || !notification.data?.deadlineUtc) return notification;
        try {
          const response = await checkpointDeadlineExtensionApi.getMine(
            notification.data.teamId,
            notification.data.checkpointNumber,
            notification.data.deadlineUtc,
          );
          return { ...notification, data: { ...notification.data, deadlineExtensionRequest: response.data ?? response } };
        } catch {
          return notification;
        }
      }));
      setNotifications(hydrated);

      const countRes = await notificationApi.getUnreadCount();
      setUnreadCount(countRes.data?.count ?? countRes.count ?? 0);
    } catch (err) {
      console.error('Failed to fetch notifications:', err);
    }
  };

  useEffect(() => {
    fetchNotifications();
    const unsubscribe = subscribeProjectDirectionRealtime((event) => {
      if (event.eventType === 'ProjectDirectionNotificationReady' || event.eventType === 'TeamFormationChanged'
        || event.eventType === 'TeamCreated')
        void fetchNotifications();
    }, () => { void fetchNotifications(); });
    // Backup refresh for notification types that do not have a realtime event yet.
    const interval = setInterval(fetchNotifications, 30000);
    return () => {
      unsubscribe();
      clearInterval(interval);
    };
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
    const notificationId = getNotificationId(n);
    let targetLink = n.link;

    if (isProjectDirectionSubmittedNotification(n) && !n.link?.startsWith('/lecturer/classes')) {
      const classId = getLegacyNotificationClassId(n);
      if (classId) {
        try {
          const response = await classApi.getById(classId);
          const classPayload = unwrapApiData(response);
          const classInfo = toClassViewModel(classPayload?.class || classPayload);
          targetLink = buildLecturerDirectionOverviewLink({
            semester: classInfo.semester,
            year: classInfo.year,
            classId: classInfo._id,
            teamId: n.data?.teamId,
          });
        } catch (error) {
          console.error('Failed to resolve project direction notification target:', error);
        }
      }
    }

    if (!n.isRead) {
      try {
        await notificationApi.markRead(notificationId);
        setNotifications(prev => prev.map(item => getNotificationId(item) === notificationId ? { ...item, isRead: true } : item));
        setUnreadCount(prev => Math.max(0, prev - 1));
      } catch (err) {
        console.error('Failed to mark notification as read:', err);
      }
    }
    if (targetLink) {
      navigate(targetLink);
    }
  };

  const isOverdueLeaderNotification = (notification) =>
    String(notification?.type || '').replaceAll('_', '').toUpperCase() === 'DEADLINEOVERDUE' &&
    Boolean(notification?.data?.isTeamLeader);

  const isDeadlineExtensionRequestNotification = (notification) =>
    String(notification?.type || '').replaceAll('_', '').toUpperCase() === 'DEADLINEEXTENSIONREQUESTED';

  const openExtensionRequest = (event, notification) => {
    event.stopPropagation();
    setExtensionReason('');
    setExtensionNotification(notification);
  };

  const submitExtensionRequest = async () => {
    const data = extensionNotification?.data;
    const reason = extensionReason.trim();
    if (!data?.teamId || !data?.checkpointNumber || !reason || extensionSaving) return;
    setExtensionSaving(true);
    try {
      const response = await checkpointDeadlineExtensionApi.create(data.teamId, data.checkpointNumber, { reason });
      const request = response.data ?? response;
      const notificationId = getNotificationId(extensionNotification);
      setNotifications(previous => previous.map(item => getNotificationId(item) === notificationId
        ? { ...item, data: { ...item.data, deadlineExtensionRequest: request } }
        : item));
      setExtensionNotification(null);
      toast.success('Your request was sent to the lecturer.');
    } catch (error) {
      const message = error?.response?.data?.message || 'Unable to send the request. Please try again.';
      toast.error(message);
    } finally {
      setExtensionSaving(false);
    }
  };

  const getIcon = (type) => {
    switch (type) {
      case 'WORKSHOP':
      case 'SEMINAR':
      case 'DeadlineReminder':
      case 'DEADLINE_REMINDER':
      case 'DeadlineOverdue':
      case 'DEADLINE_OVERDUE':
        return <Calendar className="w-4 h-4 text-indigo-500" />;
      case 'DeadlineExtensionRequested':
      case 'DEADLINE_EXTENSION_REQUESTED':
        return <MessageSquare className="w-4 h-4 text-amber-600" />;
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
      case 'AccountApprovalRequested':
      case 'ACCOUNT_APPROVAL_REQUESTED':
        return <UserCheck className="w-4 h-4 text-amber-500" />;
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
                  key={getNotificationId(n)}
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
                    {isOverdueLeaderNotification(n) && (
                      n.data?.deadlineExtensionRequest ? (
                        <button type="button" onClick={(event) => { event.stopPropagation(); setExtensionNotification(n); }} className="mt-2 text-xs font-semibold text-primary hover:underline">
                          View request
                        </button>
                      ) : (
                        <button type="button" onClick={(event) => openExtensionRequest(event, n)} className="mt-2 text-xs font-semibold text-primary hover:underline">
                          Ask lecturer to reopen deadline
                        </button>
                      )
                    )}
                    {isDeadlineExtensionRequestNotification(n) && (
                      <button type="button" onClick={(event) => { event.stopPropagation(); setExtensionNotification(n); }} className="mt-2 inline-flex items-center gap-1 text-xs font-semibold text-primary hover:underline">
                        View request <ExternalLink className="h-3 w-3" />
                      </button>
                    )}
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
      {extensionNotification && createPortal((
        <div className="fixed inset-0 z-[60] flex items-center justify-center bg-slate-950/40 p-4" role="dialog" aria-modal="true" aria-labelledby="deadline-extension-title">
          <div className="w-full max-w-lg rounded-2xl bg-white p-5 shadow-xl">
            <h2 id="deadline-extension-title" className="text-base font-bold text-slate-900">
              {extensionNotification.data?.deadlineExtensionRequest ? 'Deadline extension request' : 'Ask lecturer to reopen deadline'}
            </h2>
            <p className="mt-1 text-sm text-slate-600">{extensionNotification.title}</p>
            {extensionNotification.data?.deadlineExtensionRequest ? (
              <p className="mt-4 whitespace-pre-wrap rounded-xl bg-slate-50 p-3 text-sm text-slate-700">{extensionNotification.data.deadlineExtensionRequest.reason}</p>
            ) : isDeadlineExtensionRequestNotification(extensionNotification) ? (
              <div className="mt-4 space-y-2 rounded-xl bg-slate-50 p-3 text-sm text-slate-700">
                <p><span className="font-semibold">Team:</span> {extensionNotification.data?.teamName || 'Not available'}</p>
                <p><span className="font-semibold">Class:</span> {extensionNotification.data?.classCode || 'Not available'}</p>
                <p><span className="font-semibold">Checkpoint:</span> {extensionNotification.data?.checkpointTitle || `Checkpoint ${extensionNotification.data?.checkpointNumber || ''}`}</p>
                <p><span className="font-semibold">Reason:</span></p>
                <p className="whitespace-pre-wrap">{extensionNotification.data?.reason || 'Not available'}</p>
              </div>
            ) : (
              <textarea value={extensionReason} onChange={(event) => setExtensionReason(event.target.value)} maxLength={2000} rows={5} className="mt-4 w-full rounded-xl border border-slate-200 p-3 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20" placeholder="Explain why your team submitted late…" aria-label="Reason for late submission" />
            )}
            <div className="mt-4 flex justify-end gap-2">
              <button type="button" onClick={() => setExtensionNotification(null)} className="rounded-lg border border-slate-200 px-3 py-2 text-sm font-semibold text-slate-600">Close</button>
              {isDeadlineExtensionRequestNotification(extensionNotification) && (
                <button type="button" onClick={() => { setExtensionNotification(null); void handleNotificationClick(extensionNotification); }} className="rounded-lg bg-primary px-3 py-2 text-sm font-semibold text-white">Open checkpoint</button>
              )}
              {!extensionNotification.data?.deadlineExtensionRequest && !isDeadlineExtensionRequestNotification(extensionNotification) && (
                <button type="button" disabled={!extensionReason.trim() || extensionSaving} onClick={() => void submitExtensionRequest()} className="rounded-lg bg-primary px-3 py-2 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-50">{extensionSaving ? 'Sending…' : 'Send request'}</button>
              )}
            </div>
          </div>
        </div>
      ), document.body)}
    </div>
  );
};

export default NotificationDropdown;
