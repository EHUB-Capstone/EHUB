// @ts-nocheck
import { useState, useEffect, useRef, useContext } from 'react';
import { useSearchParams } from 'react-router-dom';
import io from 'socket.io-client';
import { AuthContext } from '../../context/AuthContext';
import { chatApi } from '../../api/chatApi';
import { runtimeConfig } from '../../config/runtimeConfig';
import {
  MessageSquare, Send, Users, Shield, GraduationCap, Star,
  Search, Loader2, Clock, User, Paperclip, Pencil, X,
  ChevronRight, BadgeCheck, Menu, Smile, RotateCcw, Check,
  ThumbsUp, PartyPopper, Lightbulb, Heart, Sparkles
} from 'lucide-react';
import toast from 'react-hot-toast';
// ─── Role helpers ────────────────────────────────────────────────────────────
const roleConfig = {
  ADMIN:    { color: 'bg-red-50 text-red-700 border-red-200',     icon: <Shield       className="w-3 h-3 text-red-500 shrink-0" />,     label: 'Admin' },
  LECTURER: { color: 'bg-purple-50 text-purple-700 border-purple-200', icon: <GraduationCap className="w-3 h-3 text-purple-500 shrink-0" />, label: 'Lecturer' },
  MENTOR:   { color: 'bg-amber-50 text-amber-700 border-amber-200',  icon: <Star         className="w-3 h-3 text-amber-500 shrink-0" />,    label: 'Mentor' },
  STUDENT:  { color: 'bg-blue-50 text-blue-700 border-blue-200',   icon: null,                                                            label: 'Student' },
};

const normalizeRole = (r = '') => r.toUpperCase();
const getEntityId = (entity) => {
  if (!entity) return '';
  if (typeof entity === 'object') {
    return (entity._id || entity.id || '').toString();
  }
  return entity.toString();
};
const isEmojiOnly = (text = '') => {
  const value = text.trim();
  return Boolean(value) && /^(?:\p{Extended_Pictographic}|\p{Emoji_Modifier}|\uFE0F|\u200D|\s)+$/u.test(value);
};
const STICKER_CATEGORIES = [
  {
    key: 'popular',
    label: 'Popular',
    icon: Sparkles,
    stickers: ['👍', '❤️', '😂', '😮', '😢', '😡', '👏', '🔥', '🎉', '✅', '🚀', '💯'],
  },
  {
    key: 'faces',
    label: 'Faces',
    icon: Smile,
    stickers: ['😀', '😃', '😄', '😁', '😆', '😅', '😂', '🤣', '😊', '😇', '🙂', '🙃', '😉', '😍', '🥰', '😘', '😎', '🤩', '🥳', '😭', '😤', '🤔', '🫡', '😴'],
  },
  {
    key: 'reactions',
    label: 'Reactions',
    icon: ThumbsUp,
    stickers: ['👍', '👎', '👌', '✌️', '🤞', '🤟', '🤘', '🤙', '👏', '🙌', '🫶', '💪', '🙏', '☝️', '👀', '💯', '✅', '❌'],
  },
  {
    key: 'celebrate',
    label: 'Celebrate',
    icon: PartyPopper,
    stickers: ['🎉', '🎊', '🥳', '🎂', '🎁', '🏆', '🥇', '⭐', '🌟', '✨', '🔥', '🚀', '🎈', '🍾', '🕺', '💃'],
  },
  {
    key: 'work',
    label: 'Work',
    icon: Lightbulb,
    stickers: ['💡', '📌', '📣', '📝', '📊', '📈', '📉', '🎯', '🧠', '🔍', '⚡', '✅', '⏰', '📅', '💻', '🛠️', '📚', '✍️'],
  },
  {
    key: 'love',
    label: 'Love',
    icon: Heart,
    stickers: ['❤️', '🧡', '💛', '💚', '💙', '💜', '🖤', '🤍', '🤎', '💖', '💗', '💓', '💞', '💕', '💌', '🫶', '😍', '🥰'],
  },
];
const MESSAGE_REACTIONS = ['👍', '❤️', '😂', '😮', '😢', '👏'];

const roleBadge = (role) => {
  const cfg = roleConfig[normalizeRole(role)] || roleConfig.STUDENT;
  return (
    <span className={`inline-flex items-center gap-0.5 text-[9px] font-bold px-1.5 py-0.5 rounded-md border uppercase tracking-wider ${cfg.color}`}>
      {cfg.icon}{cfg.label}
    </span>
  );
};

