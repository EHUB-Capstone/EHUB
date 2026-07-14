import { useState, useEffect, useRef, FormEvent, ChangeEvent, KeyboardEvent, ClipboardEvent } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import {
  User, Mail, Lock, ArrowRight, Eye, EyeOff,
  ShieldCheck, RefreshCw, Clock, AlertTriangle,
  Sun, Moon, CheckCircle,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { useTheme } from '../../context/ThemeContext';
import { TEAM_MAJOR_GROUPS } from '../../constants/majors';
import logo from '../../assets/logo.png';

const OTP_EXPIRE_SECONDS = 5 * 60;
const RESEND_COOLDOWN    = 60;

type Role = 'STUDENT' | 'LECTURER' | 'MENTOR';
interface LocationState { step?: string; email?: string; }

const Register: React.FC = () => {
  const { isDark, toggleTheme } = useTheme();

  /* Step 1 */
  const [name,            setName]            = useState<string>('');
  const [email,           setEmail]           = useState<string>('');
  const [password,        setPassword]        = useState<string>('');
  const [confirmPassword, setConfirmPassword] = useState<string>('');
  const [role,            setRole]            = useState<Role>('STUDENT');
  const [major,           setMajor]           = useState<string>('');
  const [loading,         setLoading]         = useState<boolean>(false);
  const [showPass,        setShowPass]        = useState<boolean>(false);
  const [showConfirm,     setShowConfirm]     = useState<boolean>(false);
  const [emailTakenError, setEmailTakenError] = useState<boolean>(false);

  /* Step 2 OTP */
  const [step,           setStep]           = useState<1 | 2>(1);
  const [otpValues,      setOtpValues]      = useState<string[]>(['','','','','','']);
  const [otpLoading,     setOtpLoading]     = useState<boolean>(false);
  const [countdown,      setCountdown]      = useState<number>(OTP_EXPIRE_SECONDS);
  const [resendCooldown, setResendCooldown] = useState<number>(0);
  const [resendLoading,  setResendLoading]  = useState<boolean>(false);
  const otpRefs = useRef<(HTMLInputElement | null)[]>([]);

  const navigate = useNavigate();
  const location = useLocation();
  const locState = location.state as LocationState | null;

  useEffect(() => {
    if (locState?.step === 'otp' && locState?.email) {
      setEmail(locState.email); setStep(2); setCountdown(OTP_EXPIRE_SECONDS);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (step !== 2 || countdown <= 0) return;
    const t = setInterval(() => setCountdown(c => c - 1), 1000);
    return () => clearInterval(t);
  }, [step, countdown]);

  useEffect(() => {
    if (resendCooldown <= 0) return;
    const t = setInterval(() => setResendCooldown(c => c - 1), 1000);
    return () => clearInterval(t);
  }, [resendCooldown]);

  const fmt = (s: number) => `${Math.floor(s/60).toString().padStart(2,'0')}:${(s%60).toString().padStart(2,'0')}`;

  /* ─── Handlers ─────────────────────────────────── */
  const handleRegister = async (e: FormEvent) => {
    e.preventDefault();
    if (!name.trim())                                  return void toast.error('Enter your full name.');
    if (!email.trim() || !/\S+@\S+\.\S+/.test(email)) return void toast.error('Enter a valid email.');
    if (password.length < 6)                          return void toast.error('Password must be at least 6 characters.');
    if (password !== confirmPassword)                 return void toast.error('Passwords do not match.');
    setLoading(true); setEmailTakenError(false);
    try {
      // TODO: await register({ name, email, password, role, major });
      toast.success('Account created! Check your email for OTP.');
      setStep(2); setCountdown(OTP_EXPIRE_SECONDS); setResendCooldown(RESEND_COOLDOWN);
      setTimeout(() => otpRefs.current[0]?.focus(), 300);
    } catch (err: unknown) {
      const error = err as { message?: string; response?: { status?: number; data?: { message?: string } }; data?: { emailTaken?: boolean; needVerify?: boolean } };
      if (error.data?.emailTaken)  { setEmailTakenError(true); return; }
      if (error.data?.needVerify)  { toast(error.message ?? '', { icon: '📧' }); setStep(2); setCountdown(OTP_EXPIRE_SECONDS); setResendCooldown(RESEND_COOLDOWN); setTimeout(() => otpRefs.current[0]?.focus(), 300); }
      else if (error.response?.status === 409) { toast.error(error.message ?? ''); navigate('/login', { state: { email } }); }
      else toast.error(error.message ?? 'Registration failed.');
    } finally { setLoading(false); }
  };

  const handleOtpChange = (idx: number, val: string) => {
    if (!/^\d?$/.test(val)) return;
    const next = [...otpValues]; next[idx] = val; setOtpValues(next);
    if (val && idx < 5) otpRefs.current[idx + 1]?.focus();
  };
  const handleOtpKeyDown = (idx: number, e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Backspace' && !otpValues[idx] && idx > 0) otpRefs.current[idx - 1]?.focus();
  };
  const handleOtpPaste = (e: ClipboardEvent<HTMLDivElement>) => {
    e.preventDefault();
    const pasted = e.clipboardData.getData('text').replace(/\D/g,'').slice(0,6);
    if (!pasted) return;
    const next = [...otpValues];
    pasted.split('').forEach((ch, i) => { next[i] = ch; });
    setOtpValues(next);
    otpRefs.current[Math.min(pasted.length, 5)]?.focus();
  };

  const handleVerifyOtp = async (e: FormEvent) => {
    e.preventDefault();
    const otp = otpValues.join('');
    if (otp.length !== 6) return void toast.error('Enter all 6 digits.');
    if (countdown <= 0)   return void toast.error('OTP expired. Request a new one.');
    setOtpLoading(true);
    try {
      // TODO: const { user, isPending } = await verifyOtp(email, otp);
      toast.success('Verified! Welcome 🎉');
      navigate('/student');
    } catch (err: unknown) {
      const error = err as { message?: string };
      toast.error(error.message ?? 'Invalid OTP.');
      setOtpValues(['','','','','','']); otpRefs.current[0]?.focus();
    } finally { setOtpLoading(false); }
  };

  const handleResend = async () => {
    if (resendCooldown > 0) return;
    setResendLoading(true);
    try {
      // TODO: await resendOtp(email);
      toast.success('New OTP sent!');
      setOtpValues(['','','','','','']); setCountdown(OTP_EXPIRE_SECONDS); setResendCooldown(RESEND_COOLDOWN);
      otpRefs.current[0]?.focus();
    } catch (err: unknown) {
      const error = err as { response?: { data?: { message?: string } } };
      toast.error(error.response?.data?.message ?? 'Failed to resend OTP.');
    } finally { setResendLoading(false); }
  };

  /* ─── Render ───────────────────────────────────── */
  return (
    <div className="min-h-screen flex font-sans transition-colors duration-300 bg-slate-50 dark:bg-[#0a0f1e]">

      {/* ── LEFT PANEL ── */}
      <div className="hidden lg:flex w-[40%] flex-col justify-center p-12 relative overflow-hidden bg-gradient-to-br from-[#034EA2] to-[#023a78] dark:from-[#0d1a3a] dark:to-[#0a0f1e]">
        <div className="absolute -top-[10%] -right-[10%] w-[350px] h-[350px] rounded-full bg-[#F37021]/10 blur-[80px]" />
        
        <div className="relative z-10">
          <Link to="/" className="inline-flex items-center gap-2.5 no-underline mb-12">
            <img src={logo} alt="EHub" className="w-10 h-10 object-contain" />
            <span className="text-[20px] font-extrabold tracking-tight">
              <span className="text-[#93c5fd] dark:text-[#5b9bd5]">E</span>
              <span className="text-[#F37021]">HUB</span>
            </span>
          </Link>
          
          <h2 className="text-[32px] font-extrabold text-white leading-[1.2] mb-4 tracking-tight">
            Join the future of <span className="text-[#F37021]">student startups</span>
          </h2>
          <p className="text-white/50 text-[15px] leading-[1.7] mb-9">
            Create your account and start managing your startup journey with mentors, evaluations, and AI-powered insights.
          </p>
          
          <div className="flex flex-col gap-3.5">
            {['Free to join, forever', 'AI-powered project evaluation', 'Connect with expert mentors', 'Track your startup progress'].map((t, i) => (
              <div key={i} className="flex items-center gap-3">
                <CheckCircle size={18} color="#F37021" />
                <span className="text-white/65 text-[14px]">{t}</span>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* ── RIGHT PANEL ── */}
      <div className="flex-1 flex flex-col items-center justify-center p-[32px_24px] overflow-y-auto relative">

        {/* Theme toggle */}
        <button onClick={toggleTheme}
          className="absolute top-6 right-6 w-10 h-10 rounded-[10px] border border-slate-200 dark:border-white/10 bg-white dark:bg-white/5 cursor-pointer flex items-center justify-center text-slate-500 dark:text-slate-400 transition-all hover:bg-slate-100 dark:hover:bg-white/10"
          aria-label="Toggle theme"
        >
          {isDark ? <Sun size={18} /> : <Moon size={18} />}
        </button>

        <AnimatePresence mode="wait">

          {/* ── STEP 1: Registration form ── */}
          {step === 1 && (
            <motion.div key="step1"
              initial={{ opacity: 0, x: -20 }} animate={{ opacity: 1, x: 0 }} exit={{ opacity: 0, x: -20 }}
              transition={{ duration: 0.3 }}
              className="w-full max-w-[420px]">

              {/* Mobile logo */}
              <div className="lg:hidden text-center mb-7">
                <Link to="/" className="inline-flex items-center gap-2.5 no-underline">
                  <img src={logo} alt="EHub" className="w-[36px] h-[36px] object-contain" />
                  <span className="text-[18px] font-extrabold tracking-tight">
                    <span className="text-[#034EA2]">E</span><span className="text-[#F37021]">HUB</span>
                  </span>
                </Link>
              </div>

              <h1 className="text-[26px] font-extrabold text-slate-900 dark:text-slate-50 mb-1.5 tracking-tight">Create your account</h1>
              <p className="text-slate-500 dark:text-slate-400 text-[14px] mb-6">Join EHub and start your startup journey</p>

              {/* Email taken banner */}
              <AnimatePresence>
                {emailTakenError && (
                  <motion.div initial={{ opacity: 0, y: -8 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: -8 }}
                    className="bg-red-500/10 border border-red-500/30 rounded-xl p-3.5 flex gap-3 mb-4.5">
                    <AlertTriangle size={18} className="text-red-400 shrink-0 mt-0.5" />
                    <div>
                      <p className="text-[13px] font-semibold text-red-400 mb-1">Email already registered</p>
                      <p className="text-[12px] text-slate-500 dark:text-slate-400 mb-2"><strong>{email}</strong> is already in use.</p>
                      <div className="flex gap-3">
                        <Link to="/login" state={{ prefillEmail: email }} className="text-[12px] font-semibold text-red-400 underline">Sign in →</Link>
                        <Link to="/forgot-password" className="text-[12px] font-semibold text-slate-500 dark:text-slate-400 underline">Forgot password?</Link>
                      </div>
                    </div>
                  </motion.div>
                )}
              </AnimatePresence>

              <form onSubmit={handleRegister} className="flex flex-col gap-3.5">
                {/* Name */}
                <div>
                  <label className="block text-[13px] font-semibold text-slate-900 dark:text-slate-50 mb-1.5">Full Name</label>
                  <div className="relative">
                    <User size={16} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400 pointer-events-none" />
                    <input id="reg-name" type="text" value={name} onChange={(e: ChangeEvent<HTMLInputElement>) => setName(e.target.value)}
                      placeholder="Nguyen Van A" required
                      className="w-full py-2.5 pr-3.5 pl-10 rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/5 text-slate-900 dark:text-slate-100 text-[14px] outline-none transition-colors focus:border-[#F37021] dark:focus:border-[#F37021]"
                    />
                  </div>
                </div>

                {/* Email */}
                <div>
                  <label className="block text-[13px] font-semibold text-slate-900 dark:text-slate-50 mb-1.5">Email</label>
                  <div className="relative">
                    <Mail size={16} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400 pointer-events-none" />
                    <input id="reg-email" type="email" value={email} onChange={(e: ChangeEvent<HTMLInputElement>) => { setEmail(e.target.value); setEmailTakenError(false); }}
                      placeholder="you@example.com" required
                      className="w-full py-2.5 pr-3.5 pl-10 rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/5 text-slate-900 dark:text-slate-100 text-[14px] outline-none transition-colors focus:border-[#F37021] dark:focus:border-[#F37021]"
                    />
                  </div>
                </div>

                {/* Password */}
                <div>
                  <label className="block text-[13px] font-semibold text-slate-900 dark:text-slate-50 mb-1.5">Password</label>
                  <div className="relative">
                    <Lock size={16} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400 pointer-events-none" />
                    <input id="reg-password" type={showPass ? 'text' : 'password'} value={password} onChange={(e: ChangeEvent<HTMLInputElement>) => setPassword(e.target.value)}
                      placeholder="Min. 6 characters" required
                      className="w-full py-2.5 pr-11 pl-10 rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/5 text-slate-900 dark:text-slate-100 text-[14px] outline-none transition-colors focus:border-[#F37021] dark:focus:border-[#F37021]"
                    />
                    <button type="button" onClick={() => setShowPass(p => !p)}
                      className="absolute right-3.5 top-1/2 -translate-y-1/2 bg-transparent border-none cursor-pointer text-slate-400 p-0">
                      {showPass ? <EyeOff size={15} /> : <Eye size={15} />}
                    </button>
                  </div>
                </div>

                {/* Confirm Password */}
                <div>
                  <label className="block text-[13px] font-semibold text-slate-900 dark:text-slate-50 mb-1.5">Confirm Password</label>
                  <div className="relative">
                    <Lock size={16} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400 pointer-events-none" />
                    <input id="reg-confirm" type={showConfirm ? 'text' : 'password'} value={confirmPassword} onChange={(e: ChangeEvent<HTMLInputElement>) => setConfirmPassword(e.target.value)}
                      placeholder="Re-enter password" required
                      className="w-full py-2.5 pr-11 pl-10 rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/5 text-slate-900 dark:text-slate-100 text-[14px] outline-none transition-colors focus:border-[#F37021] dark:focus:border-[#F37021]"
                    />
                    <button type="button" onClick={() => setShowConfirm(p => !p)}
                      className="absolute right-3.5 top-1/2 -translate-y-1/2 bg-transparent border-none cursor-pointer text-slate-400 p-0">
                      {showConfirm ? <EyeOff size={15} /> : <Eye size={15} />}
                    </button>
                  </div>
                </div>

                {/* Role */}
                <div>
                  <label className="block text-[13px] font-semibold text-slate-900 dark:text-slate-50 mb-2">Role</label>
                  <div className="grid grid-cols-3 gap-2 p-1 rounded-xl border border-slate-200 dark:border-white/10 bg-slate-100 dark:bg-white/5">
                    {(['STUDENT','LECTURER','MENTOR'] as Role[]).map(r => (
                      <label key={r} className="cursor-pointer">
                        <input type="radio" name="role" value={r} checked={role === r} onChange={() => setRole(r)} className="hidden" />
                        <div className={`text-center py-[9px] px-1 rounded-[9px] text-[13px] font-semibold transition-all ${
                          role === r 
                            ? 'bg-white text-[#F37021] border border-[#F37021]/30 shadow-[0_0_12px_rgba(243,112,33,0.15)] dark:bg-[#F37021]/15'
                            : 'bg-transparent text-slate-500 dark:text-slate-400 border border-transparent shadow-none'
                        }`}>
                          {r.charAt(0) + r.slice(1).toLowerCase()}
                        </div>
                      </label>
                    ))}
                  </div>
                </div>

                {/* Major (STUDENT only) */}
                {role === 'STUDENT' && (
                  <div>
                    <label className="block text-[13px] font-semibold text-slate-900 dark:text-slate-50 mb-1.5">Major</label>
                    <select id="reg-major" value={major} onChange={(e: ChangeEvent<HTMLSelectElement>) => setMajor(e.target.value)} required
                      className="w-full py-2.5 px-3.5 rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/5 text-[14px] outline-none transition-colors focus:border-[#F37021] dark:focus:border-[#F37021] text-slate-900 dark:text-slate-100"
                    >
                      <option value="" className="text-slate-500">-- Select Major --</option>
                      {TEAM_MAJOR_GROUPS.map(g => (
                        <optgroup key={g.key} label={g.label} className="text-slate-900 dark:text-slate-100 bg-white dark:bg-slate-800">
                          {g.majors.map(m => <option key={m.code} value={m.code}>{m.code} — {m.name}</option>)}
                        </optgroup>
                      ))}
                    </select>
                  </div>
                )}

                {/* Submit */}
                <button type="submit" disabled={loading}
                  className={`w-full py-3 rounded-xl border-none font-bold text-[15px] text-white flex items-center justify-center gap-2 mt-1 transition-all bg-gradient-to-br from-[#F37021] to-[#e05a10] shadow-[0_0_24px_rgba(243,112,33,0.3)] ${loading ? 'opacity-70 cursor-not-allowed' : 'cursor-pointer hover:-translate-y-0.5 hover:shadow-[0_4px_32px_rgba(243,112,33,0.5)]'}`}
                >
                  {loading
                    ? <div className="w-[18px] h-[18px] rounded-full border-2 border-white/50 border-t-white animate-spin" />
                    : <><span>Create Account</span><ArrowRight size={17} /></>
                  }
                </button>
              </form>

              <p className="text-center mt-5 text-[14px] text-slate-500 dark:text-slate-400">
                Already have an account?{' '}
                <Link to="/login" className="text-[#F37021] font-bold no-underline">Sign in</Link>
              </p>
            </motion.div>
          )}

          {/* ── STEP 2: OTP ── */}
          {step === 2 && (
            <motion.div key="step2"
              initial={{ opacity: 0, x: 20 }} animate={{ opacity: 1, x: 0 }} exit={{ opacity: 0, x: 20 }}
              transition={{ duration: 0.3 }}
              className="w-full max-w-[400px] text-center">

              {/* Icon */}
              <div className="w-[72px] h-[72px] rounded-[20px] bg-gradient-to-br from-emerald-500 to-emerald-600 flex items-center justify-center mx-auto mb-6 shadow-[0_0_30px_rgba(16,185,129,0.35)]">
                <ShieldCheck size={34} color="#fff" />
              </div>

              <h1 className="text-[24px] font-extrabold text-slate-900 dark:text-slate-50 mb-2">Verify your email</h1>
              <p className="text-slate-500 dark:text-slate-400 text-[14px] mb-1">OTP code sent to</p>
              <p className="text-[#F37021] text-[14px] font-bold mb-6">{email}</p>

              {/* Countdown */}
              <div className={`inline-flex items-center gap-2 px-4.5 py-2 rounded-full mb-7 text-[13px] font-bold border ${countdown > 60 ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-500' : countdown > 0 ? 'bg-amber-500/10 border-amber-500/30 text-amber-500' : 'bg-red-500/10 border-red-500/30 text-red-500'}`}>
                <Clock size={14} />
                {countdown > 0 ? `Expires in ${fmt(countdown)}` : 'OTP expired — request a new one'}
              </div>

              {/* OTP inputs */}
              <form onSubmit={handleVerifyOtp}>
                <div className="flex gap-2.5 justify-center mb-6" onPaste={handleOtpPaste}>
                  {otpValues.map((val, idx) => (
                    <input key={idx}
                      ref={el => { otpRefs.current[idx] = el; }}
                      type="text" inputMode="numeric" maxLength={1} value={val}
                      onChange={(e: ChangeEvent<HTMLInputElement>) => handleOtpChange(idx, e.target.value)}
                      onKeyDown={(e: KeyboardEvent<HTMLInputElement>) => handleOtpKeyDown(idx, e)}
                      className={`w-[52px] h-[60px] text-center text-[22px] font-extrabold rounded-xl border-2 outline-none transition-all ${
                        val 
                          ? 'border-[#F37021] bg-orange-50 dark:bg-[#F37021]/10 text-[#F37021]'
                          : 'border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/5 text-slate-900 dark:text-slate-50'
                      }`}
                    />
                  ))}
                </div>

                <button type="submit" disabled={otpLoading || countdown <= 0}
                  className={`w-full py-3 rounded-xl border-none font-bold text-[15px] text-white flex items-center justify-center gap-2 transition-all bg-gradient-to-br from-[#F37021] to-[#e05a10] shadow-[0_0_24px_rgba(243,112,33,0.3)] ${
                    (otpLoading || countdown <= 0) 
                      ? 'opacity-60 cursor-not-allowed' 
                      : 'cursor-pointer hover:-translate-y-0.5 hover:shadow-[0_4px_32px_rgba(243,112,33,0.5)]'
                  }`}
                >
                  {otpLoading
                    ? <div className="w-[18px] h-[18px] rounded-full border-2 border-white/50 border-t-white animate-spin" />
                    : 'Verify OTP'
                  }
                </button>
              </form>

              {/* Resend */}
              <div className="mt-5">
                <p className="text-[13px] text-slate-500 dark:text-slate-400 mb-2">Didn't receive the email?</p>
                <button onClick={handleResend} disabled={resendCooldown > 0 || resendLoading}
                  className={`bg-transparent border-none inline-flex items-center gap-1.5 text-[13px] font-bold ${
                    (resendCooldown > 0 || resendLoading) 
                      ? 'cursor-not-allowed text-slate-500 dark:text-slate-400' 
                      : 'cursor-pointer text-[#F37021]'
                  }`}>
                  <RefreshCw size={14} className={resendLoading ? 'animate-spin' : ''} />
                  {resendCooldown > 0 ? `Resend in ${resendCooldown}s` : resendLoading ? 'Sending...' : 'Resend OTP'}
                </button>
              </div>

              <button onClick={() => { setStep(1); setOtpValues(['','','','','','']); }}
                className="mt-4 bg-transparent border-none cursor-pointer text-[13px] text-slate-500 dark:text-slate-400 w-full hover:text-slate-700 dark:hover:text-slate-300 transition-colors">
                ← Back to edit your details
              </button>
            </motion.div>
          )}

        </AnimatePresence>
      </div>
    </div>
  );
};

export default Register;
