// @ts-nocheck
import { lazy, Suspense, useState, useEffect, useRef } from 'react';
import { useAuth } from '../../hooks/useAuth';
import { useLocation, useNavigate } from 'react-router-dom';
import { Mail, Key, User, ShieldCheck, Camera, ArrowLeft, Eye, EyeOff } from 'lucide-react';
import Button from '../../components/ui/Button';
import Badge from '../../components/ui/Badge';
import { changePassword, getCurrentUser, updateProfile } from '../../api/authApi';
import toast from 'react-hot-toast';
import { motion } from 'framer-motion';
import { TEAM_MAJOR_GROUPS, ALL_TEAM_MAJOR_CODES } from '../../constants/majors';
import { parseApiError } from '../../utils/apiError';
import { resolveProfileTab } from '../../utils/profileNavigation';
import { useTranslation } from 'react-i18next';
import i18n from '../../i18n';

const MentorProfileSection = lazy(() => import('../mentor/MentorProfilePage'));

const roleBadgeVariant = { ADMIN: 'Approved', LECTURER: 'Submitted', MENTOR: 'Review', STUDENT: 'Reviewed' };
const roleLabel = {
  get ADMIN() { return i18n.t('common:roles.ADMIN'); },
  get LECTURER() { return i18n.t('common:roles.LECTURER'); },
  get MENTOR() { return i18n.t('common:roles.MENTOR'); },
  get STUDENT() { return i18n.t('common:roles.STUDENT'); },
};