function StickerPicker({ activeCategory, onCategoryChange, onSelect }) {
  const category = STICKER_CATEGORIES.find((item) => item.key === activeCategory)
    || STICKER_CATEGORIES[0];

  return (
    <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-lg">
      <div className="flex items-center border-b border-slate-100 bg-slate-50/80 px-1">
        {STICKER_CATEGORIES.map(({ key, label, icon: Icon }) => (
          <button
            key={key}
            type="button"
            onClick={() => onCategoryChange(key)}
            title={label}
            aria-label={label}
            aria-pressed={activeCategory === key}
            className={`flex h-10 flex-1 items-center justify-center border-b-2 transition-colors ${
              activeCategory === key
                ? 'border-primary text-primary'
                : 'border-transparent text-slate-400 hover:bg-white hover:text-slate-700'
            }`}
          >
            <Icon className="h-4 w-4" />
          </button>
        ))}
      </div>

      <div className="max-h-48 overflow-y-auto p-2">
        <div className="grid grid-cols-6 gap-1 sm:grid-cols-8">
          {category.stickers.map((emoji, index) => (
            <button
              key={`${category.key}-${emoji}-${index}`}
              type="button"
              onClick={() => onSelect({ emoji, label: category.label })}
              className="flex aspect-square min-h-10 items-center justify-center rounded-lg text-2xl transition-transform hover:bg-slate-100 hover:scale-110 active:scale-95"
              aria-label={`${category.label} sticker ${index + 1}`}
            >
              {emoji}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

const groupReactions = (reactions = [], currentUserId = '') => {
  const grouped = new Map();

  reactions.forEach((reaction) => {
    if (!reaction?.emoji) return;
    const userId = (reaction.userId?._id || reaction.userId || '').toString();
    const userName = reaction.userId?.name || reaction.userName || 'Member';
    const current = grouped.get(reaction.emoji) || {
      emoji: reaction.emoji,
      count: 0,
      reactedByMe: false,
      names: [],
    };
    current.count += 1;
    current.reactedByMe = current.reactedByMe || userId === currentUserId;
    current.names.push(userName);
    grouped.set(reaction.emoji, current);
  });

  return [...grouped.values()];
};

// ─── Avatar fallback ─────────────────────────────────────────────────────────
const Avatar = ({ src, name, size = 'md' }) => {
  const sz = size === 'sm' ? 'w-7 h-7 text-xs' : 'w-8 h-8 text-sm';
  const initial = (name || '?')[0].toUpperCase();
  if (src) return <img src={src} alt={name} className={`${sz} rounded-full border border-slate-200 object-cover shrink-0`} />;
  return (
    <div className={`${sz} rounded-full bg-gradient-to-br from-primary-400 to-primary-600 flex items-center justify-center text-white font-bold shrink-0`}>
      {initial}
    </div>
  );
};

// ─── Members Panel ───────────────────────────────────────────────────────────
function MembersPanel({ chatGroupId, currentUserId, onClose, onNicknameChange }) {
  const [data, setData]         = useState(null);
  const [loading, setLoading]   = useState(true);
  const [editing, setEditing]   = useState(false);
  const [nickInput, setNickInput] = useState('');
  const [saving, setSaving]     = useState(false);

  const fetchMembers = async () => {
    try {
      setLoading(true);
      const res = await chatApi.getMembers(chatGroupId);
      setData(res?.data || res);
    } catch {
      toast.error('Could not load member list.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (chatGroupId) fetchMembers();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [chatGroupId]);

  // Pre-fill current user nickname
  useEffect(() => {
    if (data?.members) {
      const me = data.members.find(m => m.userId?.toString() === currentUserId?.toString());
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setNickInput(me?.nickname || '');
    }
  }, [data, currentUserId]);

  const handleSaveNickname = async () => {
    setSaving(true);
    try {
      await chatApi.updateNickname(chatGroupId, nickInput.trim() || null);
      toast.success('Nickname updated!');
      setEditing(false);
      // Notify parent to update nicknameMap immediately
      if (onNicknameChange) {
        onNicknameChange(currentUserId, nickInput.trim() || null);
      }
      fetchMembers();
    } catch {
      toast.error('Could not save nickname.');
    } finally {
      setSaving(false);
    }
  };

  const myMember = data?.members?.find(m => m.userId?.toString() === currentUserId?.toString());

  return (
    <>
    <button
      type="button"
      aria-label="Close members panel"
      onClick={onClose}
      className="fixed inset-0 z-[55] bg-slate-900/30 md:hidden"
    />
    <div className="fixed inset-y-0 right-0 z-[60] flex w-[min(22rem,100vw)] flex-col border-l border-slate-100 bg-white shadow-2xl md:static md:z-auto md:w-72 md:shrink-0 md:shadow-none">
      {/* Header */}
      <div className="px-4 py-3 border-b border-slate-100 flex items-center justify-between bg-slate-50">
        <div className="flex items-center gap-2">
          <Users className="w-4 h-4 text-primary" />
          <span className="font-bold text-sm text-slate-800">Members</span>
          {data?.members && (
            <span className="text-xs text-slate-400 font-medium">({data.members.length})</span>
          )}
        </div>
        <button onClick={onClose} className="text-slate-400 hover:text-slate-600 transition-colors cursor-pointer">
          <X className="w-4 h-4" />
        </button>
      </div>

      {/* My Nickname section */}
      {myMember !== undefined && (
        <div className="mx-3 my-3 p-3 rounded-xl border border-primary-100 bg-primary-50/40">
          <div className="flex items-center justify-between mb-2">
            <span className="text-xs font-bold text-primary-700 flex items-center gap-1">
              <BadgeCheck className="w-3.5 h-3.5" /> Your Nickname
            </span>
            {!editing && (
              <button
                onClick={() => setEditing(true)}
                className="text-[11px] text-primary hover:text-primary-700 font-semibold flex items-center gap-0.5 cursor-pointer"
              >
                <Pencil className="w-3 h-3" /> Edit
              </button>
            )}
          </div>
          {editing ? (
            <div className="flex gap-1.5">
              <input
                type="text"
                value={nickInput}
                onChange={e => setNickInput(e.target.value)}
                maxLength={50}
                placeholder="Enter nickname..."
                className="flex-1 text-xs px-2.5 py-1.5 border border-primary-200 rounded-lg focus:outline-none focus:border-primary bg-white"
                onKeyDown={e => { if (e.key === 'Enter') handleSaveNickname(); if (e.key === 'Escape') setEditing(false); }}
                autoFocus
              />
              <button
                onClick={handleSaveNickname}
                disabled={saving}
                className="px-2.5 py-1.5 bg-primary text-white text-xs rounded-lg font-semibold hover:bg-primary-600 disabled:opacity-50 cursor-pointer transition-all"
              >
                {saving ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : 'Save'}
              </button>
              <button
                onClick={() => setEditing(false)}
                className="px-2 py-1.5 text-slate-400 hover:text-slate-600 text-xs rounded-lg cursor-pointer"
              >
                <X className="w-3.5 h-3.5" />
              </button>
            </div>
          ) : (
            <p className="text-sm font-semibold text-slate-700">
              {myMember?.nickname ? (
                <span className="text-primary-700">{myMember.nickname}</span>
              ) : (
                <span className="text-slate-400 italic text-xs">No nickname set</span>
              )}
            </p>
          )}
        </div>
      )}

      {/* Members list */}
      <div className="flex-1 overflow-y-auto p-3 space-y-1.5">
        {loading ? (
          <div className="flex justify-center pt-6">
            <Loader2 className="w-5 h-5 animate-spin text-primary" />
          </div>
        ) : !data?.members?.length ? (
          <p className="text-xs text-center text-slate-400 pt-6">No members found.</p>
        ) : (
          data.members.map((m, i) => (
            <div
              key={m.userId || m.studentId || i}
              className="flex items-start gap-2.5 p-2.5 rounded-xl hover:bg-slate-50 transition-colors"
            >
              <Avatar src={m.avatar} name={m.displayName} size="sm" />
              <div className="flex-1 min-w-0">
                {/* Nickname (nếu có) hiển thị nổi bật */}
                {m.nickname ? (
                  <>
                    <p className="text-xs font-bold text-primary-700 break-words leading-snug">
                      {m.nickname}
                    </p>
                    <p className="text-[10px] text-slate-500 font-medium break-words leading-snug mt-0.5">
                      {m.displayName || 'Unknown'}
                    </p>
                  </>
                ) : (
                  <p className="text-xs font-semibold text-slate-800 break-words leading-snug">
                    {m.displayName || 'Unknown'}
                  </p>
                )}
                <div className="flex items-center gap-1.5 flex-wrap mt-1">
                  {roleBadge(m.role)}
                  {m.userId?.toString() === currentUserId?.toString() && (
                    <span className="text-[9px] text-primary font-bold bg-primary-50 border border-primary-100 px-1.5 py-0.5 rounded-md">
                      You
                    </span>
                  )}
                </div>
                {m.email && (
                  <p className="text-[10px] text-slate-400 break-words mt-0.5">{m.email}</p>
                )}
              </div>
            </div>
          ))
        )}
      </div>
    </div>
    </>
  );
}

// ─── Main GroupChat Component ─────────────────────────────────────────────────
export default function GroupChat() {
  const { user } = useContext(AuthContext);
  const [searchParams] = useSearchParams();
  const initialGroupId = searchParams.get('groupId');

  const [channels, setChannels]             = useState([]);
  const [selectedChannel, setSelectedChannel] = useState(null);
  const [messages, setMessages]             = useState([]);
  const [inputText, setInputText]           = useState('');
  const [loadingChannels, setLoadingChannels] = useState(true);
  const [loadingMessages, setLoadingMessages] = useState(false);
  const [searchQuery, setSearchQuery]       = useState('');
  const [showMembers, setShowMembers]       = useState(false);
  const [showMobileChannels, setShowMobileChannels] = useState(false);
  // userId → nickname map cho channel đang chọn
  const [nicknameMap, setNicknameMap]       = useState({});
  const [channelMembers, setChannelMembers] = useState([]);
  const [editingMessage, setEditingMessage] = useState(null);
  const [showStickerPicker, setShowStickerPicker] = useState(false);
  const [activeStickerCategory, setActiveStickerCategory] = useState('popular');
  const [reactionPickerMessageId, setReactionPickerMessageId] = useState(null);

  const [selectedFile, setSelectedFile] = useState(null);
  const [uploading, setUploading]       = useState(false);

  const socketRef        = useRef(null);
  const chatEndRef       = useRef(null);
  const fileInputRef     = useRef(null);
  const selectedChannelRef = useRef(null); // track current channel for reconnect
  const currentUserId = user?._id || user?.id;
  const currentUserIdString = getEntityId(currentUserId);

  function scrollToBottom() {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }

  // ─── Socket.io ───────────────────────────────────────────────────────────
  useEffect(() => {
    if (!runtimeConfig.realtime.enabled) {
      socketRef.current = null;
      return;
    }

    let socket = null;
    try {
      socket = io(runtimeConfig.realtime.origin, {
        withCredentials: true,
        transports: ['websocket', 'polling'],
        autoConnect: false,
        reconnectionAttempts: 2,
        timeout: 2000,
      });
      socketRef.current = socket;

      socket.on('connect_error', () => {
        // Silently catch connection refusal without spamming global errors
      });

      socket.on('connect', () => {
        if (selectedChannelRef.current) {
          socket.emit('join_room', selectedChannelRef.current._id);
        }
      });

      socket.on('receive_message', (msg) => {
        setMessages(prev => {
          if (prev.some(m => m._id === msg._id)) return prev;
          return [...prev, msg];
        });
        setTimeout(scrollToBottom, 100);
      });

      socket.on('message_updated', (msg) => {
        setMessages(prev => prev.map(m => (m._id === msg._id ? msg : m)));
      });

      socket.on('message_revoked', (msg) => {
        setMessages(prev => prev.map(m => (m._id === msg._id ? msg : m)));
      });

      socket.on('message_reaction_updated', (msg) => {
        setMessages(prev => prev.map(m => (m._id === msg._id ? msg : m)));
      });

      // Try connecting safely
      socket.connect();
    } catch {
      // Ignore socket setup errors gracefully
    }

    return () => {
      if (socket) socket.disconnect();
    };
  }, []);

  // ─── Load Channels ────────────────────────────────────────────────────────
  useEffect(() => {
    const fetchChannels = async () => {
      try {
        const res = await chatApi.getChannels();
        const data = res?.data || res || [];
        setChannels(data);
        if (data.length > 0) {
          const target = initialGroupId ? data.find(c => c._id === initialGroupId) : data[0];
          setSelectedChannel(target || data[0]);
        }
      } catch {
        toast.error('Could not load chat channels.');
      } finally {
        setLoadingChannels(false);
      }
    };
    fetchChannels();
  }, [initialGroupId]);

  // ─── Join Room, Load History & build Nickname Map ───────────────────────────
  useEffect(() => {
    if (!selectedChannel) return;
    selectedChannelRef.current = selectedChannel; // track for reconnect
    const joinAndLoad = async () => {
      setLoadingMessages(true);
      setShowMembers(false);
      setNicknameMap({});
      setChannelMembers([]);
      setEditingMessage(null);
      setShowStickerPicker(false);
      setActiveStickerCategory('popular');
      setReactionPickerMessageId(null);
      try {
        socketRef.current?.emit('leave_room', selectedChannel._id);
        // Only join if socket is already connected; connect handler handles the rest
        if (socketRef.current?.connected) {
          socketRef.current.emit('join_room', selectedChannel._id);
        }

        // Load messages & members concurrently
        const [msgRes, memberRes] = await Promise.allSettled([
          chatApi.getMessages(selectedChannel._id),
          chatApi.getMembers(selectedChannel._id),
        ]);

        if (msgRes.status === 'fulfilled') {
          setMessages(msgRes.value?.data || msgRes.value || []);
          setTimeout(scrollToBottom, 100);
        } else {
          toast.error('Could not load message history.');
        }

        // Build { userId: nickname } map
        if (memberRes.status === 'fulfilled') {
          const members = memberRes.value?.data?.members || memberRes.value?.members || [];
          setChannelMembers(members);
          const map = {};
          members.forEach(m => {
            if (m.userId && m.nickname) {
              map[m.userId.toString()] = m.nickname;
            }
          });
          setNicknameMap(map);
        }
      } finally {
        setLoadingMessages(false);
      }
    };
    joinAndLoad();
  }, [selectedChannel]);

  const handleFileSelect = (e) => {
    const file = e.target.files[0];
    if (!file) return;
    if (file.size > 10 * 1024 * 1024) { toast.error('File must not exceed 10MB'); return; }
    setSelectedFile(file);
  };

  const getMemberDisplayName = (member) => member?.nickname || member?.displayName || member?.email || 'member';
  const getMentionToken = (member) => `@${getMemberDisplayName(member).replace(/\s+/g, '_')}`;
  const mentionMatch = inputText.match(/@([\p{L}\p{N}_-]*)$/u);
  const mentionSuggestions = mentionMatch
    ? channelMembers
        .filter(member => {
          const query = mentionMatch[1].toLowerCase();
          return getMemberDisplayName(member).toLowerCase().includes(query)
            || member.email?.toLowerCase().includes(query);
        })
        .slice(0, 5)
    : [];

  const insertMention = (member) => {
    const token = `${getMentionToken(member)} `;
    setInputText(prev => prev.replace(/@([\p{L}\p{N}_-]*)$/u, token));
  };

  const buildMentions = () => channelMembers
    .filter(member => inputText.includes(getMentionToken(member)))
    .map(member => ({
      userId: member.userId || null,
      name: getMemberDisplayName(member),
    }));

  // ─── Send Message ─────────────────────────────────────────────────────────
  const handleSendMessage = async (e) => {
    e.preventDefault();
    if (!selectedChannel) return;
    if (!inputText.trim() && !selectedFile) return;

    if (!runtimeConfig.realtime.enabled || !socketRef.current) {
      toast.error('Real-time chat is not enabled in this environment.');
      return;
    }

    if (editingMessage) {
      socketRef.current.emit('edit_message', {
        messageId: editingMessage._id,
        senderId: currentUserId,
        text: inputText.trim(),
      });
      setEditingMessage(null);
      setInputText('');
      return;
    }

    let attachmentPayload = null;
    if (selectedFile) {
      setUploading(true);
      try {
        const formData = new FormData();
        formData.append('file', selectedFile);
        const res = await chatApi.uploadFile(formData);
        const d = res?.data || res;
        attachmentPayload = { url: d.url, name: d.name, fileType: d.fileType };
      } catch (err) {
        toast.error(err?.message || 'Upload failed');
        setUploading(false);
        return;
      }
      setUploading(false);
    }

    const myNickname = nicknameMap[currentUserIdString] || null;

    // Normalize role to uppercase ENUM value expected by backend schema
    const validRoles = ['ADMIN', 'LECTURER', 'STUDENT', 'MENTOR'];
    const rawRole = (user?.role || 'STUDENT').toUpperCase();
    const senderRole = validRoles.includes(rawRole) ? rawRole : 'STUDENT';

    if (!currentUserIdString) {
      toast.error('Unable to send the message because the user is not authenticated.');
      return;
    }

    if (!socketRef.current?.connected) {
      toast.error('The real-time connection was lost. Please try again shortly.');
      return;
    }

    socketRef.current.emit('send_message', {
      chatGroupId: selectedChannel._id,
      senderId:    user?._id || user?.id,
      senderName:  myNickname || user?.name || 'Anonymous',
      senderRole,
      text:        inputText.trim(),
      attachment:  attachmentPayload,
      mentions:    buildMentions(),
    });
    setInputText('');
    setSelectedFile(null);
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const handleSendSticker = ({ emoji, label }) => {
    if (!selectedChannel || !socketRef.current?.connected || !currentUserIdString) return;

    const validRoles = ['ADMIN', 'LECTURER', 'STUDENT', 'MENTOR'];
    const rawRole = (user?.role || 'STUDENT').toUpperCase();
    const senderRole = validRoles.includes(rawRole) ? rawRole : 'STUDENT';
    const myNickname = nicknameMap[currentUserIdString] || null;

    socketRef.current.emit('send_message', {
      chatGroupId: selectedChannel._id,
      senderId: currentUserId,
      senderName: myNickname || user?.name || 'Anonymous',
      senderRole,
      sticker: { emoji, label: label || 'Sticker' },
    });
    setShowStickerPicker(false);
  };

  const handleStartEdit = (message) => {
    setEditingMessage(message);
    setInputText(message.text || '');
    setSelectedFile(null);
    setShowStickerPicker(false);
  };

  const handleRevokeMessage = (message) => {
    if (!socketRef.current?.connected || !currentUserIdString) return;
    socketRef.current.emit('revoke_message', {
      messageId: message._id,
      senderId: currentUserId,
    });
  };

  const handleReactMessage = (messageId, emoji) => {
    if (!socketRef.current?.connected || !currentUserIdString) return;
    socketRef.current.emit('react_message', {
      messageId,
      userId: currentUserId,
      emoji,
    });
    setReactionPickerMessageId(null);
  };

  const filteredChannels = channels.filter(c =>
    c.groupName?.toLowerCase().includes(searchQuery.toLowerCase()) ||
    c.team?.teamName?.toLowerCase().includes(searchQuery.toLowerCase()) ||
    c.class?.classCode?.toLowerCase().includes(searchQuery.toLowerCase())
  );

  if (loadingChannels) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[50vh]">
        <Loader2 className="w-8 h-8 text-primary animate-spin" />
        <p className="text-sm text-slate-400 mt-2 font-medium">Loading chat...</p>
      </div>
    );
  }

  return (
    <div className="relative mx-auto flex h-[calc(100dvh-96px)] max-w-7xl overflow-hidden rounded-2xl border border-slate-200/60 bg-white shadow-xs sm:h-[calc(100vh-130px)]">

      {/* 1. Left Channels Sidebar */}
      {showMobileChannels && (
        <button
          type="button"
          aria-label="Close chat list"
          onClick={() => setShowMobileChannels(false)}
          className="fixed inset-0 z-40 bg-slate-900/30 md:hidden"
        />
      )}
      <div className={`${showMobileChannels ? 'flex' : 'hidden'} fixed inset-y-0 left-0 z-50 w-[min(22rem,100vw)] flex-col border-r border-slate-100 bg-slate-50/95 shadow-2xl backdrop-blur md:static md:z-auto md:flex md:w-72 md:shrink-0 md:bg-slate-50/50 md:shadow-none md:backdrop-blur-0`}>
        {/* Search header */}
        <div className="p-4 border-b border-slate-100 bg-white">
          <div className="mb-3 flex items-center justify-between md:hidden">
            <div>
              <p className="text-sm font-bold text-slate-900">Chat groups</p>
              <p className="text-xs text-slate-400">{filteredChannels.length} available</p>
            </div>
            <button
              type="button"
              onClick={() => setShowMobileChannels(false)}
              className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700"
              aria-label="Close chat groups"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
          <div className="relative">
            <Search className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
            <input
              type="text"
              placeholder="Search chat groups..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full pl-9 pr-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:border-primary transition-all bg-slate-50/30"
            />
          </div>
        </div>

        {/* Channels listing */}
        <div className="flex-1 overflow-y-auto p-2 space-y-1">
          {filteredChannels.length === 0 ? (
            <div className="text-center py-8 text-xs text-slate-400 font-medium">
              No chat groups found.
            </div>
          ) : (
            filteredChannels.map((c) => {
              const isSelected = selectedChannel?._id === c._id;
              const lastMsgText = c.lastMessage?.messageType === 'STICKER'
                ? `${c.lastMessage?.sticker?.emoji || 'Sticker'} Sticker`
                : c.lastMessage?.text || 'No messages yet';
              const lastMsgSender = c.lastMessage?.senderName ? `${c.lastMessage.senderName}: ` : '';

              return (
                <button
                  key={c._id}
                  onClick={() => {
                    setSelectedChannel(c);
                    setShowMobileChannels(false);
                  }}
                  className={`w-full text-left p-3 rounded-xl transition-all flex items-start gap-3 relative ${
                    isSelected
                      ? 'bg-primary-50/80 border-primary-100 text-primary shadow-xs'
                      : 'hover:bg-slate-100/60 text-slate-700'
                  }`}
                >
                  <div className={`w-10 h-10 rounded-xl flex items-center justify-center shrink-0 ${
                    isSelected ? 'bg-primary text-white' : 'bg-slate-200 text-slate-500'
                  }`}>
                    <MessageSquare className="w-5 h-5" />
                  </div>

                  <div className="min-w-0 flex-1">
                    {/* Group name in EXE201g format */}
                    <p className={`font-bold text-sm truncate ${isSelected ? 'text-primary' : 'text-slate-800'}`}>
                      {c.groupName}
                    </p>
                    <p className="text-[10px] font-semibold text-slate-400 uppercase tracking-wider mt-0.5">
                      {c.class?.classCode || 'Startup Group'} • {c.memberCount ?? 0} members
                    </p>
                    <p className="text-xs text-slate-400 truncate mt-1">
                      <span className="font-semibold">{lastMsgSender}</span>{lastMsgText}
                    </p>
                  </div>
                </button>
              );
            })
          )}
        </div>
      </div>

      {/* 2. Right Chat Screen */}
      <div className="flex-1 flex flex-col bg-slate-50/20 min-w-0">
        {selectedChannel ? (
          <>
            {/* Header */}
            <div className="flex shrink-0 items-center justify-between gap-3 border-b border-slate-100 bg-white px-3 py-3 shadow-2xs sm:px-6 sm:py-4">
              <div className="flex min-w-0 items-center gap-2 sm:gap-3">
                <button
                  type="button"
                  onClick={() => setShowMobileChannels(true)}
                  className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-slate-100 text-slate-600 hover:bg-slate-200 md:hidden"
                  aria-label="Open chat groups"
                >
                  <Menu className="h-4 w-4" />
                </button>
                <div className="hidden h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary-100 text-primary sm:flex">
                  <MessageSquare className="w-5 h-5" />
                </div>
                <div className="min-w-0">
                  <h2 className="truncate text-sm font-bold leading-tight text-slate-900 sm:text-base">
                    {selectedChannel.groupName}
                  </h2>
                  <p className="mt-0.5 truncate font-mono text-[11px] text-slate-400 sm:text-xs">
                    Class: {selectedChannel.class?.classCode || '—'} · {selectedChannel.team?.teamCode || 'General'} · {channelMembers.length || selectedChannel.memberCount || 0} members
                  </p>
                </div>
              </div>

              {/* Toggle Members Panel */}
              <button
                onClick={() => setShowMembers(v => !v)}
                title="View members"
                className={`flex shrink-0 items-center gap-1.5 rounded-xl px-2.5 py-2 text-sm font-semibold transition-all cursor-pointer sm:px-3 ${
                  showMembers
                    ? 'bg-primary text-white shadow-sm'
                    : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                }`}
              >
                <Users className="w-4 h-4" />
                <span className="hidden sm:inline">Members</span>
                {(channelMembers.length > 0 || (selectedChannel?.memberCount ?? 0) > 0) && (
                  <span className="text-xs font-bold px-1.5 py-0.5 rounded-md bg-white/20">
                    {channelMembers.length || selectedChannel?.memberCount}
                  </span>
                )}
                <ChevronRight className={`w-3.5 h-3.5 transition-transform ${showMembers ? 'rotate-180' : ''}`} />
              </button>
            </div>

            {/* Messages + optional Members panel */}
            <div className="flex-1 flex overflow-hidden">
              {/* Messages Area */}
              <div className="flex-1 space-y-3 overflow-y-auto p-3 sm:space-y-4 sm:p-6">
                {loadingMessages ? (
                  <div className="flex flex-col items-center justify-center h-full">
                    <Loader2 className="w-6 h-6 text-primary animate-spin" />
                    <p className="text-xs text-slate-400 mt-1 font-medium">Loading messages...</p>
                  </div>
                ) : messages.length === 0 ? (
                  <div className="flex flex-col items-center justify-center h-full text-slate-300">
                    <MessageSquare className="w-16 h-16 stroke-1 mb-2 animate-bounce" />
                    <p className="text-sm font-semibold">Welcome to the group chat!</p>
                    <p className="text-xs text-slate-400 mt-0.5">Start the conversation with your group.</p>
                  </div>
                ) : (
                  messages.map((m, index) => {
                    const senderId = getEntityId(m.senderId);
                    const isMine = Boolean(
                      currentUserIdString && senderId === currentUserIdString
                    );
                    const senderAvatar = typeof m.senderId === 'object'
                      ? m.senderId?.avatar
                      : null;
                    const formattedTime = new Date(m.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
                    // Ưu tiên nickname từ map; fallback về senderName đã lưu
                    const resolvedName = (senderId && nicknameMap[senderId]) || m.senderName;
                    const hasNickname  = senderId && !!nicknameMap[senderId];
                    const canManageMessage = isMine && !m.isRevoked;
                    const isStickerMessage = !m.isRevoked
                      && m.messageType === 'STICKER'
                      && Boolean(m.sticker?.emoji);
                    const isEmojiOnlyMessage = !m.isRevoked && isEmojiOnly(m.text);
                    const reactionGroups = groupReactions(m.reactions, currentUserIdString);

                    return (
                      <div
                        key={m._id || index}
                        className={`flex w-full items-start gap-2 sm:gap-3 ${
                          isMine ? 'justify-end' : 'justify-start'
                        }`}
                      >
                        {/* Avatar */}
                        {!isMine && (
                          senderAvatar ? (
                            <img src={senderAvatar} alt={resolvedName} className="h-7 w-7 shrink-0 rounded-full border border-slate-100 object-cover sm:h-8 sm:w-8" />
                          ) : (
                            <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full border border-slate-300 bg-gradient-to-br from-slate-300 to-slate-400 sm:h-8 sm:w-8">
                              <User className="h-3.5 w-3.5 text-white sm:h-4 sm:w-4" />
                            </div>
                          )
                        )}

                        {/* Bubble */}
                        <div className={`min-w-0 max-w-[92%] space-y-1 sm:max-w-[80%] ${
                          isStickerMessage ? 'w-fit' : ''
                        }`}>
                          <div className={`relative flex items-center gap-1.5 flex-wrap ${isMine ? 'justify-end' : ''}`}>
                            {/* Tên hiển thị: nickname (primary) + tên thật nhỏ bên cạnh */}
                            {!isMine && (
                              <>
                                <span className={`text-xs font-bold ${hasNickname ? 'text-primary-700' : 'text-slate-800'}`}>
                                  {resolvedName}
                                </span>
                                {hasNickname && (
                                  <span className="text-[10px] text-slate-400 font-medium">({m.senderName})</span>
                                )}
                                {roleBadge(m.senderRole)}
                              </>
                            )}
                            {!m.isRevoked && (
                              <button
                                type="button"
                                onClick={() => setReactionPickerMessageId(
                                  reactionPickerMessageId === m._id ? null : m._id
                                )}
                                className="rounded-md p-1 text-slate-400 transition-colors hover:bg-slate-100 hover:text-amber-500"
                                title="React to message"
                                aria-label="React to message"
                                aria-expanded={reactionPickerMessageId === m._id}
                              >
                                <Smile className="h-3.5 w-3.5" />
                              </button>
                            )}
                            {canManageMessage && (
                              <span className="ml-1 inline-flex overflow-hidden rounded-lg border border-slate-200/60">
                                {m.text && (
                                  <button
                                    type="button"
                                    onClick={() => handleStartEdit(m)}
                                    className="px-1.5 py-1 text-slate-400 hover:bg-slate-100 hover:text-primary"
                                    title="Edit message"
                                  >
                                    <Pencil className="h-3 w-3" />
                                  </button>
                                )}
                                <button
                                  type="button"
                                  onClick={() => handleRevokeMessage(m)}
                                  className="px-1.5 py-1 text-slate-400 hover:bg-red-50 hover:text-red-500"
                                  title="Revoke message"
                                >
                                  <RotateCcw className="h-3 w-3" />
                                </button>
                              </span>
                            )}

                            {reactionPickerMessageId === m._id && (
                              <div className={`absolute top-full z-30 mt-1 flex items-center gap-1 rounded-full border border-slate-200 bg-white p-1.5 shadow-xl ${
                                isMine ? 'right-0' : 'left-0'
                              }`}>
                                {MESSAGE_REACTIONS.map((emoji) => (
                                  <button
                                    key={emoji}
                                    type="button"
                                    onClick={() => handleReactMessage(m._id, emoji)}
                                    className="flex h-8 w-8 items-center justify-center rounded-full text-lg transition-transform hover:scale-125 hover:bg-slate-100"
                                    aria-label={`React ${emoji}`}
                                  >
                                    {emoji}
                                  </button>
                                ))}
                              </div>
                            )}
                          </div>

                          <div className={`break-words text-sm leading-relaxed ${
                            isEmojiOnlyMessage ? 'text-center' : ''
                          } ${
                            isStickerMessage
                              ? 'w-fit bg-transparent p-0'
                              : `rounded-2xl p-2.5 sm:p-3 ${
                                  isMine
                                    ? 'rounded-tr-none bg-primary text-white'
                                    : 'rounded-tl-none border border-slate-200/60 bg-white text-slate-800 shadow-3xs'
                                }`
                          }`}>
                            {m.isRevoked ? (
                              <p className={`italic ${isMine ? 'text-primary-100' : 'text-slate-400'}`}>
                                Tin nhắn đã được thu hồi
                              </p>
                            ) : (
                              <>
                                {m.messageType === 'STICKER' && m.sticker?.emoji && (
                                  <div
                                    className="inline-flex h-14 min-w-14 items-center justify-center text-[52px] leading-none drop-shadow-sm"
                                    title={m.sticker.label || 'Sticker'}
                                  >
                                    {m.sticker.emoji}
                                  </div>
                                )}
                                {m.text && (
                                  <p className={`whitespace-pre-line ${isEmojiOnlyMessage ? 'text-2xl leading-none' : ''}`}>
                                    {m.text}
                                  </p>
                                )}
                              </>
                            )}

                            {!m.isRevoked && m.attachment?.url && (
                              <div className="mt-2 max-w-[70vw] sm:max-w-xs">
                                {m.attachment.fileType === 'image' ? (
                                  <a href={m.attachment.url} target="_blank" rel="noopener noreferrer">
                                    <img
                                      src={m.attachment.url}
                                      alt={m.attachment.name || 'Attachment'}
                                      className="max-w-full max-h-48 rounded-lg object-cover cursor-pointer hover:opacity-90 transition-opacity"
                                    />
                                  </a>
                                ) : (
                                  <a
                                    href={m.attachment.url}
                                    target="_blank"
                                    rel="noopener noreferrer"
                                    className={`flex items-center gap-2 p-2.5 rounded-xl border text-xs font-semibold ${
                                      isMine
                                        ? 'bg-primary-600/50 border-primary-500 text-white hover:bg-primary-600'
                                        : 'bg-slate-50 border-slate-200 text-slate-700 hover:bg-slate-100'
                                    } transition-all`}
                                  >
                                    <Paperclip className="w-4 h-4 shrink-0" />
                                    <span className="max-w-[150px] truncate">{m.attachment.name || 'Download file'}</span>
                                  </a>
                                )}
                              </div>
                            )}

                            <span className={`flex items-center gap-1 text-[9px] ${
                              isMine ? 'justify-end text-right' : 'justify-start text-left'
                            } ${
                              isStickerMessage ? 'mt-0.5 text-slate-400' : `mt-1.5 opacity-60 ${
                                isMine ? 'text-primary-100' : 'text-slate-400'
                              }`
                            }`}>
                              <Clock className="w-2.5 h-2.5" />
                              {formattedTime}{m.isEdited ? ' · edited' : ''}
                            </span>
                          </div>

                          {reactionGroups.length > 0 && (
                            <div className={`flex flex-wrap gap-1 ${isMine ? 'justify-end' : ''}`}>
                              {reactionGroups.map((reaction) => (
                                <button
                                  key={reaction.emoji}
                                  type="button"
                                  onClick={() => handleReactMessage(m._id, reaction.emoji)}
                                  title={reaction.names.join(', ')}
                                  className={`inline-flex h-6 items-center gap-1 rounded-full border px-2 text-xs transition-colors ${
                                    reaction.reactedByMe
                                      ? 'border-primary-200 bg-primary-50 text-primary'
                                      : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'
                                  }`}
                                >
                                  <span>{reaction.emoji}</span>
                                  <span className="text-[10px] font-bold">{reaction.count}</span>
                                </button>
                              ))}
                            </div>
                          )}
                        </div>
                      </div>
                    );
                  })
                )}
                <div ref={chatEndRef} />
              </div>

              {/* Members Panel */}
              {showMembers && (
                <MembersPanel
                  chatGroupId={selectedChannel._id}
                  currentUserId={currentUserId}
                  onClose={() => setShowMembers(false)}
                  onNicknameChange={(userId, nickname) => {
                    setNicknameMap(prev => {
                      const next = { ...prev };
                      if (nickname) {
                        next[userId.toString()] = nickname;
                      } else {
                        delete next[userId.toString()];
                      }
                      return next;
                    });
                  }}
                />
              )}
            </div>

            {/* Input Form */}
            <div className="shrink-0 space-y-2 border-t border-slate-100 bg-white p-3 sm:p-4">
              {editingMessage && (
                <div className="flex items-center justify-between gap-2 rounded-xl border border-primary-100 bg-primary-50 px-3 py-2 text-xs text-primary">
                  <span className="font-semibold">Editing message</span>
                  <button
                    type="button"
                    onClick={() => { setEditingMessage(null); setInputText(''); }}
                    className="font-bold hover:text-primary-700"
                  >
                    Cancel
                  </button>
                </div>
              )}
              {uploading && (
                <div className="flex items-center gap-2 text-xs text-slate-500 bg-slate-50 p-2 rounded-lg">
                  <Loader2 className="w-3.5 h-3.5 animate-spin text-primary" />
                  <span>Uploading file...</span>
                </div>
              )}
              {selectedFile && (
                <div className="flex items-center justify-between gap-2 text-xs text-slate-600 bg-slate-100/80 px-3 py-1.5 rounded-xl border border-slate-200">
                  <div className="flex items-center gap-1.5 min-w-0">
                    <Paperclip className="w-3.5 h-3.5 text-slate-400 shrink-0" />
                    <span className="truncate font-semibold">{selectedFile.name}</span>
                  </div>
                  <button
                    type="button"
                    onClick={() => setSelectedFile(null)}
                    className="text-red-500 hover:text-red-700 font-bold shrink-0 ml-2 cursor-pointer"
                  >
                    Remove
                  </button>
                </div>
              )}
              {showStickerPicker && (
                <StickerPicker
                  activeCategory={activeStickerCategory}
                  onCategoryChange={setActiveStickerCategory}
                  onSelect={handleSendSticker}
                />
              )}
              {mentionSuggestions.length > 0 && (
                <div className="flex flex-wrap gap-2 rounded-xl border border-slate-200 bg-white p-2 shadow-sm">
                  {mentionSuggestions.map((member, index) => (
                    <button
                      key={member.userId || member.studentId || index}
                      type="button"
                      onClick={() => insertMention(member)}
                      className="rounded-lg bg-slate-50 px-2.5 py-1.5 text-xs font-semibold text-slate-700 hover:bg-primary-50 hover:text-primary"
                    >
                      {getMentionToken(member)}
                    </button>
                  ))}
                </div>
              )}
              <form onSubmit={handleSendMessage} className="flex gap-2">
                <input
                  type="file"
                  ref={fileInputRef}
                  onChange={handleFileSelect}
                  className="hidden"
                  accept="image/*,application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,application/zip,application/x-zip-compressed,text/plain"
                />
                <button
                  type="button"
                  onClick={() => fileInputRef.current?.click()}
                  disabled={uploading || !!editingMessage}
                  className="shrink-0 rounded-xl border border-slate-200 p-3 text-slate-500 transition-all hover:bg-slate-50 cursor-pointer"
                  title="Upload image or file"
                >
                  <Paperclip className="w-4 h-4" />
                </button>
                <button
                  type="button"
                  onClick={() => setShowStickerPicker(v => !v)}
                  disabled={!!editingMessage}
                  className="shrink-0 rounded-xl border border-slate-200 p-3 text-slate-500 transition-all hover:bg-slate-50 cursor-pointer disabled:opacity-50"
                  title="Send sticker"
                >
                  <Smile className="w-4 h-4" />
                </button>
                <input
                  type="text"
                  value={inputText}
                  onChange={(e) => setInputText(e.target.value)}
                  placeholder={editingMessage ? 'Edit your message...' : selectedFile ? 'Add a message or press Send...' : 'Type a message... Use @ to tag'}
                  className="min-w-0 flex-1 rounded-xl border border-slate-200 bg-slate-50/20 px-3 py-3 text-sm transition-all focus:outline-none focus:border-primary sm:px-4"
                />
                <button
                  type="submit"
                  disabled={uploading || (!inputText.trim() && !selectedFile)}
                  className="flex shrink-0 items-center gap-1.5 rounded-xl bg-primary px-3 py-3 text-sm font-semibold text-white transition-all hover:bg-primary-600 disabled:opacity-50 active:scale-95 cursor-pointer sm:px-4"
                >
                  <span className="hidden sm:inline">{editingMessage ? 'Save' : 'Send'}</span>
                  {editingMessage ? <Check className="w-4 h-4" /> : <Send className="w-4 h-4" />}
                </button>
              </form>
            </div>
          </>
        ) : (
          <div className="flex-1 flex flex-col items-center justify-center text-slate-300">
            <MessageSquare className="w-16 h-16 stroke-1 mb-2" />
            <p className="text-sm font-semibold">No chat channel selected</p>
          </div>
        )}
      </div>

    </div>
  );
}
