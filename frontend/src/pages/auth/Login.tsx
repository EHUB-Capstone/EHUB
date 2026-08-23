import { useState, useEffect } from 'react';
import type { FormEvent } from 'react';
import { useNavigate, Link, useLocation } from 'react-router-dom';
import { motion } from 'framer-motion';
import toast from 'react-hot-toast';
import {
  Mail, Lock, ArrowRight, AlertCircle, Eye, EyeOff,
  Sun, Moon, GraduationCap, Users, Shield, TrendingUp,
} from 'lucide-react';
import { GoogleLogin } from '@react-oauth/google';
import type { CredentialResponse } from '@react-oauth/google';
import { useTheme } from '../../context/ThemeContext';
import { useAuth } from '../../hooks/useAuth';
import { AUTH_ERROR_CODES } from '../../types/auth';
import { parseApiError } from '../../utils/apiError';
import {
  AUTH_FIELD_LIMITS,
  LOGIN_FIELDS,
  mapApiFieldErrors,
  normalizeLoginPayload,
  toFieldErrorMap,
  validateLoginPayload,
} from '../../utils/authValidation';
import type { AuthFieldErrors, LoginField } from '../../utils/authValidation';
import logo from '../../assets/logo.png';

/* ─── Google icon ─────────────────────────────────── */
const GoogleIcon: React.FC = () => (
  <svg width="18" height="18" viewBox="0 0 24 24">
    <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"/>
    <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"/>
    <path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"/>
    <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"/>
  </svg>
);

/* ─── Left panel features ─────────────────────────── */
const sideFeatures = [
  { icon: GraduationCap, text: 'Manage startup projects across semesters' },
  { icon: Users,         text: 'Connect with expert mentors' },
  { icon: Shield,        text: 'Secure evaluation & data storage' },
  { icon: TrendingUp,    text: 'Track long-term startup growth' },
];

/* ─── Types ───────────────────────────────────────── */
interface LocationState {
  prefillEmail?: string;
  email?: string;
  isPending?: boolean;
}

/* ─── Component ───────────────────────────────────── */
function normalizeRole(role: string): string {
  return role.trim().toUpperCase();
}