const ProfileSettings = () => {
  const { t } = useTranslation('profile');
  const { user, updateUser, refreshUser } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const role = user?.role?.toUpperCase() || 'STUDENT';
  const activeTab = resolveProfileTab(location.pathname, location.search, role);

  useEffect(() => {
    if (location.state?.message) {
      toast.error(location.state.message, { id: 'missing-major-toast', duration: 5000 });
      // clear the state so it doesn't trigger again on refresh
      window.history.replaceState({}, document.title);
    }
  }, [location]);
  
  // Profile state
  const [name, setName] = useState(user?.name || '');
  const nameEditedRef = useRef(false);
  const [avatarFile, setAvatarFile] = useState<File | null>(null);
  const avatarInputRef = useRef<HTMLInputElement>(null);
  const [major, setMajor] = useState(user?.major || '');
  const majorEditedRef = useRef(false);
  const [isSavingProfile, setIsSavingProfile] = useState(false);

  // Password state
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [isSavingPassword, setIsSavingPassword] = useState(false);

  const myClassesPath = role === 'MENTOR' ? '/mentor' : role === 'STUDENT' ? '/student/classes' : role === 'ADMIN' ? '/admin/classes' : '/lecturer/classes';

  useEffect(() => {
    if (role === 'STUDENT') {
      void refreshUser().catch(() => toast.error(t('profileSettings.unableToRefreshProfileInformationReload')));
    }
  }, [refreshUser, role]);

  useEffect(() => {
    if (!nameEditedRef.current) setName(user?.name || '');
    if (!majorEditedRef.current) setMajor(user?.major || '');
  }, [user?.name, user?.major]);

  const handleUpdateProfile = async (e) => {
    e.preventDefault();
    if (isSavingProfile) return;
    if (!name.trim()) return toast.error(t('profileSettings.nameIsRequired'));
    setIsSavingProfile(true);
    try {
      const formData = new FormData();
      formData.append('fullName', name.trim());
      if (avatarFile) {
        formData.append('avatar', avatarFile);
      }
      if (role === 'STUDENT' && major) {
        formData.append('major', major);
      }
      const profile = await updateProfile(formData);
      updateUser({
        fullName: profile.fullName,
        name: profile.fullName,
        avatarUrl: profile.avatarUrl,
        avatar: profile.avatarUrl || undefined,
        majorCode: profile.majorCode,
        major: profile.majorCode,
      });
      nameEditedRef.current = false;
      majorEditedRef.current = false;
      setAvatarFile(null);
      if (avatarInputRef.current) {
        avatarInputRef.current.value = '';
      }
      toast.success(t('profileSettings.profileUpdatedSuccessfully'));
    } catch (err) {
      const error = parseApiError(err, t('profileSettings.failedToUpdateProfile'));
      toast.error(error.message);
      if (role === 'STUDENT' && (error.code === 'MAJOR_LOCKED' || error.code === 'CLASS_ENROLLMENT_MAJOR_LOCKED')) {
        majorEditedRef.current = false;
        setMajor(user?.majorCode ?? user?.major ?? '');
        try {
          const currentUser = await getCurrentUser();
          setMajor(currentUser.majorCode ?? '');
          updateUser({ majorCode: currentUser.majorCode, major: currentUser.majorCode });
        } catch {
          toast.error(t('profileSettings.unableToRefreshYourSavedMajor'));
        }
      }
    } finally {
      setIsSavingProfile(false);
    }
  };

  const handleAvatarChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] || null;
    if (file && (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type) || file.size > 5 * 1024 * 1024)) {
      event.target.value = '';
      toast.error(t('profileSettings.chooseAJpegPngOrWebp'));
      return;
    }
    setAvatarFile(file);
  };

  const handleChangePassword = async (e) => {
    e.preventDefault();
    if (!currentPassword || !newPassword || !confirmPassword) {
      return toast.error(t('profileSettings.pleaseFillAllPasswordFields'));
    }
    if (newPassword.length < 6) {
      return toast.error(t('profileSettings.newPasswordMustBeAtLeast'));
    }
    if (newPassword !== confirmPassword) {
      return toast.error(t('profileSettings.newPasswordsDoNotMatch'));
    }
    
    setIsSavingPassword(true);
    try {
      await changePassword({ currentPassword, newPassword, confirmPassword });
      toast.success(t('profileSettings.passwordChangedSuccessfully'));
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
    } catch (err) {
      toast.error(parseApiError(err, t('profileSettings.failedToChangePassword')).message);
    } finally {
      setIsSavingPassword(false);
    }
  };

  const tabs = [
    ...(role === 'MENTOR' ? [{ id: 'mentor', label: t('profileSettings.mentorProfileCv'), icon: User, path: '/profile' }] : []),
    { id: 'profile', label: role === 'MENTOR' ? t('profileSettings.personalInformation') : t('profileSettings.profile'), icon: User, path: role === 'MENTOR' ? '/profile?tab=personal' : '/profile' },
    { id: 'password', label: t('profileSettings.securitySettings'), icon: Key, path: '/settings' },
  ];

  const isMissingMajor = role === 'STUDENT' && (!user?.major || !ALL_TEAM_MAJOR_CODES.includes(user.major.toUpperCase()));

  return (
    <div className="max-w-4xl mx-auto">
      {/* Header */}
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="mb-6 sm:mb-8">
        <h1 className="text-heading sm:text-display text-secondary tracking-tight">{activeTab === 'password' ? t('profileSettings.accountSettings') : t('profileSettings.myProfile')}</h1>
        <p className="text-slate-500 mt-1">{activeTab === 'password' ? t('profileSettings.manageYourAccountSecurityPreferences') : activeTab === 'mentor' ? t('profileSettings.manageYourExpertiseExperienceAndCv') : t('profileSettings.viewAndUpdateYourPersonalDetails')}</p>
      </motion.div>

      {/* ── Missing Major Banner ── */}
      {isMissingMajor && (
        <motion.div
          initial={{ opacity: 0, y: -12 }}
          animate={{ opacity: 1, y: 0 }}
          className="mb-6 flex items-start gap-3 bg-amber-50 border border-amber-300 rounded-2xl p-4 shadow-sm"
        >
          <span className="text-2xl">⚠️</span>
          <div>
            <p className="font-semibold text-amber-800 text-sm">{t('profileSettings.youHaveNotSelectedAMajor')}</p>
            <p className="text-xs text-amber-700 mt-0.5">
              {t('profileSettings.selectA')} <strong>{t('profileSettings.major')}</strong> {t('profileSettings.hereOrFromYourOwnRow')}
            </p>
          </div>
        </motion.div>
      )}

      {/* Profile Banner */}
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.05 }}
        className="rounded-2xl p-6 sm:p-8 mb-6 text-white relative overflow-hidden bg-primary"
      >
        <div className="absolute top-0 right-0 w-40 h-40 bg-white/10 rounded-full -translate-y-1/2 translate-x-1/2" />
        <div className="absolute bottom-0 left-1/3 w-24 h-24 bg-white/5 rounded-full translate-y-1/2" />
        <div className="flex flex-col sm:flex-row items-center gap-4 relative z-10">
          <div className="w-16 h-16 sm:w-20 sm:h-20 rounded-2xl bg-white/20 backdrop-blur-sm border-2 border-white/30 flex items-center justify-center overflow-hidden shrink-0">
            {user?.avatar ? (
              <img src={user.avatar} alt={t('profileSettings.avatar')} className="w-full h-full object-cover" />
            ) : (
              <span className="text-2xl sm:text-3xl font-bold">{user?.name?.charAt(0)?.toUpperCase() || 'U'}</span>
            )}
          </div>
          <div className="text-center sm:text-left">
            <h2 className="text-xl sm:text-2xl font-bold">{user?.name || t('profileSettings.user')}</h2>
            <p className="text-white/70 text-sm">{user?.email || ''}</p>
            <Badge variant={roleBadgeVariant[role]} size="sm" className="mt-2 bg-white/20 border-white/30 text-white">
              {roleLabel[role] || role}
            </Badge>
          </div>
        </div>
      </motion.div>

      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.1 }}
        className="bg-white rounded-2xl border border-slate-200/60 shadow-sm overflow-hidden flex flex-col md:flex-row min-h-[420px]"
      >
        {/* Sidebar Tabs */}
        <div className="w-full md:w-56 bg-slate-50/50 border-b md:border-b-0 md:border-r border-slate-200/60 p-3 md:p-4 flex md:flex-col gap-1 overflow-x-auto">
          {tabs.map(tab => (
            <button
              key={tab.id}
              aria-current={activeTab === tab.id ? 'page' : undefined}
              className={`flex items-center gap-2.5 px-4 py-2.5 rounded-xl text-sm font-medium transition-all whitespace-nowrap ${
                activeTab === tab.id
                  ? 'bg-white text-primary-700 shadow-sm border border-slate-200/50'
                  : 'text-slate-500 hover:bg-slate-100 hover:text-slate-800'
              }`}
              onClick={() => navigate(tab.path)}
            >
              <tab.icon className="w-4 h-4 shrink-0" /> {tab.label}
            </button>
          ))}
        </div>

        {/* Content */}
        <div className="flex-1 p-5 sm:p-8">
          {role === 'MENTOR' && activeTab === 'mentor' && (
            <Suspense fallback={<p role="status">{t('profileSettings.loadingMentorProfile')}</p>}>
              <MentorProfileSection embedded />
            </Suspense>
          )}
          {activeTab === 'profile' && (
            <div className="max-w-md">
              <h2 className="text-lg font-bold text-slate-900 mb-1">{t('profileSettings.profileInformation')}</h2>
              <p className="text-sm text-slate-500 mb-6">{t('profileSettings.updateYourPersonalDetails')}</p>
              <form onSubmit={handleUpdateProfile} className="space-y-5">
                <div>
                  <label htmlFor="profile-email" className="block text-sm font-medium text-slate-700 mb-1.5">{t('profileSettings.emailAddress')}</label>
                  <div className="relative">
                    <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                    <input id="profile-email" type="email" value={user?.email || ''} disabled className="w-full bg-slate-50 border border-slate-200 rounded-xl pl-10 pr-4 py-2.5 text-slate-500 cursor-not-allowed text-sm" />
                  </div>
                  <p className="text-xs text-slate-400 mt-1.5">{t('profileSettings.emailCannotBeChanged')}</p>
                </div>

                <div>
                  <label htmlFor="profile-role" className="block text-sm font-medium text-slate-700 mb-1.5">{t('profileSettings.role')}</label>
                  <div className="relative">
                    <ShieldCheck className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                    <input id="profile-role" type="text" value={roleLabel[role] || role} disabled className="w-full bg-slate-50 border border-slate-200 rounded-xl pl-10 pr-4 py-2.5 text-slate-500 cursor-not-allowed font-medium text-sm" />
                  </div>
                </div>

                <div>
                  <label htmlFor="profile-name" className="block text-sm font-medium text-slate-700 mb-1.5">{t('profileSettings.fullName')}</label>
                  <input id="profile-name" type="text" value={name} onChange={e => { nameEditedRef.current = true; setName(e.target.value); }} placeholder={t('profileSettings.yourName')} className="w-full bg-white border border-slate-200 rounded-xl px-4 py-2.5 text-slate-900 text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all outline-none" />
                </div>

                <div>
                  <label htmlFor="profile-avatar" className="block text-sm font-medium text-slate-700 mb-1.5">{t('profileSettings.avatarOptional')}</label>
                  <div className="relative">
                    <Camera className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                    <input ref={avatarInputRef} id="profile-avatar" type="file" accept="image/jpeg,image/png,image/webp" onChange={handleAvatarChange} className="w-full bg-white border border-slate-200 rounded-xl pl-10 pr-4 py-2.5 text-slate-900 text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all outline-none file:mr-3 file:border-0 file:bg-transparent file:text-sm file:font-medium" />
                  </div>
                  <p className="text-xs text-slate-400 mt-1.5">{t('profileSettings.jpegPngOrWebpMaximum5')}</p>
                </div>

                {role === 'STUDENT' && (
                    <div>
                      <label htmlFor="profile-major" className="block text-sm font-medium text-slate-700 mb-1.5">{t('profileSettings.major')}</label>
                      <select
                        id="profile-major"
                        value={major} onChange={e => { majorEditedRef.current = true; setMajor(e.target.value); }}
                        disabled={isSavingProfile}
                        className="w-full bg-white border border-slate-200 rounded-xl px-4 py-2.5 text-slate-900 text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all outline-none"
                      >
                        <option value="">{t('profileSettings.selectAMajor')}</option>
                        {TEAM_MAJOR_GROUPS.map(group => (
                          <optgroup key={group.key} label={group.label}>
                            {group.majors.map(m => (
                              <option key={m.code} value={m.code}>{m.code} - {m.name}</option>
                            ))}
                          </optgroup>
                        ))}
                      </select>
                    </div>
                )}

                <div className="pt-2 flex flex-col sm:flex-row gap-3">
                  <Button type="submit" variant="primary" isLoading={isSavingProfile} disabled={isSavingProfile}>
                    {t('profileSettings.saveChanges')}
                  </Button>
                  <Button
                    type="button"
                    variant="primary"
                    icon={ArrowLeft}
                    onClick={() => navigate(myClassesPath)}
                    className="bg-secondary hover:bg-secondary-dark hover:shadow-glow-cyan focus:ring-secondary/20"
                  >
                    {role === 'MENTOR' ? t('profileSettings.backToOverview') : t('profileSettings.backMyClasses')}
                  </Button>
                </div>
              </form>
            </div>
          )}

          {activeTab === 'password' && (
            <div className="max-w-md">
              <h2 className="text-lg font-bold text-slate-900 mb-1">{t('profileSettings.changePassword')}</h2>
              <p className="text-sm text-slate-500 mb-6">{t('profileSettings.ensureYourAccountIsSecure')}</p>
              <form onSubmit={handleChangePassword} className="space-y-5">
                <div>
                  <label htmlFor="current-pw" className="block text-sm font-medium text-slate-700 mb-1.5">{t('profileSettings.currentPassword')}</label>
                  <div className="relative">
                    <input id="current-pw" type={showCurrentPassword ? 'text' : 'password'} value={currentPassword} onChange={e => setCurrentPassword(e.target.value)} required autoComplete="current-password" placeholder="••••••••" className="w-full bg-white border border-slate-200 rounded-xl pl-4 pr-11 py-2.5 text-slate-900 text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all outline-none" />
                    <button
                      type="button"
                      onClick={() => setShowCurrentPassword(visible => !visible)}
                      aria-label={showCurrentPassword ? t('profileSettings.hideCurrentPassword') : t('profileSettings.showCurrentPassword')}
                      className="absolute right-3.5 top-1/2 -translate-y-1/2 bg-transparent border-none cursor-pointer text-slate-400 p-0 hover:text-slate-600"
                    >
                      {showCurrentPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                    </button>
                  </div>
                </div>

                <div className="w-full h-px bg-slate-100 my-2" />

                <div>
                  <label htmlFor="new-pw" className="block text-sm font-medium text-slate-700 mb-1.5">{t('profileSettings.newPassword')}</label>
                  <div className="relative">
                    <input id="new-pw" type={showNewPassword ? 'text' : 'password'} value={newPassword} onChange={e => setNewPassword(e.target.value)} required minLength={6} autoComplete="new-password" placeholder={t('profileSettings.min6Characters')} className="w-full bg-white border border-slate-200 rounded-xl pl-4 pr-11 py-2.5 text-slate-900 text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all outline-none" />
                    <button
                      type="button"
                      onClick={() => setShowNewPassword(visible => !visible)}
                      aria-label={showNewPassword ? t('profileSettings.hideNewPassword') : t('profileSettings.showNewPassword')}
                      className="absolute right-3.5 top-1/2 -translate-y-1/2 bg-transparent border-none cursor-pointer text-slate-400 p-0 hover:text-slate-600"
                    >
                      {showNewPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                    </button>
                  </div>
                </div>

                <div>
                  <label htmlFor="confirm-pw" className="block text-sm font-medium text-slate-700 mb-1.5">{t('profileSettings.confirmNewPassword')}</label>
                  <div className="relative">
                    <input id="confirm-pw" type={showConfirmPassword ? 'text' : 'password'} value={confirmPassword} onChange={e => setConfirmPassword(e.target.value)} required minLength={6} autoComplete="new-password" placeholder={t('profileSettings.reEnterNewPassword')} className="w-full bg-white border border-slate-200 rounded-xl pl-4 pr-11 py-2.5 text-slate-900 text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all outline-none" />
                    <button
                      type="button"
                      onClick={() => setShowConfirmPassword(visible => !visible)}
                      aria-label={showConfirmPassword ? t('profileSettings.hideConfirmPassword') : t('profileSettings.showConfirmPassword')}
                      className="absolute right-3.5 top-1/2 -translate-y-1/2 bg-transparent border-none cursor-pointer text-slate-400 p-0 hover:text-slate-600"
                    >
                      {showConfirmPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                    </button>
                  </div>
                </div>

                <div className="pt-2">
                  <Button type="submit" variant="primary" isLoading={isSavingPassword}>{t('profileSettings.updatePassword')}</Button>
                </div>
              </form>
            </div>
          )}
        </div>
      </motion.div>
    </div>
  );
};

export default ProfileSettings;
