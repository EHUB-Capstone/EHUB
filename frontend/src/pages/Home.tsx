import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import type { Easing, Variants } from 'framer-motion';
import {
  ArrowRight, GraduationCap, Users, Brain, BarChart3,
  Rocket, Menu, X, CheckCircle2, TrendingUp, Shield, Star,
  Sun, Moon,
} from 'lucide-react';
import { useState } from 'react';
import logo from '../assets/logo.png';
import { useTheme } from '../context/ThemeContext';

/* ─── Animation variants ─────────────────────────────── */
const smoothEase: Easing = [0.22, 1, 0.36, 1] as const;

const fadeUp: Variants = {
  hidden: { opacity: 0, y: 40 },
  visible: (i = 0) => ({
    opacity: 1, y: 0,
    transition: { delay: i * 0.12, duration: 0.65, ease: smoothEase },
  }),
};
const fadeIn: Variants = {
  hidden: { opacity: 0 },
  visible: (i = 0) => ({ opacity: 1, transition: { delay: i * 0.1, duration: 0.5 } }),
};

/* ─── Types ──────────────────────────────────────────── */
interface Feature { icon: React.ElementType; title: string; desc: string; accent: string; }
interface Stat    { value: string; label: string; icon: React.ElementType; }
interface Step    { num: string; title: string; desc: string; }

/* ─── Static data ────────────────────────────────────── */
const features: Feature[] = [
  { icon: GraduationCap, title: 'Project Management',    desc: 'Organize and track student startup projects across semesters with structured workflows and milestone management.', accent: '#F97316' },
  { icon: Users,         title: 'Mentor Connection',     desc: 'Bridge the gap between mentors and student teams for focused coaching, feedback, and real-world guidance.', accent: '#F97316' },
  { icon: Brain,         title: 'AI-Powered Evaluation', desc: 'Leverage AI to assess startup viability, feasibility, and market potential — fast, unbiased, and data-driven.', accent: '#F97316' },
  { icon: BarChart3,     title: 'Progress Analytics',    desc: 'Visualize team performance with dashboards, KPI tracking, and detailed reports in real time.', accent: '#F97316' },
  { icon: Shield,        title: 'Secure Data Storage',   desc: 'All project data, documents, and evaluations are securely stored and easily accessible anytime.', accent: '#F97316' },
  { icon: TrendingUp,    title: 'Long-term Development', desc: 'Support startup journeys beyond a single semester — track growth, iterations, and outcomes over time.', accent: '#F97316' },
];

const stats: Stat[] = [
  { value: '500+',  label: 'Startup Projects', icon: Rocket },
  { value: '1,200', label: 'Active Students',  icon: Users },
  { value: '98%',   label: 'Satisfaction Rate',icon: Star },
  { value: '50+',   label: 'Expert Mentors',   icon: CheckCircle2 },
];

const steps: Step[] = [
  { num: '01', title: 'Register & Join',   desc: 'Students and mentors create accounts and join their respective classes or incubation cohorts.' },
  { num: '02', title: 'Submit Projects',   desc: 'Teams submit startup proposals and documents through the structured project management system.' },
  { num: '03', title: 'Get Evaluated',     desc: 'AI and mentors review the projects, providing scores, feedback, and actionable insights.' },
  { num: '04', title: 'Grow & Succeed',    desc: 'Track progress over time, iterate on ideas, and build toward a real-world startup launch.' },
];