const Login: React.FC = () => {
  const { isDark, toggleTheme } = useTheme();
  const { loginWithEmail, loginWithGoogle } = useAuth();

  const [email, setEmail]                     = useState<string>('');
  const [password, setPassword]               = useState<string>('');
  const [showPass, setShowPass]               = useState<boolean>(false);
  const [loading, setLoading]                 = useState<boolean>(false);
  const [googleLoading, setGoogleLoading]     = useState<boolean>(false);
  const [pendingApproval, setPendingApproval] = useState<boolean>(false);
  const [rejectedStatus, setRejectedStatus]   = useState<boolean>(false);
  const [blockedStatus, setBlockedStatus]     = useState<boolean>(false);
  const [fieldErrors, setFieldErrors]         = useState<AuthFieldErrors<LoginField>>({});
  const [formError, setFormError]             = useState<string>('');

  const navigate  = useNavigate();
  const location  = useLocation();
  const state     = location.state as LocationState | null;
  const googleButtonWidth = typeof window === 'undefined'
    ? 400
    : Math.min(400, Math.max(240, window.innerWidth - 48));

  useEffect(() => {
    if (state?.prefillEmail || state?.email) setEmail(state.prefillEmail ?? state.email ?? '');
    if (state?.isPending) setPendingApproval(true);
  }, [state]);

  const redirectByRole = (roles: string[]) => {
    const normalizedRoles = roles.map(normalizeRole);
    if (normalizedRoles.includes('ADMIN'))         navigate('/admin');
    else if (normalizedRoles.includes('LECTURER')) navigate('/lecturer');
    else if (normalizedRoles.includes('MENTOR'))   navigate('/mentor');
    else                                           navigate('/student');
  };

  const clearFieldError = (field: LoginField) => {
    setFieldErrors(current => {
      if (!current[field]) return current;
      const next = { ...current };
      delete next[field];
      return next;
    });
    setFormError('');
    setPendingApproval(false);
    setRejectedStatus(false);
    setBlockedStatus(false);
  };

  const validateField = (field: LoginField) => {
    const error = validateLoginPayload({ email, password }).find(item => item.field === field);
    setFieldErrors(current => {
      const next = { ...current };
      if (error) next[field] = error.message;
      else delete next[field];
      return next;
    });
  };

  const focusFirstError = (errors: AuthFieldErrors<LoginField>) => {
    const firstField = LOGIN_FIELDS.find(field => errors[field]);
    if (firstField) requestAnimationFrame(() => document.getElementById(`login-${firstField}`)?.focus());
  };

  /* ── Email / password login ─────────────────────── */
  const handleLogin = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setPendingApproval(false);
    setRejectedStatus(false);
    setBlockedStatus(false);
    const payload = { email, password };
    const nextFieldErrors = toFieldErrorMap(validateLoginPayload(payload));
    setFieldErrors(nextFieldErrors);
    setFormError('');
    if (Object.keys(nextFieldErrors).length > 0) {
      focusFirstError(nextFieldErrors);
      return;
    }

    const normalizedPayload = normalizeLoginPayload(payload);
    if (normalizedPayload.email !== email) setEmail(normalizedPayload.email);
    setLoading(true);
    try {
      const user = await loginWithEmail(normalizedPayload);
      toast.success('Login successful!');
      redirectByRole(user.roles as string[]);
    } catch (err: unknown) {
      const { code, message, fieldErrors: apiFieldErrors } = parseApiError(err, 'Login failed.');
      const mappedFieldErrors = mapApiFieldErrors(apiFieldErrors, LOGIN_FIELDS);
      setFieldErrors(mappedFieldErrors);
      focusFirstError(mappedFieldErrors);
      if (code === AUTH_ERROR_CODES.ACCOUNT_PENDING_APPROVAL) setPendingApproval(true);
      else if (code === AUTH_ERROR_CODES.ACCOUNT_REJECTED)    setRejectedStatus(true);
      else if (code === AUTH_ERROR_CODES.USER_BLOCKED)        setBlockedStatus(true);
      else if (code === AUTH_ERROR_CODES.USER_INACTIVE)       setFormError('Your account is inactive. Contact support.');
      else if (Object.keys(mappedFieldErrors).length === 0)    setFormError(message);
    } finally { setLoading(false); }
  };

  /* ── Google login ───────────────────────────────── */
  const handleGoogleSuccess = async (credentialResponse: CredentialResponse) => {
    if (!credentialResponse.credential) {
      toast.error('Google did not return an ID token. Please try again.');
      return;
    }

    setGoogleLoading(true);
    setPendingApproval(false);
    setRejectedStatus(false);
    setBlockedStatus(false);
    setFieldErrors({});
    setFormError('');

    try {
      const user = await loginWithGoogle(credentialResponse.credential);
      toast.success('Signed in with Google!');
      redirectByRole(user.roles as string[]);
    } catch (err: unknown) {
      const { code, message } = parseApiError(err, 'Google sign-in failed.');
      if (code === AUTH_ERROR_CODES.ACCOUNT_NOT_REGISTERED) {
        toast.error('No account found for this Google email. Please register first.');
      } else if (code === AUTH_ERROR_CODES.ACCOUNT_PENDING_APPROVAL) {
        setPendingApproval(true);
      } else if (code === AUTH_ERROR_CODES.ACCOUNT_REJECTED) {
        setRejectedStatus(true);
      } else if (code === AUTH_ERROR_CODES.USER_BLOCKED) {
        setBlockedStatus(true);
      } else if (code === AUTH_ERROR_CODES.USER_INACTIVE) {
        toast.error('Your account is inactive. Contact support.');
      } else {
        toast.error(message || 'Google sign-in failed.');
      }
    } finally {
      setGoogleLoading(false);
    }
  };

  const handleGoogleLogin = () => {
    const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID ?? '';
    if (!clientId) {
      toast('Google sign-in is not configured yet.', { icon: '🔜' });
      return;
    }
  };

  return (
    <div className="min-h-screen flex font-sans transition-colors duration-300 bg-white dark:bg-[#0F172A]">

      {/* ── LEFT PANEL (branding) ── */}
      <div className="hidden lg:flex w-[44%] flex-col justify-between p-12 relative overflow-hidden bg-[#0F172A]">
        {/* BG orbs */}
        <div className="absolute -top-[10%] -right-[10%] w-[420px] h-[420px] rounded-full bg-[#EA6A12]/12 blur-[110px]" />
        <div className="absolute bottom-[10%] -left-[5%] w-[320px] h-[320px] rounded-full bg-white/5 blur-[90px]" />

        <div className="relative z-10">
          <Link to="/" className="inline-flex items-center gap-2.5 no-underline">
            <img src={logo} alt="EHub" className="w-[42px] h-[42px] object-contain" />
            <span className="text-[22px] font-extrabold tracking-tight">
              <span className="text-[#F3A07A]">E</span>
              <span className="text-[#79A8D9]">HUB</span>
            </span>
          </Link>
        </div>

        <div className="relative z-10">
          <motion.div initial={{ opacity: 0, y: 30 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.7 }}>
            <h2 className="text-4xl font-extrabold text-white leading-[1.2] mb-4 tracking-tight">
              Build tomorrow's startups,{' '}
              <span className="text-[#EA6A12]">today.</span>
            </h2>
            <p className="text-white/60 text-[15px] leading-[1.7] mb-9">
              EHub is the all-in-one platform for managing, evaluating, and growing student startup projects from idea to reality.
            </p>
            <div className="flex flex-col gap-4">
              {sideFeatures.map((f, i) => (
                <motion.div key={i} initial={{ opacity: 0, x: -20 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: 0.2 + i * 0.1, duration: 0.5 }}
                  className="flex items-center gap-3.5">
                  <div className="w-[38px] h-[38px] rounded-[10px] bg-[#EA6A12]/14 border border-[#EA6A12]/25 flex items-center justify-center shrink-0">
                    <f.icon size={18} color="#EA6A12" />
                  </div>
                  <span className="text-white/70 text-[14px] font-medium">{f.text}</span>
                </motion.div>
              ))}
            </div>
          </motion.div>
        </div>

        <p className="text-white/30 text-[12px] relative z-10" />
      </div>

      {/* ── RIGHT PANEL (form) ── */}
      <div className="flex-1 flex flex-col items-center justify-center p-[40px_24px] relative">

        {/* Theme toggle */}
        <button onClick={toggleTheme}
          className="absolute top-6 right-6 w-10 h-10 rounded-[14px] border border-[#E5E7EB] dark:border-white/10 bg-white dark:bg-white/5 cursor-pointer flex items-center justify-center text-[#64748B] dark:text-slate-400 transition-all hover:bg-[#F8FAFC] dark:hover:bg-white/10"
          aria-label="Toggle theme"
        >
          {isDark ? <Sun size={18} /> : <Moon size={18} />}
        </button>

        <motion.div initial={{ opacity: 0, y: 24 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.6 }}
          className="w-full max-w-[400px]">

          {/* Mobile logo */}
          <div className="lg:hidden text-center mb-8">
            <Link to="/" className="inline-flex items-center gap-2.5 no-underline">
              <img src={logo} alt="EHub" className="w-[38px] h-[38px] object-contain" />
              <span className="text-[20px] font-extrabold tracking-tight">
                <span className="text-[#F08A5D]">E</span>
                <span className="text-[#1E5E9F] dark:text-[#79A8D9]">HUB</span>
              </span>
            </Link>
          </div>

          <h1 className="text-[28px] font-extrabold text-[#0F172A] dark:text-slate-50 mb-1.5 tracking-tight">Welcome back</h1>
          <p className="text-[#64748B] dark:text-slate-400 text-[14px] mb-7">Sign in to your EHub account</p>

          {/* Alert banners */}
          {pendingApproval && (
            <div role="alert" aria-live="assertive" className="bg-blue-500/10 border border-blue-500/30 rounded-xl p-3.5 flex gap-3 mb-5">
              <AlertCircle size={18} className="text-blue-400 shrink-0 mt-0.5" />
              <div>
                <p className="text-[13px] font-semibold text-blue-400 mb-1">Pending Approval</p>
                <p className="text-[12px] text-slate-500 dark:text-slate-400">An admin must approve your Mentor/Lecturer account before you can sign in.</p>
              </div>
            </div>
          )}
          {rejectedStatus && (
            <div role="alert" aria-live="assertive" className="bg-red-500/10 border border-red-500/30 rounded-xl p-3.5 flex gap-3 mb-5">
              <AlertCircle size={18} className="text-red-400 shrink-0 mt-0.5" />
              <div>
                <p className="text-[13px] font-semibold text-red-400 mb-1">Account Rejected</p>
                <p className="text-[12px] text-slate-500 dark:text-slate-400">Your registration was declined. Contact support for assistance.</p>
              </div>
            </div>
          )}

          {/* Google btn */}
          {import.meta.env.VITE_GOOGLE_CLIENT_ID ? (
            <div className={`relative mb-5 flex justify-center ${googleLoading ? 'pointer-events-none opacity-70' : ''}`}>
              <GoogleLogin
                onSuccess={handleGoogleSuccess}
                onError={() => { toast.error('Google sign-in failed.'); setGoogleLoading(false); }}
                theme={isDark ? 'filled_black' : 'outline'}
                size="large"
                text="continue_with"
                shape="rectangular"
                logo_alignment="center"
                width={googleButtonWidth}
              />
              {googleLoading && (
                <div className="absolute inset-0 flex items-center justify-center rounded-[14px] bg-white/70 dark:bg-[#0F172A]/70">
                  <div className="w-[18px] h-[18px] rounded-full border-2 border-[#EA6A12] border-t-transparent animate-spin" />
                </div>
              )}
            </div>
          ) : (
            <button onClick={handleGoogleLogin} disabled={googleLoading}
              className="w-full flex items-center justify-center gap-2.5 py-3 rounded-[14px] border border-[#E5E7EB] dark:border-white/10 bg-white dark:bg-white/5 text-[#0F172A] dark:text-slate-100 text-[14px] font-semibold cursor-pointer mb-5 transition-all hover:bg-[#F8FAFC] dark:hover:bg-white/10"
            >
              {googleLoading
                ? <div className="w-[18px] h-[18px] rounded-full border-2 border-[#EA6A12] border-t-transparent animate-spin" />
                : <GoogleIcon />
              }
              {googleLoading ? 'Connecting...' : 'Continue with Google'}
            </button>
          )}

          {/* Divider */}
          <div className="flex items-center gap-3 mb-5">
            <div className="flex-1 h-px bg-[#E5E7EB] dark:bg-white/10" />
            <span className="text-[12px] text-[#64748B] dark:text-slate-400 font-medium">or sign in with email</span>
            <div className="flex-1 h-px bg-[#E5E7EB] dark:bg-white/10" />
          </div>

          {formError && (
            <div id="login-form-error" role="alert"
              className="bg-red-500/10 border border-red-500/30 rounded-xl p-3.5 flex gap-3 mb-5">
              <AlertCircle size={18} className="text-red-500 shrink-0 mt-0.5" />
              <p className="text-[13px] text-red-500 dark:text-red-400">{formError}</p>
            </div>
          )}
          {blockedStatus && (
            <div role="alert" aria-live="assertive" className="bg-red-500/10 border border-red-500/30 rounded-xl p-3.5 flex gap-3 mb-5">
              <AlertCircle size={18} className="text-red-400 shrink-0 mt-0.5" />
              <div>
                <p className="text-[13px] font-semibold text-red-400 mb-1">Account Blocked</p>
                <p className="text-[12px] text-slate-500 dark:text-slate-400">Your account has been blocked. Contact support for assistance.</p>
              </div>
            </div>
          )}

          {/* Form */}
          <form onSubmit={handleLogin} className="flex flex-col gap-4" noValidate aria-describedby={formError ? 'login-form-error' : undefined}>
            {/* Email */}
            <div>
              <label htmlFor="login-email" className="block text-[13px] font-semibold text-slate-900 dark:text-slate-50 mb-1.5">Email</label>
              <div className="relative">
                <Mail size={16} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400 pointer-events-none" />
                <input id="login-email" type="email" value={email}
                  onChange={e => { setEmail(e.target.value); clearFieldError('email'); }}
                  onBlur={() => validateField('email')}
                  placeholder="you@example.com" autoComplete="email" required maxLength={AUTH_FIELD_LIMITS.emailMax}
                  aria-invalid={Boolean(fieldErrors.email)}
                  aria-describedby={fieldErrors.email ? 'login-email-error' : undefined}
                  className={`w-full py-2.5 pr-3.5 pl-10 rounded-[14px] border bg-[#F8FAFC] dark:bg-white/5 text-[#0F172A] dark:text-slate-100 text-[14px] outline-none transition-colors ${fieldErrors.email ? 'border-red-500 focus:border-red-500' : 'border-[#E5E7EB] dark:border-white/10 focus:border-[#EA6A12] dark:focus:border-[#EA6A12]'}`}
                />
              </div>
              {fieldErrors.email && <p id="login-email-error" role="alert" className="mt-1.5 text-[12px] text-red-500 dark:text-red-400">{fieldErrors.email}</p>}
            </div>

            {/* Password */}
            <div>
              <label htmlFor="login-password" className="block text-[13px] font-semibold text-slate-900 dark:text-slate-50 mb-1.5">Password</label>
              <div className="relative">
                <Lock size={16} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400 pointer-events-none" />
                <input id="login-password" type={showPass ? 'text' : 'password'} value={password}
                  onChange={e => { setPassword(e.target.value); clearFieldError('password'); }}
                  onBlur={() => validateField('password')}
                  placeholder="••••••" autoComplete="current-password" required
                  minLength={AUTH_FIELD_LIMITS.passwordMin} maxLength={AUTH_FIELD_LIMITS.passwordMax}
                  aria-invalid={Boolean(fieldErrors.password)}
                  aria-describedby={fieldErrors.password ? 'login-password-error' : undefined}
                  className={`w-full py-2.5 pr-11 pl-10 rounded-[14px] border bg-[#F8FAFC] dark:bg-white/5 text-[#0F172A] dark:text-slate-100 text-[14px] outline-none transition-colors ${fieldErrors.password ? 'border-red-500 focus:border-red-500' : 'border-[#E5E7EB] dark:border-white/10 focus:border-[#EA6A12] dark:focus:border-[#EA6A12]'}`}
                />
                <button type="button" onClick={() => setShowPass(p => !p)}
                  aria-label={showPass ? 'Hide password' : 'Show password'}
                  className="absolute right-3.5 top-1/2 -translate-y-1/2 bg-transparent border-none cursor-pointer text-slate-400 p-0">
                  {showPass ? <EyeOff size={16} /> : <Eye size={16} />}
                </button>
              </div>
              {fieldErrors.password && <p id="login-password-error" role="alert" className="mt-1.5 text-[12px] text-red-500 dark:text-red-400">{fieldErrors.password}</p>}
              <div className="mt-2 text-right">
                <Link to="/forgot-password" className="text-[12px] text-[#EA6A12] no-underline font-semibold hover:underline">
                  Forgot password?
                </Link>
              </div>
            </div>

            {/* Submit */}
            <button type="submit" disabled={loading}
              className={`w-full h-14 rounded-[14px] border-none font-semibold text-[15px] text-white flex items-center justify-center gap-2 mt-1 transition-all duration-200 ease-out bg-[linear-gradient(135deg,#EA6A12,#D97706)] shadow-[0_10px_28px_rgba(234,106,18,0.18)] ${loading ? 'opacity-70 cursor-not-allowed' : 'cursor-pointer hover:-translate-y-0.5 hover:shadow-[0_14px_36px_rgba(234,106,18,0.22)]'}`}
            >
              {loading
                ? <div className="w-[18px] h-[18px] rounded-full border-2 border-white/50 border-t-white animate-spin" />
                : <><span>Sign In</span><ArrowRight size={17} /></>
              }
            </button>
          </form>

          <p className="text-center mt-6 text-[14px] text-slate-500 dark:text-slate-400">
            Don't have an account?{' '}
            <Link to="/register" className="text-[#EA6A12] font-bold no-underline">Create one</Link>
          </p>
        </motion.div>
      </div>
    </div>
  );
};

export default Login;
