// @ts-nocheck
import { useState, useEffect, useMemo } from 'react';
import { X, Calendar, Clock, MapPin, Link2, Users, Upload, Image as ImageIcon, FileText, Trash2, Globe, Monitor, Search, ArrowRightLeft } from 'lucide-react';
import { classApi } from '../../api/classApi';
import { toClassViewModel, unwrapApiData } from '../../utils/classMappers';
import { classFeatureFlags } from '../../config/classFeatureFlags';
import { workshopApi } from '../../api/workshopApi';
import { userApi } from '../../api/userApi';
import { chatApi } from '../../api/chatApi';
import toast from 'react-hot-toast';
import WorkshopPreviewModal from './WorkshopPreviewModal';

const WorkshopForm = ({ isOpen, onClose, workshop, onSave }) => {
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [type, setType] = useState('WORKSHOP');
  const [targetAudience, setTargetAudience] = useState('CLASS');

  const [onlineClassIds, setOnlineClassIds] = useState([]);
  const [offlineClassIds, setOfflineClassIds] = useState([]);
  const [teamIds, setTeamIds] = useState([]);

  const [bannerUrl, setBannerUrl] = useState('');
  const [attachments, setAttachments] = useState([]);
  const [checkInDeadline, setCheckInDeadline] = useState('');

  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [startTime, setStartTime] = useState('');
  const [endTime, setEndTime] = useState('');

  const [location, setLocation] = useState('');
  const [meetingLink, setMeetingLink] = useState('');

  const [selectedMentors, setSelectedMentors] = useState([]);
  const [status, setStatus] = useState('PUBLISHED');

  const [availableClasses, setAvailableClasses] = useState([]);
  const [availableTeams, setAvailableTeams] = useState([]);
  const [availableMentors, setAvailableMentors] = useState([]);
  const [loading, setLoading] = useState(false);
  const [uploading, setUploading] = useState(false);

  // New UI states
  const [primaryMode, setPrimaryMode] = useState('ONLINE'); // 'ONLINE' or 'OFFLINE'
  const [classSearch, setClassSearch] = useState('');
  const [showPreview, setShowPreview] = useState(false);
  const [onlinePage, setOnlinePage] = useState(1);
  const [offlinePage, setOfflinePage] = useState(1);
  const ITEMS_PER_PAGE = 10;

  const todayStr = useMemo(() => {
    const today = new Date();
    const yyyy = today.getFullYear();
    const mm = String(today.getMonth() + 1).padStart(2, '0');
    const dd = String(today.getDate()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}`;
  }, []);

  const minStartDate = useMemo(() => {
    if (!workshop) return todayStr;
    if (workshop.startDate) {
      const start = new Date(workshop.startDate).toISOString().split('T')[0];
      if (start < todayStr) {
        return start;
      }
    }
    return todayStr;
  }, [workshop, todayStr]);

  const minEndDate = useMemo(() => {
    if (startDate) return startDate;
    return minStartDate;
  }, [startDate, minStartDate]);

  const minCheckInDeadline = useMemo(() => {
    const nowLocalStr = new Date(new Date().getTime() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16);
    if (!workshop) return nowLocalStr;
    if (workshop.checkInDeadline) {
      const deadline = new Date(workshop.checkInDeadline).toISOString().slice(0, 16);
      if (deadline < nowLocalStr) {
        return deadline;
      }
    }
    return nowLocalStr;
  }, [workshop]);

  const maxCheckInDeadline = useMemo(() => {
    if (endDate && endTime) {
      return `${endDate}T${endTime}`;
    }
    return undefined;
  }, [endDate, endTime]);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [clsRes, userRes] = await Promise.all([
          classApi.getAll(),
          userApi.getAll({ role: 'MENTOR' })
        ]);

        const classPayload = unwrapApiData(clsRes);
        const cList = (classPayload?.items || []).map(toClassViewModel);
        setAvailableClasses(cList);

        const uList = userRes.data?.users || userRes.users || userRes.data || [];
        setAvailableMentors(Array.isArray(uList) ? uList : []);
      } catch (err) {
        toast.error('Failed to load initial data');
      }
    };
    if (isOpen) {
      fetchData();
    }
  }, [isOpen]);

  useEffect(() => {
    const fetchTeams = async () => {
      if (!classFeatureFlags.teamManagement) {
        setAvailableTeams([]);
        return;
      }

      const allSelectedClassIds = [...onlineClassIds, ...offlineClassIds];
      if (allSelectedClassIds.length === 0) {
        setAvailableTeams([]);
        return;
      }
      try {
        const promises = allSelectedClassIds.map(id => classApi.getTeams(id));
        const results = await Promise.all(promises);
        let allTeams = [];
        results.forEach(res => {
          const list = res.data || res || [];
          if (Array.isArray(list)) {
            allTeams = [...allTeams, ...list];
          }
        });
        setAvailableTeams(allTeams);
      } catch (err) {
        toast.error('Failed to load teams');
      }
    };
    fetchTeams();
  }, [onlineClassIds, offlineClassIds]);

  useEffect(() => {
    if (workshop) {
      setTitle(workshop.title || '');
      setDescription(workshop.description || '');
      setType(workshop.type || 'WORKSHOP');
      setTargetAudience(workshop.targetAudience || 'CLASS');

      const onIds = workshop.onlineClassIds?.map(c => c._id || c) || [];
      const offIds = workshop.offlineClassIds?.map(c => c._id || c) || [];
      setOnlineClassIds(onIds);
      setOfflineClassIds(offIds);

      setTeamIds(workshop.teamIds?.map(t => t._id || t) || []);
      setBannerUrl(workshop.bannerUrl || '');
      setAttachments(workshop.attachments || []);
      setCheckInDeadline(workshop.checkInDeadline ? new Date(workshop.checkInDeadline).toISOString().slice(0, 16) : '');

      setStartDate(workshop.startDate ? new Date(workshop.startDate).toISOString().split('T')[0] : '');
      setEndDate(workshop.endDate ? new Date(workshop.endDate).toISOString().split('T')[0] : '');
      setStartTime(workshop.startTime || '');
      setEndTime(workshop.endTime || '');
      setLocation(workshop.location || '');
      setMeetingLink(workshop.meetingLink || '');
      setSelectedMentors(workshop.mentors?.map(m => m._id || m) || []);
      setStatus(workshop.status || 'PUBLISHED');

      // Determine primary mode based on existing data
      if (onIds.length > 0) {
        setPrimaryMode('ONLINE');
      } else if (offIds.length > 0) {
        setPrimaryMode('OFFLINE');
      }
    } else {
      resetForm();
    }
  }, [workshop, isOpen]);

  // Handle automatic distribution of classes when changing modes or toggling checkboxes
  const handleClassSelection = (classId, isSelected) => {
    if (primaryMode === 'ONLINE') {
      if (isSelected) {
        setOnlineClassIds(prev => [...prev, classId]);
        setOfflineClassIds(prev => prev.filter(id => id !== classId));
      } else {
        setOnlineClassIds(prev => prev.filter(id => id !== classId));
        setOfflineClassIds(prev => [...prev, classId]);
      }
    } else {
      if (isSelected) {
        setOfflineClassIds(prev => [...prev, classId]);
        setOnlineClassIds(prev => prev.filter(id => id !== classId));
      } else {
        setOfflineClassIds(prev => prev.filter(id => id !== classId));
        setOnlineClassIds(prev => [...prev, classId]);
      }
    }
  };

  const handleSelectAll = () => {
    const allIds = availableClasses.map(c => c._id);
    if (primaryMode === 'ONLINE') {
      setOnlineClassIds(allIds);
      setOfflineClassIds([]);
    } else {
      setOfflineClassIds(allIds);
      setOnlineClassIds([]);
    }
  };

  const handleDeselectAll = () => {
    // Due to the auto-assignment logic requested, deselecting all in one mode
    // means assigning ALL classes to the other mode.
    const allIds = availableClasses.map(c => c._id);
    if (primaryMode === 'ONLINE') {
      setOnlineClassIds([]);
      setOfflineClassIds(allIds);
    } else {
      setOfflineClassIds([]);
      setOnlineClassIds(allIds);
    }
  };

  const swapClassMode = (classId) => {
    if (onlineClassIds.includes(classId)) {
      setOnlineClassIds(prev => prev.filter(id => id !== classId));
      setOfflineClassIds(prev => [...prev, classId]);
    } else if (offlineClassIds.includes(classId)) {
      setOfflineClassIds(prev => prev.filter(id => id !== classId));
      setOnlineClassIds(prev => [...prev, classId]);
    }
  };

  // Re-run the auto-assignment when all classes are loaded and none are selected yet (for Create mode)
  useEffect(() => {
    if (!workshop && availableClasses.length > 0 && onlineClassIds.length === 0 && offlineClassIds.length === 0 && ['CLASS', 'TEAM', 'LECTURER', 'MENTOR'].includes(targetAudience)) {
      const allIds = availableClasses.map(c => c._id);
      // All classes default to Offline initially. Admin can check any class to make it Online.
      setOfflineClassIds(allIds);
      setOnlineClassIds([]);
    }
  }, [availableClasses, workshop, targetAudience]);

  // Make sure to sync available classes with offline/online lists if new classes are fetched
  useEffect(() => {
    if (availableClasses.length > 0 && ['CLASS', 'TEAM', 'LECTURER', 'MENTOR'].includes(targetAudience)) {
      const allAvailableIds = availableClasses.map(c => c._id);
      const currentlyAssigned = new Set([...onlineClassIds, ...offlineClassIds]);

      const missingIds = allAvailableIds.filter(id => !currentlyAssigned.has(id));
      if (missingIds.length > 0) {
        // Any missing/new class defaults to Offline format
        setOfflineClassIds(prev => [...prev, ...missingIds]);
      }
    }
  }, [availableClasses, targetAudience]);

  const resetForm = () => {
    setTitle('');
    setDescription('');
    setType('WORKSHOP');
    setTargetAudience('CLASS');
    setOnlineClassIds([]);
    setOfflineClassIds([]);
    setTeamIds([]);
    setBannerUrl('');
    setAttachments([]);
    setCheckInDeadline('');
    setStartDate('');
    setEndDate('');
    setStartTime('');
    setEndTime('');
    setLocation('');
    setMeetingLink('');
    setSelectedMentors([]);
    setStatus('PUBLISHED');
    setPrimaryMode('ONLINE');
    setClassSearch('');
    setOnlinePage(1);
    setOfflinePage(1);
  };

  const getDerivedFormat = () => {
    if (onlineClassIds.length > 0 && offlineClassIds.length > 0) return 'HYBRID';
    if (onlineClassIds.length > 0) return 'ONLINE';
    if (offlineClassIds.length > 0) return 'OFFLINE';
    return 'OFFLINE';
  };

  const validateForm = () => {
    if (!title || !startDate || !endDate || !startTime || !endTime) {
      toast.error('Please fill in all required fields');
      return false;
    }

    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const checkStartDate = new Date(startDate);
    checkStartDate.setHours(0, 0, 0, 0);

    const checkEndDate = new Date(endDate);
    checkEndDate.setHours(0, 0, 0, 0);

    const originalStart = workshop?.startDate ? new Date(workshop.startDate) : null;
    if (originalStart) originalStart.setHours(0, 0, 0, 0);

    const originalEnd = workshop?.endDate ? new Date(workshop.endDate) : null;
    if (originalEnd) originalEnd.setHours(0, 0, 0, 0);

    const isNew = !workshop?._id;
    const isStartChanged = originalStart && checkStartDate.getTime() !== originalStart.getTime();
    const isEndChanged = originalEnd && checkEndDate.getTime() !== originalEnd.getTime();

    if ((isNew || isStartChanged) && checkStartDate < today) {
      toast.error('Start Date cannot be in the past');
      return false;
    }

    if ((isNew || isEndChanged) && checkEndDate < today) {
      toast.error('End Date cannot be in the past');
      return false;
    }

    if (checkStartDate > checkEndDate) {
      toast.error('Start Date must be before or equal to End Date');
      return false;
    }

    if (startDate === endDate && startTime >= endTime) {
      toast.error('Start Time must be before End Time when on the same day');
      return false;
    }

    if (checkInDeadline) {
      const deadlineDate = new Date(checkInDeadline);
      const now = new Date();
      
      const originalDeadline = workshop?.checkInDeadline ? new Date(workshop.checkInDeadline) : null;
      const isDeadlineChanged = !originalDeadline || deadlineDate.getTime() !== originalDeadline.getTime();

      if ((isNew || isDeadlineChanged) && deadlineDate < now) {
        toast.error('Check-in Deadline cannot be in the past');
        return false;
      }

      const workshopEnd = new Date(`${endDate}T${endTime}`);
      if (deadlineDate > workshopEnd) {
        toast.error('Check-in Deadline cannot be after the Workshop End Time');
        return false;
      }
    }

    if (targetAudience === 'CLASS' || targetAudience === 'TEAM' || targetAudience === 'LECTURER' || targetAudience === 'MENTOR') {
      if (onlineClassIds.length === 0 && offlineClassIds.length === 0) {
        toast.error('Please select at least one online or offline class');
        return false;
      }
    }

    if (targetAudience === 'TEAM' && teamIds.length === 0) {
      toast.error('Please select at least one team');
      return false;
    }

    if (onlineClassIds.length > 0 && !meetingLink) {
      toast.error('Meeting Link is required for online classes');
      return false;
    }

    if (meetingLink) {
      const urlPattern = /^(https?:\/\/)[^\s/$.?#].[^\s]*$/i;
      if (!urlPattern.test(meetingLink)) {
        toast.error('Please enter a valid URL for Meeting Link (starting with http:// or https://)');
        return false;
      }
    }

    if (offlineClassIds.length > 0 && !location) {
      toast.error('Location is required for offline classes');
      return false;
    }

    return true;
  };

  const handlePreviewClick = (e) => {
    e.preventDefault();
    if (validateForm()) {
      setShowPreview(true);
    }
  };

  const handleSubmit = async () => {
    const payload = {
      title,
      description,
      type,
      targetAudience,
      onlineClassIds: ['CLASS', 'TEAM', 'LECTURER', 'MENTOR'].includes(targetAudience) ? onlineClassIds : [],
      offlineClassIds: ['CLASS', 'TEAM', 'LECTURER', 'MENTOR'].includes(targetAudience) ? offlineClassIds : [],
      teamIds: targetAudience === 'TEAM' ? teamIds : [],
      bannerUrl,
      attachments,
      checkInDeadline: checkInDeadline || null,
      mentors: selectedMentors,
      startDate,
      endDate,
      startTime,
      endTime,
      location,
      meetingLink,
      status,
    };

    setLoading(true);
    try {
      if (workshop?._id) {
        await workshopApi.update(workshop._id, payload);
        toast.success('Workshop updated successfully');
      } else {
        await workshopApi.create(payload);
        toast.success('Workshop created successfully');
      }
      setShowPreview(false);
      onSave();
      onClose();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Failed to save workshop');
    } finally {
      setLoading(false);
    }
  };

  const handleFileUpload = async (e, type) => {
    const file = e.target.files[0];
    if (!file) return;

    if (file.size > 10 * 1024 * 1024) {
      toast.error('File size must be under 10MB');
      return;
    }

    setUploading(true);
    try {
      const formData = new FormData();
      formData.append('file', file);
      const res = await chatApi.uploadFile(formData);
      const url = res.data?.url || res.url;

      if (type === 'banner') {
        setBannerUrl(url);
      } else {
        setAttachments(prev => [...prev, url]);
      }
    } catch (err) {
      toast.error('Failed to upload file');
    } finally {
      setUploading(false);
      e.target.value = '';
    }
  };

  const toggleTeam = (tId) => {
    setTeamIds(prev => prev.includes(tId) ? prev.filter(id => id !== tId) : [...prev, tId]);
  };

  const toggleMentor = (mId) => {
    setSelectedMentors(prev => prev.includes(mId) ? prev.filter(id => id !== mId) : [...prev, mId]);
  };

  const filteredClasses = useMemo(() => {
    if (!classSearch.trim()) return availableClasses;
    const lowerSearch = classSearch.toLowerCase();
    return availableClasses.filter(c =>
      (c.classCode || '').toLowerCase().includes(lowerSearch) ||
      (c.subjectCode || '').toLowerCase().includes(lowerSearch)
    );
  }, [availableClasses, classSearch]);

  const workshopPayload = {
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
    format: getDerivedFormat()
  };

  if (!isOpen) return null;

  return (
    <>
      <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
        <div className="bg-white rounded-2xl w-full max-w-5xl border border-slate-200/60 shadow-2xl flex flex-col max-h-[90vh]">
          {/* Header */}
          <div className="flex justify-between items-center px-6 py-4 border-b border-slate-100 shrink-0">
            <h2 className="text-xl font-bold text-slate-800 flex items-center gap-2">
              <Calendar className="w-5 h-5 text-primary" />
              {workshop?._id ? 'Edit Event / Workshop' : 'Schedule Event / Workshop'}
            </h2>
            <div className="flex items-center gap-3">
              <span className="text-xs font-semibold px-2.5 py-1 rounded-full bg-slate-100 text-slate-700 shadow-sm border border-slate-200">
                Format: {getDerivedFormat()}
              </span>
              <button onClick={onClose} className="p-1.5 hover:bg-slate-100 rounded-lg text-slate-400 hover:text-slate-600 transition-colors">
                <X className="w-5 h-5" />
              </button>
            </div>
          </div>

          {/* Content */}
          <form className="flex-1 overflow-y-auto p-6">
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
              {/* Left Column - Main Info */}
              <div className="col-span-1 lg:col-span-2 space-y-6">

                {/* Basic Info */}
                <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-sm space-y-5">
                  <h3 className="text-sm font-bold text-slate-800 border-b border-slate-100 pb-2">Basic Information</h3>

                  <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                    <div className="md:col-span-2">
                      <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-2">Title *</label>
                      <input
                        type="text"
                        value={title}
                        onChange={e => setTitle(e.target.value)}
                        className="w-full bg-slate-50 border border-slate-200 rounded-xl px-4 py-2.5 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all"
                        required
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-2">Type</label>
                      <select
                        value={type}
                        onChange={e => setType(e.target.value)}
                        className="w-full bg-slate-50 border border-slate-200 rounded-xl px-4 py-2.5 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all"
                      >
                        <option value="WORKSHOP">Workshop</option>
                        <option value="SEMINAR">Seminar</option>
                      </select>
                    </div>
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-2">Description</label>
                    <textarea
                      value={description}
                      onChange={e => setDescription(e.target.value)}
                      rows={3}
                      className="w-full bg-slate-50 border border-slate-200 rounded-xl px-4 py-2.5 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all"
                    />
                  </div>
                </div>

                {/* Dates & Times */}
                <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-sm space-y-5">
                  <h3 className="text-sm font-bold text-slate-800 border-b border-slate-100 pb-2">Schedule & Location</h3>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <div>
                      <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-2 flex items-center gap-1">
                        <Calendar className="w-3.5 h-3.5" /> Start Date *
                      </label>
                      <input
                        type="date"
                        value={startDate}
                        min={minStartDate}
                        onChange={e => setStartDate(e.target.value)}
                        className="w-full bg-slate-50 border border-slate-200 rounded-xl px-4 py-2.5 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all"
                        required
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-2 flex items-center gap-1">
                        <Calendar className="w-3.5 h-3.5" /> End Date *
                      </label>
                      <input
                        type="date"
                        value={endDate}
                        min={minEndDate}
                        onChange={e => setEndDate(e.target.value)}
                        className="w-full bg-slate-50 border border-slate-200 rounded-xl px-4 py-2.5 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all"
                        required
                      />
                    </div>
                  </div>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <div>
                      <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-2 flex items-center gap-1">
                        <Clock className="w-3.5 h-3.5" /> Start Time *
                      </label>
                      <input
                        type="time"
                        value={startTime}
                        onChange={e => setStartTime(e.target.value)}
                        className="w-full bg-slate-50 border border-slate-200 rounded-xl px-4 py-2.5 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all"
                        required
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-2 flex items-center gap-1">
                        <Clock className="w-3.5 h-3.5" /> End Time *
                      </label>
                      <input
                        type="time"
                        value={endTime}
                        onChange={e => setEndTime(e.target.value)}
                        className="w-full bg-slate-50 border border-slate-200 rounded-xl px-4 py-2.5 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all"
                        required
                      />
                    </div>
                  </div>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <div>
                      <label className="block text-xs font-bold text-amber-600 uppercase tracking-wider mb-2 flex items-center gap-1">
                        <MapPin className="w-3.5 h-3.5" /> Offline Location {offlineClassIds.length > 0 && '*'}
                      </label>
                      <input
                        type="text"
                        value={location}
                        onChange={e => setLocation(e.target.value)}
                        className={`w-full bg-slate-50 border ${offlineClassIds.length > 0 && !location ? 'border-red-300 focus:border-red-500 focus:ring-red-500/20' : 'border-slate-200 focus:border-primary focus:ring-primary/20'} rounded-xl px-4 py-2.5 text-sm text-slate-900 focus:outline-none focus:ring-2 transition-all`}
                        placeholder="e.g. Room AL-L203"
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-indigo-600 uppercase tracking-wider mb-2 flex items-center gap-1">
                        <Link2 className="w-3.5 h-3.5" /> Online Link {onlineClassIds.length > 0 && '*'}
                      </label>
                      <input
                        type="url"
                        value={meetingLink}
                        onChange={e => setMeetingLink(e.target.value)}
                        className={`w-full bg-slate-50 border ${onlineClassIds.length > 0 && !meetingLink ? 'border-red-300 focus:border-red-500 focus:ring-red-500/20' : 'border-slate-200 focus:border-primary focus:ring-primary/20'} rounded-xl px-4 py-2.5 text-sm text-slate-900 focus:outline-none focus:ring-2 transition-all`}
                        placeholder="https://meet.google.com/..."
                      />
                    </div>
                  </div>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 border-t border-slate-100 pt-4">
                    <div>
                      <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-2 flex items-center gap-1">
                        <Clock className="w-3.5 h-3.5" /> Check-in Deadline
                      </label>
                      <input
                        type="datetime-local"
                        value={checkInDeadline}
                        min={minCheckInDeadline}
                        max={maxCheckInDeadline}
                        onChange={e => setCheckInDeadline(e.target.value)}
                        className="w-full bg-slate-50 border border-slate-200 rounded-xl px-4 py-2.5 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all"
                      />
                    </div>
                  </div>
                </div>
                {/* Target Audience & Class Groups Selection */}
                <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-sm space-y-4">
                  <div className="flex justify-between items-center border-b border-slate-100 pb-2">
                    <h3 className="text-sm font-bold text-slate-800">Class Participation</h3>
                    <select
                      value={targetAudience}
                      onChange={e => {
                        setTargetAudience(e.target.value);
                        if (e.target.value === 'ALL_STUDENTS') {
                          setOnlineClassIds([]);
                          setOfflineClassIds([]);
                          setTeamIds([]);
                        }
                      }}
                      className="bg-slate-50 border border-slate-200 rounded-lg px-3 py-1.5 text-xs focus:outline-none focus:border-primary font-semibold text-slate-700"
                    >
                      <option value="CLASS">Class Students</option>
                      <option value="TEAM">Specific Teams</option>
                      <option value="LECTURER">Lecturers</option>
                      <option value="MENTOR">Mentors</option>
                      <option value="ALL_STUDENTS">All Students</option>
                    </select>
                  </div>

                  {['CLASS', 'TEAM', 'LECTURER', 'MENTOR'].includes(targetAudience) && (
                    <div className="space-y-6">
                      <p className="text-xs text-slate-500">
                        Assign classes to either Online or Offline format. Selecting a class in one section will automatically place the remaining classes in the other section.
                      </p>

                      {/* Search Bar for Classes */}
                      <div className="relative w-full max-w-md">
                        <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                        <input
                          type="text"
                          value={classSearch}
                          onChange={(e) => setClassSearch(e.target.value)}
                          placeholder="Search classes by code or subject..."
                          className="w-full pl-9 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-sm focus:outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 transition-all font-semibold text-slate-800"
                        />
                      </div>

                      {/* Online & Offline Section Grid */}
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">

                        {/* ONLINE CLASSES SECTION */}
                        <div className="border border-indigo-100 rounded-2xl bg-indigo-50/20 overflow-hidden flex flex-col shadow-sm">
                          <div className="bg-indigo-100/50 px-4 py-3 border-b border-indigo-100 flex justify-between items-center shrink-0">
                            <div className="flex items-center gap-2 text-sm font-bold text-indigo-700">
                              <Monitor className="w-4 h-4" />
                              ONLINE CLASSES
                              <span className="bg-indigo-200 text-indigo-800 px-2 py-0.5 rounded-full text-xs font-bold shadow-sm">
                                {onlineClassIds.length} Selected
                              </span>
                            </div>
                            <div className="flex items-center gap-2">
                              <button
                                type="button"
                                onClick={() => {
                                  // Assign all to Online (meaning Offline becomes empty)
                                  const allIds = availableClasses.map(c => c._id);
                                  setOnlineClassIds(allIds);
                                  setOfflineClassIds([]);
                                  setOnlinePage(1);
                                }}
                                className="px-2.5 py-1 bg-white hover:bg-indigo-50 text-indigo-700 rounded-lg text-xs font-bold transition-all border border-indigo-200 shadow-sm"
                              >
                                Select All
                              </button>
                            </div>
                          </div>

                          {/* Online List */}
                          <div className="p-3 max-h-[340px] overflow-y-auto space-y-2 flex-1">
                            {filteredClasses.length === 0 ? (
                              <p className="text-xs text-center text-slate-400 py-6 italic">No classes match search</p>
                            ) : (
                              (() => {
                                const totalPages = Math.ceil(filteredClasses.length / ITEMS_PER_PAGE);
                                const currentPage = Math.min(onlinePage, totalPages || 1);
                                const startIndex = (currentPage - 1) * ITEMS_PER_PAGE;
                                const paginatedList = filteredClasses.slice(startIndex, startIndex + ITEMS_PER_PAGE);

                                return (
                                  <>
                                    <div className="space-y-2">
                                      {paginatedList.map(c => {
                                        const isOnline = onlineClassIds.includes(c._id);
                                        return (
                                          <label
                                            key={c._id}
                                            className={`flex items-center gap-3 p-2.5 rounded-xl border cursor-pointer transition-all ${isOnline ? 'bg-indigo-50/80 border-indigo-300 shadow-sm' : 'bg-white border-slate-100 opacity-60 hover:opacity-100'}`}
                                          >
                                            <input
                                              type="checkbox"
                                              checked={isOnline}
                                              onChange={(e) => {
                                                if (e.target.checked) {
                                                  // Tick: move to Online
                                                  setOnlineClassIds(prev => [...prev, c._id]);
                                                  setOfflineClassIds(prev => prev.filter(id => id !== c._id));
                                                } else {
                                                  // Untick: move to Offline
                                                  setOnlineClassIds(prev => prev.filter(id => id !== c._id));
                                                  setOfflineClassIds(prev => [...prev, c._id]);
                                                }
                                              }}
                                              className="w-4 h-4 rounded text-indigo-600 focus:ring-indigo-500 cursor-pointer"
                                            />
                                            <div className="flex flex-col min-w-0">
                                              <span className="text-sm font-bold text-slate-800 truncate">{c.classCode}</span>
                                              <span className="text-[10px] font-semibold text-slate-500 truncate">{c.subjectCode}</span>
                                            </div>
                                            {isOnline && (
                                              <span className="ml-auto bg-indigo-100 text-indigo-700 px-2 py-0.5 rounded text-[10px] font-bold shadow-sm">
                                                ONLINE
                                              </span>
                                            )}
                                          </label>
                                        );
                                      })}
                                    </div>

                                    {/* Pagination Controls */}
                                    {totalPages > 1 && (
                                      <div className="flex justify-between items-center pt-3 border-t border-indigo-100/50 text-xs mt-2 shrink-0">
                                        <button
                                          type="button"
                                          disabled={currentPage === 1}
                                          onClick={() => setOnlinePage(prev => Math.max(prev - 1, 1))}
                                          className="px-2 py-1 bg-white border border-slate-200 rounded hover:bg-slate-50 disabled:opacity-50 text-slate-600 font-bold"
                                        >
                                          Prev
                                        </button>
                                        <span className="text-slate-500 font-semibold">
                                          Page {currentPage} of {totalPages}
                                        </span>
                                        <button
                                          type="button"
                                          disabled={currentPage === totalPages}
                                          onClick={() => setOnlinePage(prev => Math.min(prev + 1, totalPages))}
                                          className="px-2 py-1 bg-white border border-slate-200 rounded hover:bg-slate-50 disabled:opacity-50 text-slate-600 font-bold"
                                        >
                                          Next
                                        </button>
                                      </div>
                                    )}
                                  </>
                                );
                              })()
                            )}
                          </div>
                        </div>

                        {/* OFFLINE CLASSES SECTION */}
                        <div className="border border-amber-100 rounded-2xl bg-amber-50/20 overflow-hidden flex flex-col shadow-sm">
                          <div className="bg-amber-100/50 px-4 py-3 border-b border-amber-100 flex justify-between items-center shrink-0">
                            <div className="flex items-center gap-2 text-sm font-bold text-amber-700">
                              <Globe className="w-4 h-4" />
                              OFFLINE CLASSES
                              <span className="bg-amber-200 text-amber-800 px-2 py-0.5 rounded-full text-xs font-bold shadow-sm">
                                {offlineClassIds.length} Selected
                              </span>
                            </div>
                            <div className="flex items-center gap-2">
                              <button
                                type="button"
                                onClick={() => {
                                  // Assign all to Offline (meaning Online becomes empty)
                                  const allIds = availableClasses.map(c => c._id);
                                  setOfflineClassIds(allIds);
                                  setOnlineClassIds([]);
                                  setOfflinePage(1);
                                }}
                                className="px-2.5 py-1 bg-white hover:bg-amber-50 text-amber-700 rounded-lg text-xs font-bold transition-all border border-amber-200 shadow-sm"
                              >
                                Select All
                              </button>
                            </div>
                          </div>

                          {/* Offline List */}
                          <div className="p-3 max-h-[340px] overflow-y-auto space-y-2 flex-1">
                            {filteredClasses.length === 0 ? (
                              <p className="text-xs text-center text-slate-400 py-6 italic">No classes match search</p>
                            ) : (
                              (() => {
                                const totalPages = Math.ceil(filteredClasses.length / ITEMS_PER_PAGE);
                                const currentPage = Math.min(offlinePage, totalPages || 1);
                                const startIndex = (currentPage - 1) * ITEMS_PER_PAGE;
                                const paginatedList = filteredClasses.slice(startIndex, startIndex + ITEMS_PER_PAGE);

                                return (
                                  <>
                                    <div className="space-y-2">
                                      {paginatedList.map(c => {
                                        const isOffline = offlineClassIds.includes(c._id);
                                        return (
                                          <label
                                            key={c._id}
                                            className={`flex items-center gap-3 p-2.5 rounded-xl border cursor-pointer transition-all ${isOffline ? 'bg-amber-50/80 border-amber-300 shadow-sm' : 'bg-white border-slate-100 opacity-60 hover:opacity-100'}`}
                                          >
                                            <input
                                              type="checkbox"
                                              checked={isOffline}
                                              onChange={(e) => {
                                                if (e.target.checked) {
                                                  // Tick: move to Offline
                                                  setOfflineClassIds(prev => [...prev, c._id]);
                                                  setOnlineClassIds(prev => prev.filter(id => id !== c._id));
                                                } else {
                                                  // Untick: move to Online
                                                  setOfflineClassIds(prev => prev.filter(id => id !== c._id));
                                                  setOnlineClassIds(prev => [...prev, c._id]);
                                                }
                                              }}
                                              className="w-4 h-4 rounded text-amber-600 focus:ring-amber-500 cursor-pointer"
                                            />
                                            <div className="flex flex-col min-w-0">
                                              <span className="text-sm font-bold text-slate-800 truncate">{c.classCode}</span>
                                              <span className="text-[10px] font-semibold text-slate-500 truncate">{c.subjectCode}</span>
                                            </div>
                                            {isOffline && (
                                              <span className="ml-auto bg-amber-100 text-amber-700 px-2 py-0.5 rounded text-[10px] font-bold shadow-sm">
                                                OFFLINE
                                              </span>
                                            )}
                                          </label>
                                        );
                                      })}
                                    </div>

                                    {/* Pagination Controls */}
                                    {totalPages > 1 && (
                                      <div className="flex justify-between items-center pt-3 border-t border-amber-100/50 text-xs mt-2 shrink-0">
                                        <button
                                          type="button"
                                          disabled={currentPage === 1}
                                          onClick={() => setOfflinePage(prev => Math.max(prev - 1, 1))}
                                          className="px-2 py-1 bg-white border border-slate-200 rounded hover:bg-slate-50 disabled:opacity-50 text-slate-600 font-bold"
                                        >
                                          Prev
                                        </button>
                                        <span className="text-slate-500 font-semibold">
                                          Page {currentPage} of {totalPages}
                                        </span>
                                        <button
                                          type="button"
                                          disabled={currentPage === totalPages}
                                          onClick={() => setOfflinePage(prev => Math.min(prev + 1, totalPages))}
                                          className="px-2 py-1 bg-white border border-slate-200 rounded hover:bg-slate-50 disabled:opacity-50 text-slate-600 font-bold"
                                        >
                                          Next
                                        </button>
                                      </div>
                                    )}
                                  </>
                                );
                              })()
                            )}
                          </div>
                        </div>

                      </div>

                    </div>
                  )}

                  {/* Team Selectors */}
                  {targetAudience === 'TEAM' && (onlineClassIds.length > 0 || offlineClassIds.length > 0) && (
                    <div className="mt-4 pt-4 border-t border-slate-100">
                      <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-2 flex items-center gap-1">
                        <Users className="w-3.5 h-3.5" /> Select Teams
                      </label>
                      {availableTeams.length === 0 ? (
                        <p className="text-xs text-slate-400 italic">No teams generated yet for selected classes.</p>
                      ) : (
                        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 max-h-[160px] overflow-y-auto p-1">
                          {availableTeams.map(t => (
                            <label key={t._id} className={`flex items-center gap-2 text-xs font-semibold p-2 cursor-pointer border rounded-lg transition-colors ${teamIds.includes(t._id) ? 'bg-primary/5 border-primary text-primary' : 'bg-slate-50 border-slate-200 text-slate-700 hover:border-slate-300'}`}>
                              <input type="checkbox" checked={teamIds.includes(t._id)} onChange={() => toggleTeam(t._id)} className="rounded text-primary focus:ring-primary" />
                              <span className="truncate">{t.teamName}</span>
                            </label>
                          ))}
                        </div>
                      )}
                    </div>
                  )}

                </div>

              </div>

              {/* Right Column - Media & Mentors */}
              <div className="col-span-1 space-y-6">

                {/* Media & Attachments */}
                <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-sm">
                  <h3 className="text-sm font-bold text-slate-800 border-b border-slate-100 pb-2 mb-4">Media & Files</h3>

                  <div className="mb-5">
                    <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-2">Banner Image</label>
                    {bannerUrl ? (
                      <div className="relative rounded-xl overflow-hidden group shadow-sm border border-slate-200">
                        <img src={bannerUrl} alt="Banner" className="w-full h-32 object-cover" />
                        <div className="absolute inset-0 bg-black/50 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity">
                          <button type="button" onClick={() => setBannerUrl('')} className="p-2.5 bg-rose-500 text-white rounded-full hover:bg-rose-600 shadow-lg transform hover:scale-110 transition-all">
                            <Trash2 size={18} />
                          </button>
                        </div>
                      </div>
                    ) : (
                      <label className="flex flex-col items-center justify-center h-32 border-2 border-dashed border-slate-300 rounded-xl cursor-pointer hover:border-primary hover:bg-primary/5 transition-all group bg-slate-50">
                        <div className="p-3 bg-white rounded-full shadow-sm mb-2 group-hover:shadow-md transition-all">
                          <ImageIcon className="w-6 h-6 text-slate-400 group-hover:text-primary transition-colors" />
                        </div>
                        <span className="text-xs font-bold text-slate-500 group-hover:text-primary transition-colors">Upload Banner</span>
                        <input type="file" accept="image/*" className="hidden" onChange={e => handleFileUpload(e, 'banner')} disabled={uploading} />
                      </label>
                    )}
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-2">Attachments</label>
                    <div className="space-y-2 mb-3">
                      {attachments.map((url, idx) => (
                        <div key={idx} className="flex items-center justify-between p-2.5 bg-slate-50 rounded-lg border border-slate-200 group hover:border-slate-300 transition-colors">
                          <div className="flex items-center gap-2 truncate">
                            <div className="p-1.5 bg-white rounded shadow-sm">
                              <FileText className="w-4 h-4 text-primary flex-shrink-0" />
                            </div>
                            <span className="text-xs font-semibold text-slate-600 truncate">Attachment {idx + 1}</span>
                          </div>
                          <button type="button" onClick={() => setAttachments(prev => prev.filter((_, i) => i !== idx))} className="text-slate-400 hover:text-rose-500 transition-colors p-1">
                            <X size={16} />
                          </button>
                        </div>
                      ))}
                    </div>
                    <label className="flex items-center justify-center gap-2 w-full py-2.5 border border-dashed border-slate-300 bg-slate-50 rounded-xl text-xs font-bold text-slate-600 hover:border-primary hover:text-primary hover:bg-primary/5 cursor-pointer transition-all">
                      <Upload size={16} /> Add File
                      <input type="file" className="hidden" onChange={e => handleFileUpload(e, 'attachment')} disabled={uploading} />
                    </label>
                  </div>
                </div>

                {/* Mentors Selection */}
                <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-sm">
                  <h3 className="text-sm font-bold text-slate-800 border-b border-slate-100 pb-2 mb-4">Assign Mentors</h3>
                  {availableMentors.length === 0 ? (
                    <p className="text-xs text-slate-400 italic">No mentors available.</p>
                  ) : (
                    <div className="grid grid-cols-1 gap-2 max-h-[220px] overflow-y-auto p-1">
                      {availableMentors.map(m => (
                        <label key={m._id} className={`flex items-center gap-3 p-2.5 cursor-pointer border rounded-xl transition-colors ${selectedMentors.includes(m._id) ? 'bg-primary/5 border-primary shadow-sm' : 'bg-slate-50 border-slate-200 hover:border-slate-300'}`}>
                          <input type="checkbox" checked={selectedMentors.includes(m._id)} onChange={() => toggleMentor(m._id)} className="w-4 h-4 rounded text-primary focus:ring-primary" />
                          <div className="flex items-center gap-2 truncate">
                            <div className="w-7 h-7 rounded-full bg-slate-200 flex items-center justify-center text-xs font-bold text-slate-500 shrink-0">
                              {m.name.charAt(0).toUpperCase()}
                            </div>
                            <span className="text-sm font-semibold text-slate-700 truncate">{m.name}</span>
                          </div>
                        </label>
                      ))}
                    </div>
                  )}
                </div>

              </div>
            </div>
          </form>

          {/* Footer */}
          <div className="flex justify-between items-center px-6 py-4 border-t border-slate-100 bg-slate-50/50 shrink-0 rounded-b-2xl">
            <div className="flex gap-6">
              <label className="flex items-center gap-2 text-sm font-bold text-slate-700 cursor-pointer group">
                <input type="radio" name="status" value="PUBLISHED" checked={status === 'PUBLISHED'} onChange={() => setStatus('PUBLISHED')} className="w-4 h-4 text-primary focus:ring-primary" />
                <span className="group-hover:text-primary transition-colors">Publish</span>
              </label>
              <label className="flex items-center gap-2 text-sm font-bold text-slate-700 cursor-pointer group">
                <input type="radio" name="status" value="DRAFT" checked={status === 'DRAFT'} onChange={() => setStatus('DRAFT')} className="w-4 h-4 text-primary focus:ring-primary" />
                <span className="group-hover:text-primary transition-colors">Draft</span>
              </label>
            </div>
            <div className="flex gap-3">
              <button type="button" onClick={onClose} className="px-5 py-2.5 border border-slate-200 rounded-xl text-sm font-bold text-slate-600 hover:bg-slate-100 transition-colors shadow-sm">
                Cancel
              </button>
              <button
                type="button"
                onClick={handlePreviewClick}
                disabled={loading || uploading}
                className="bg-primary text-white font-bold px-6 py-2.5 rounded-xl text-sm hover:shadow-glow-primary hover:bg-primary-600 active:scale-[0.98] transition-all disabled:opacity-50 shadow-md"
              >
                {loading ? 'Processing...' : (workshop?._id ? 'Review Changes' : 'Review & Create')}
              </button>
            </div>
          </div>
        </div>
      </div>

      <WorkshopPreviewModal
        isOpen={showPreview}
        onClose={() => setShowPreview(false)}
        onConfirm={handleSubmit}
        workshopData={workshopPayload}
        availableClasses={availableClasses}
        isEditing={!!workshop?._id}
        loading={loading}
      />
    </>
  );
};

export default WorkshopForm;