/* ─── Component ──────────────────────────────────────── */
const Home: React.FC = () => {
  const { isDark, toggleTheme } = useTheme();
  const [mobileMenuOpen, setMobileMenuOpen] = useState<boolean>(false);

  return (
    <div className="min-h-screen overflow-x-hidden font-sans antialiased transition-colors duration-300 bg-white dark:bg-[#0F172A] text-[#0F172A] dark:text-slate-50">

      {/* ══════════════════ NAVBAR ══════════════════ */}
      <header className="fixed top-0 left-0 right-0 z-50 h-16 backdrop-blur-xl transition-colors duration-300 bg-white/90 dark:bg-[#0F172A]/90 border-b border-[#E5E7EB] dark:border-white/10">
        <div className="h-full max-w-7xl mx-auto px-6 flex justify-between items-center">

          {/* Logo */}
          <div className="flex items-center gap-2.5">
            <img src={logo} alt="EHub" className="w-10 h-10 object-contain" />
            <span className="text-xl font-extrabold tracking-tight">
              <span className="text-[#F97316]">E</span> 
              <span className="text-[#0F172A] dark:text-white">HUB</span>
            </span>
          </div>

          {/* Desktop Nav */}
          <nav className="hidden md:flex gap-[60px] items-center">
            {['Features', 'How it works', 'About'].map(item => (
              <a key={item} href={`#${item.toLowerCase().replace(' ', '-')}`}
                className="text-sm font-semibold tracking-[-0.01em] transition-colors duration-180 ease-out text-[#64748B] hover:text-[#0F172A] dark:text-slate-400 dark:hover:text-white"
              >{item}</a>
            ))}
          </nav>

          {/* Right actions */}
          <div className="flex items-center gap-3">
            <Link to="/login" className="hidden sm:block text-sm font-semibold tracking-[-0.01em] transition-colors duration-180 ease-out text-[#64748B] hover:text-[#0F172A] dark:text-slate-400 dark:hover:text-white">
              Sign in
            </Link>

            <Link to="/register" className="hidden sm:flex items-center gap-1.5 px-5 py-2 rounded-[14px] text-sm font-semibold text-white bg-[linear-gradient(135deg,#EA6A12,#D97706)] shadow-[0_10px_28px_rgba(234,106,18,0.18)] hover:-translate-y-[2px] hover:shadow-[0_14px_36px_rgba(234,106,18,0.22)] transition-all duration-200 ease-out">
              Get Started <ArrowRight size={14} />
            </Link>

            {/* Theme Toggle */}
            <button onClick={toggleTheme} aria-label="Toggle theme"
              className="w-9 h-9 rounded-xl flex items-center justify-center transition-colors duration-200 border border-[#E5E7EB] dark:border-white/10 bg-white hover:bg-[#F8FAFC] dark:bg-white/5 dark:hover:bg-white/10 text-[#64748B] dark:text-slate-400 dark:hover:text-white"
            >
              {isDark ? <Sun size={16} /> : <Moon size={16} />}
            </button>

            <button onClick={() => setMobileMenuOpen(!mobileMenuOpen)} className="md:hidden p-2 rounded-xl text-[#64748B] dark:text-slate-300 hover:bg-[#F8FAFC] dark:hover:bg-white/10">
              {mobileMenuOpen ? <X size={20} /> : <Menu size={20} />}
            </button>
          </div>
        </div>

        {/* Mobile menu */}
        {mobileMenuOpen && (
          <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }}
            className="flex flex-col gap-3 p-6 border-t bg-white dark:bg-[#0F172A] border-[#E5E7EB] dark:border-white/10">
            {['Features', 'How it works', 'About'].map(item => (
              <a key={item} href={`#${item.toLowerCase().replace(' ', '-')}`}
                onClick={() => setMobileMenuOpen(false)}
                className="text-sm font-semibold text-[#64748B] dark:text-slate-400">{item}</a>
            ))}
            <Link to="/login" className="text-sm font-semibold text-[#64748B] dark:text-slate-400" onClick={() => setMobileMenuOpen(false)}>Sign in</Link>
            <Link to="/register" className="text-sm font-semibold text-white bg-[linear-gradient(135deg,#EA6A12,#D97706)] text-center px-4 py-2.5 rounded-[14px]" onClick={() => setMobileMenuOpen(false)}>Get Started</Link>
          </motion.div>
        )}
      </header>

      <main>
        {/* ══════════════════ HERO ══════════════════ */}
        <section className="relative flex items-center min-h-screen pt-32 pb-24 overflow-hidden bg-white dark:bg-[#0F172A]">
          {/* BG radials */}
          <div className="absolute inset-0 bg-[radial-gradient(ellipse_85%_58%_at_50%_-12%,rgba(15,23,42,0.06)_0%,transparent_70%)] dark:bg-[radial-gradient(ellipse_85%_58%_at_50%_-12%,rgba(148,163,184,0.12)_0%,transparent_70%)] transition-colors" />
          <div className="absolute inset-0 bg-[radial-gradient(ellipse_60%_52%_at_88%_76%,rgba(234,106,18,0.055)_0%,transparent_64%)] dark:bg-[radial-gradient(ellipse_60%_52%_at_88%_76%,rgba(234,106,18,0.08)_0%,transparent_64%)] transition-colors" />
          
          {/* Grid pattern (handled via a repeating linear gradient in CSS or a background image) */}
          <div className="absolute inset-0 opacity-[0.03] dark:opacity-[0.05] pointer-events-none" style={{ backgroundImage: 'linear-gradient(currentColor 1px, transparent 1px), linear-gradient(90deg, currentColor 1px, transparent 1px)', backgroundSize: '80px 80px' }} />

          {/* Floating orbs */}
          <motion.div animate={{ y: [0, -20, 0] }} transition={{ duration: 6, repeat: Infinity, ease: 'easeInOut' }}
            className="absolute top-[15%] left-[8%] w-[320px] h-[320px] rounded-full blur-[110px] bg-[#0F172A]/[0.045] dark:bg-white/[0.04]" />
          <motion.div animate={{ y: [0, 20, 0] }} transition={{ duration: 8, repeat: Infinity, ease: 'easeInOut', delay: 2 }}
            className="absolute bottom-[18%] right-[5%] w-[420px] h-[420px] rounded-full blur-[120px] bg-[#EA6A12]/[0.055] dark:bg-[#EA6A12]/[0.09]" />

          <div className="relative z-10 w-full max-w-7xl mx-auto px-6">
            <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)] gap-[88px] items-center">
              {/* Left */}
              <div>
                <motion.div initial="hidden" animate="visible" variants={fadeIn} custom={0}
                  className="inline-flex items-center gap-2 px-[18px] py-2.5 mb-8 rounded-full border bg-white/80 border-[#E5E7EB] shadow-[0_12px_32px_rgba(15,23,42,0.04)] dark:bg-[#111827]/80 dark:border-white/10">
                  <span className="w-1.5 h-1.5 rounded-full bg-[#F97316] shadow-[0_0_6px_rgba(249,115,22,0.45)]" />
                  <span className="text-[13px] font-semibold text-[#64748B] dark:text-slate-300">Now live — <span className="text-[#1E3A8A] dark:text-blue-300">EHUB Platform v1.0</span></span>
                </motion.div>

                <motion.h1 initial="hidden" animate="visible" variants={fadeUp} custom={1}
                  className="max-w-[720px] text-[clamp(56px,5vw,80px)] 2xl:text-[clamp(72px,6vw,96px)] font-black leading-[0.94] 2xl:leading-[0.92] tracking-[-0.055em] [word-spacing:-0.06em] mb-9 text-[#0F172A] dark:text-[#F8FAFC]">
                  <span className="block whitespace-nowrap">Entrepreneurship Hub</span>
                  <span className="block whitespace-nowrap">Management &</span>
                  <span className="block whitespace-nowrap">Startup Incubation</span>
             
                </motion.h1>

                <motion.p initial="hidden" animate="visible" variants={fadeUp} custom={2}
                  className="text-[22px] leading-[1.7] font-normal mb-11 max-w-[520px] text-[#64748B] dark:text-[#CBD5E1]">
                  A digital platform designed to support the management, evaluation, storage, and long-term development of student startup projects.
                </motion.p>

                <motion.div initial="hidden" animate="visible" variants={fadeUp} custom={3}
                  className="flex flex-wrap gap-4">
                  <Link to="/register"
                    className="flex h-14 items-center gap-2 px-8 rounded-[14px] text-[15px] font-semibold text-white bg-[linear-gradient(135deg,#EA6A12,#D97706)] shadow-[0_10px_28px_rgba(234,106,18,0.18)] hover:-translate-y-[2px] hover:shadow-[0_14px_36px_rgba(234,106,18,0.22)] transition-all duration-200 ease-out">
                    Start Now <ArrowRight size={18} />
                  </Link>
                  <Link to="/login"
                    className="flex h-14 items-center px-8 rounded-[14px] text-[15px] font-semibold transition-colors duration-200 ease-out border border-[#E5E7EB] bg-white hover:bg-[#F8FAFC] text-[#0F172A] dark:border-white/10 dark:bg-[#111827]/80 dark:hover:bg-white/10 dark:text-[#F8FAFC]">
                    Sign in
                  </Link>
                </motion.div>

                {/* Stats */}
                <motion.div initial="hidden" animate="visible" variants={fadeUp} custom={4}
                  className="flex flex-wrap items-center gap-8 mt-12">
                  {stats.map((s, i) => (
                    <div key={s.label} className={`text-center ${i < stats.length - 1 ? 'pr-8 border-r border-[#E5E7EB] dark:border-white/10' : ''}`}>
                      <p className="text-2xl font-bold leading-none text-[#0F172A] dark:text-[#F8FAFC]">{s.value}</p>
                      <p className="text-xs mt-1.5 text-[#64748B] dark:text-[#94A3B8]">{s.label}</p>
                    </div>
                  ))}
                </motion.div>
              </div>

              {/* Right — Dashboard Mockup */}
              <motion.div initial={{ opacity: 0, x: 112, y: 4, scale: 0.88 }} animate={{ opacity: 1, x: 59, y: 4, scale: 0.88 }} transition={{ delay: 0.4, duration: 0.8 }}
                className="hidden lg:block relative origin-center">
                <div className="absolute -inset-6 bg-[radial-gradient(ellipse_at_center,rgba(15,23,42,0.06)_0%,transparent_70%)] dark:bg-[radial-gradient(ellipse_at_center,rgba(255,255,255,0.06)_0%,transparent_70%)] blur-[22px]" />
                <div className="relative overflow-hidden rounded-[24px] border border-[#E8EDF5] dark:border-white/10 bg-white/[0.9] dark:bg-[#111827]/90 backdrop-blur-xl shadow-[0_24px_60px_rgba(15,23,42,0.08)] dark:shadow-[0_24px_60px_rgba(0,0,0,0.34)]">
                  {/* Window bar */}
                  <div className="flex items-center gap-1.5 px-4 py-2 border-b border-[#E8EDF5]/80 dark:border-white/10 bg-[#F8FAFC]/55 dark:bg-white/[0.04]">
                    {['#CBD5E1','#CBD5E1','#F97316'].map((c, i) => <div key={`${c}-${i}`} className="w-2 h-2 rounded-full opacity-70" style={{ background: c }} />)}
                    <div className="flex-1 h-5 ml-3 px-2.5 flex items-center rounded-md bg-white/80 dark:bg-[#0F172A]/70 border border-[#E8EDF5]/70 dark:border-white/10">
                      <span className="text-[10px] text-[#64748B]/80 dark:text-slate-400">ehub.platform.edu.vn</span>
                    </div>
                  </div>
                  <div className="p-5">
                    <div className="grid grid-cols-2 gap-3 mb-3.5">
                      {[
                        { label: 'Total Projects', val: '487', color: 'text-[#0F172A] dark:text-[#F8FAFC]' },
                        { label: 'Active Classes', val: '24',  color: 'text-[#0F172A] dark:text-[#F8FAFC]' },
                        { label: 'Startup Teams',  val: '96',  color: 'text-[#0F172A] dark:text-[#F8FAFC]' },
                        { label: 'AI Reviews',     val: '312', color: 'text-[#22C55E]' },
                      ].map(card => (
                        <div key={card.label} className="p-3.5 rounded-xl border border-[#E5E7EB]/80 dark:border-white/10 bg-white/95 dark:bg-[#0F172A]/60">
                          <p className="text-[10px] font-semibold uppercase tracking-wider mb-1.5 text-[#94A3B8] dark:text-slate-400">{card.label}</p>
                          <p className={`text-2xl font-extrabold ${card.color}`}>{card.val}</p>
                        </div>
                      ))}
                    </div>
                    <div className="p-4 rounded-xl border border-[#E5E7EB]/80 dark:border-white/10 bg-white/95 dark:bg-[#0F172A]/60">
                      <p className="text-[11px] font-semibold mb-3 text-[#94A3B8] dark:text-slate-400">PROJECT SUBMISSIONS</p>
                      <div className="flex items-end gap-1.5 h-[70px]">
                        {[30,55,40,70,50,85,60,75,45,90,65,80].map((h, i) => (
                          <motion.div key={i}
                            initial={{ height: 0 }} animate={{ height: `${h}%` }}
                            transition={{ delay: 0.8 + i * 0.05, duration: 0.5, ease: 'easeOut' }}
                            className={`flex-1 rounded-[4px] ${i%4===0 ? 'bg-[#F97316]/80' : 'bg-[#0F172A]/85 dark:bg-[#CBD5E1]'}`} />
                        ))}
                      </div>
                    </div>
                  </div>
                </div>
              </motion.div>
            </div>
          </div>
        </section>

        {/* ══════════════════ FEATURES ══════════════════ */}
        <section id="features" className="relative py-28 transition-colors bg-white dark:bg-[#0F172A]">
          <div className="absolute top-0 left-1/2 -translate-x-1/2 w-[600px] h-px bg-gradient-to-r from-transparent via-[#E5E7EB] dark:via-white/10 to-transparent" />
          <div className="max-w-7xl mx-auto px-6">
            <motion.div initial="hidden" whileInView="visible" viewport={{ once: true }} variants={fadeUp}
              className="text-center mb-16">
              <span className="inline-block px-4 py-1.5 rounded-full text-xs font-bold uppercase tracking-wider mb-5 border text-[#64748B] bg-[#F8FAFC] border-[#E5E7EB] dark:text-slate-300 dark:bg-white/5 dark:border-white/10">Platform Features</span>
              <h2 className="text-[clamp(28px,4vw,48px)] font-extrabold tracking-tight mb-4 text-[#0F172A] dark:text-white">Everything in one place</h2>
              <p className="text-[17px] leading-[1.75] max-w-[560px] mx-auto text-[#64748B] dark:text-slate-300">
                Built for educators, mentors, and students — EHub streamlines every step of the startup incubation journey.
              </p>
            </motion.div>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
              {features.map((f, i) => (
                <motion.div key={f.title}
                  initial="hidden" whileInView="visible" viewport={{ once: true }} variants={fadeUp} custom={i % 3}
                  whileHover={{ y: -6, transition: { duration: 0.25 } }}
                  className="group cursor-default p-7 rounded-xl transition-all duration-200 border border-[#E5E7EB] bg-[#F8FAFC] hover:bg-white hover:shadow-[0_18px_50px_rgba(15,23,42,0.08)] dark:border-white/10 dark:bg-white/[0.04] dark:hover:bg-white/[0.06]"
                >
                  <div className="w-12 h-12 rounded-xl flex items-center justify-center mb-5 border" style={{ backgroundColor: `${f.accent}15`, borderColor: `${f.accent}30` }}>
                    <f.icon size={22} color={f.accent} className="group-hover:scale-110 transition-transform" />
                  </div>
                  <h3 className="text-[17px] font-bold mb-2.5 text-[#0F172A] dark:text-white">{f.title}</h3>
                  <p className="text-[14px] leading-[1.75] text-[#64748B] dark:text-slate-300">{f.desc}</p>
                </motion.div>
              ))}
            </div>
          </div>
        </section>

        {/* ══════════════════ HOW IT WORKS ══════════════════ */}
        <section id="how-it-works" className="relative py-28 transition-colors bg-[#F8FAFC] dark:bg-[#111827]">
          <div className="max-w-7xl mx-auto px-6">
            <motion.div initial="hidden" whileInView="visible" viewport={{ once: true }} variants={fadeUp}
              className="text-center mb-16">
              <span className="inline-block px-4 py-1.5 rounded-full text-xs font-bold uppercase tracking-wider mb-5 border text-[#64748B] bg-white border-[#E5E7EB] dark:text-slate-300 dark:bg-white/5 dark:border-white/10">How It Works</span>
              <h2 className="text-[clamp(28px,4vw,48px)] font-extrabold tracking-tight text-[#0F172A] dark:text-white">Four simple steps</h2>
            </motion.div>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-8 relative">
              <div className="hidden lg:block absolute top-7 left-[12.5%] right-[12.5%] h-px bg-[#E5E7EB] dark:bg-white/10 z-0" />
              {steps.map((s, i) => (
                <motion.div key={s.num} initial="hidden" whileInView="visible" viewport={{ once: true }} variants={fadeUp} custom={i}
                  className="relative z-10 text-center px-3">
                  <div className="w-14 h-14 rounded-full mx-auto mb-5 flex items-center justify-center text-[17px] font-extrabold text-white bg-[#0F172A] shadow-[0_16px_36px_rgba(15,23,42,0.18)]">
                    {s.num}
                  </div>
                  <h3 className="text-[16px] font-bold mb-2.5 text-[#0F172A] dark:text-white">{s.title}</h3>
                  <p className="text-[13.5px] leading-[1.7] text-[#64748B] dark:text-slate-300">{s.desc}</p>
                </motion.div>
              ))}
            </div>
          </div>
        </section>

        {/* ══════════════════ STATS BANNER ══════════════════ */}
        <section className="transition-colors border-y border-[#E5E7EB] bg-white dark:border-white/10 dark:bg-[#0F172A]">
          <div className="max-w-7xl mx-auto py-14 px-6 grid grid-cols-2 lg:grid-cols-4 gap-8">
            {stats.map((s, i) => (
              <motion.div key={s.label} initial="hidden" whileInView="visible" viewport={{ once: true }} variants={fadeUp} custom={i}
                className="text-center py-2">
                <s.icon size={28} color="#F97316" className="mx-auto mb-3" />
                <p className="text-4xl font-black tracking-tight leading-none text-[#0F172A] dark:text-white">{s.value}</p>
                <p className="text-[13px] mt-2 text-[#64748B] dark:text-slate-400">{s.label}</p>
              </motion.div>
            ))}
          </div>
        </section>

        {/* ══════════════════ CTA ══════════════════ */}
        <section id="about" className="relative py-28 text-center overflow-hidden transition-colors bg-[#F8FAFC] dark:bg-[#111827]">
          <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[740px] h-[420px] bg-[radial-gradient(ellipse,rgba(15,23,42,0.08)_0%,rgba(249,115,22,0.06)_50%,transparent_70%)] dark:bg-[radial-gradient(ellipse,rgba(255,255,255,0.08)_0%,rgba(249,115,22,0.1)_50%,transparent_70%)] blur-[48px] pointer-events-none" />
          <motion.div initial="hidden" whileInView="visible" viewport={{ once: true }} variants={fadeUp}
            className="relative z-10 px-6">
            <div className="inline-flex items-center justify-center w-20 h-20 rounded-xl mb-8 bg-[#0F172A] shadow-[0_22px_55px_rgba(15,23,42,0.18)]">
              <Rocket size={32} color="#fff" />
            </div>
            <h2 className="text-[clamp(28px,4vw,52px)] font-extrabold tracking-tight mb-5 text-[#0F172A] dark:text-white">Ready to launch your startup?</h2>
            <p className="text-[17px] leading-[1.75] max-w-[500px] mx-auto mb-10 text-[#64748B] dark:text-slate-300">
              Join students and educators already using EHub to build, evaluate, and grow the next generation of startups.
            </p>
            <div className="flex flex-wrap justify-center gap-4">
              <Link to="/register"
                className="flex items-center gap-2 px-8 py-4 rounded-xl text-base font-bold text-white bg-[#F97316] shadow-[0_18px_42px_rgba(249,115,22,0.22)] hover:bg-[#EA6A12] hover:-translate-y-0.5 transition-all duration-200">
                Get started today <ArrowRight size={18} />
              </Link>
              <Link to="/login"
                className="px-8 py-4 rounded-xl text-base font-semibold transition-colors duration-200 border border-[#E5E7EB] bg-white hover:bg-[#F8FAFC] text-[#0F172A] dark:border-white/10 dark:bg-white/5 dark:hover:bg-white/10 dark:text-white">
                Sign in instead
              </Link>
            </div>
          </motion.div>
        </section>
      </main>

      {/* ══════════════════ FOOTER ══════════════════ */}
      <footer className="py-8 px-6 transition-colors border-t border-[#E5E7EB] bg-white dark:border-white/10 dark:bg-[#0F172A]">
        <div className="max-w-7xl mx-auto flex flex-col md:flex-row justify-between items-center gap-4">
          <div className="flex items-center gap-2.5">
            <img src={logo} alt="EHub" className="w-8 h-8 object-contain" />
            <span className="text-base font-extrabold">
              <span className="text-[#F97316]">E</span> 
              <span className="text-[#0F172A] dark:text-white">HUB</span>
            </span>
          </div>
          <p className="text-[13px] text-center text-[#64748B] dark:text-slate-400">
            © 2026 EHub — Entrepreneurship Hub Management & Startup Incubation Support Platform
          </p>
          <div className="flex gap-6">
            {['Features', 'How it works', 'About'].map(item => (
              <a key={item} href={`#${item.toLowerCase().replace(' ', '-')}`}
                className="text-[13px] transition-colors duration-200 text-[#64748B] hover:text-[#0F172A] dark:text-slate-400 dark:hover:text-white"
              >{item}</a>
            ))}
          </div>
        </div>
      </footer>
    </div>
  );
};

export default Home;
