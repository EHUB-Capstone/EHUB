import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import {
  ArrowRight, GraduationCap, Users, Brain, BarChart3,
  Rocket, Menu, X, CheckCircle2, TrendingUp, Shield, Star,
  Sun, Moon,
} from 'lucide-react';
import { useState } from 'react';
import logo from '../assets/logo.png';
import { useTheme } from '../context/ThemeContext';

/* ─── Animation variants ─────────────────────────────── */
const fadeUp = {
  hidden: { opacity: 0, y: 40 },
  visible: (i = 0) => ({
    opacity: 1, y: 0,
    transition: { delay: i * 0.12, duration: 0.65, ease: [0.22, 1, 0.36, 1] },
  }),
};
const fadeIn = {
  hidden: { opacity: 0 },
  visible: (i = 0) => ({ opacity: 1, transition: { delay: i * 0.1, duration: 0.5 } }),
};

/* ─── Types ──────────────────────────────────────────── */
interface Feature { icon: React.ElementType; title: string; desc: string; accent: string; }
interface Stat    { value: string; label: string; icon: React.ElementType; }
interface Step    { num: string; title: string; desc: string; }

/* ─── Static data ────────────────────────────────────── */
const features: Feature[] = [
  { icon: GraduationCap, title: 'Project Management',    desc: 'Organize and track student startup projects across semesters with structured workflows and milestone management.', accent: '#F37021' },
  { icon: Users,         title: 'Mentor Connection',     desc: 'Bridge the gap between mentors and student teams for focused coaching, feedback, and real-world guidance.', accent: '#034EA2' },
  { icon: Brain,         title: 'AI-Powered Evaluation', desc: 'Leverage AI to assess startup viability, feasibility, and market potential — fast, unbiased, and data-driven.', accent: '#0084c8' },
  { icon: BarChart3,     title: 'Progress Analytics',    desc: 'Visualize team performance with dashboards, KPI tracking, and detailed reports in real time.', accent: '#51B848' },
  { icon: Shield,        title: 'Secure Data Storage',   desc: 'All project data, documents, and evaluations are securely stored and easily accessible anytime.', accent: '#F37021' },
  { icon: TrendingUp,    title: 'Long-term Development', desc: 'Support startup journeys beyond a single semester — track growth, iterations, and outcomes over time.', accent: '#034EA2' },
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
    <div className="min-h-screen overflow-x-hidden font-sans antialiased transition-colors duration-300 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-slate-50">

      {/* ══════════════════ NAVBAR ══════════════════ */}
      <header className="fixed top-0 left-0 right-0 z-50 h-16 backdrop-blur-xl transition-colors duration-300 bg-white/80 dark:bg-slate-950/80 border-b border-slate-200 dark:border-slate-800">
        <div className="h-full max-w-7xl mx-auto px-6 flex justify-between items-center">

          {/* Logo */}
          <div className="flex items-center gap-2.5">
            <img src={logo} alt="EHub" className="w-10 h-10 object-contain" />
            <span className="text-xl font-extrabold tracking-tight">
              <span className="text-[#034EA2]">E</span>
              <span className="text-[#F37021]">HUB</span>
            </span>
          </div>

          {/* Desktop Nav */}
          <nav className="hidden md:flex gap-8 items-center">
            {['Features', 'How it works', 'About'].map(item => (
              <a key={item} href={`#${item.toLowerCase().replace(' ', '-')}`}
                className="text-sm font-medium transition-colors text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-slate-50"
              >{item}</a>
            ))}
          </nav>

          {/* Right actions */}
          <div className="flex items-center gap-3">
            <Link to="/login" className="hidden sm:block text-sm font-medium transition-colors text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-slate-50">
              Sign in
            </Link>

            <Link to="/register" className="hidden sm:flex items-center gap-1.5 px-5 py-2 rounded-lg text-sm font-semibold text-white bg-gradient-to-br from-[#F37021] to-[#e05a10] shadow-[0_0_15px_rgba(243,112,33,0.3)] hover:-translate-y-[1px] hover:shadow-[0_0_20px_rgba(243,112,33,0.5)] transition-all">
              Get Started <ArrowRight size={14} />
            </Link>

            {/* Theme Toggle */}
            <button onClick={toggleTheme} aria-label="Toggle theme"
              className="w-9 h-9 rounded-lg flex items-center justify-center transition-colors border border-slate-200 dark:border-slate-800 bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-500 dark:text-slate-400 dark:hover:text-slate-50"
            >
              {isDark ? <Sun size={16} /> : <Moon size={16} />}
            </button>

            <button onClick={() => setMobileMenuOpen(!mobileMenuOpen)} className="md:hidden p-2 rounded-lg text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800">
              {mobileMenuOpen ? <X size={20} /> : <Menu size={20} />}
            </button>
          </div>
        </div>

        {/* Mobile menu */}
        {mobileMenuOpen && (
          <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }}
            className="flex flex-col gap-3 p-6 border-t bg-white dark:bg-slate-950 border-slate-200 dark:border-slate-800">
            {['Features', 'How it works', 'About'].map(item => (
              <a key={item} href={`#${item.toLowerCase().replace(' ', '-')}`}
                onClick={() => setMobileMenuOpen(false)}
                className="text-sm font-medium text-slate-600 dark:text-slate-400">{item}</a>
            ))}
            <Link to="/login" className="text-sm font-medium text-slate-600 dark:text-slate-400" onClick={() => setMobileMenuOpen(false)}>Sign in</Link>
            <Link to="/register" className="text-sm font-semibold text-white bg-gradient-to-br from-[#F37021] to-[#e05a10] text-center px-4 py-2.5 rounded-lg" onClick={() => setMobileMenuOpen(false)}>Get Started</Link>
          </motion.div>
        )}
      </header>

      <main>
        {/* ══════════════════ HERO ══════════════════ */}
        <section className="relative flex items-center min-h-screen pt-20 overflow-hidden">
          {/* BG radials */}
          <div className="absolute inset-0 bg-[radial-gradient(ellipse_80%_60%_at_50%_-10%,rgba(3,78,162,0.1)_0%,transparent_70%)] dark:bg-[radial-gradient(ellipse_80%_60%_at_50%_-10%,rgba(3,78,162,0.32)_0%,transparent_70%)] transition-colors" />
          <div className="absolute inset-0 bg-[radial-gradient(ellipse_60%_50%_at_90%_80%,rgba(243,112,33,0.1)_0%,transparent_60%)] dark:bg-[radial-gradient(ellipse_60%_50%_at_90%_80%,rgba(243,112,33,0.15)_0%,transparent_60%)] transition-colors" />
          
          {/* Grid pattern (handled via a repeating linear gradient in CSS or a background image) */}
          <div className="absolute inset-0 opacity-[0.03] dark:opacity-5 pointer-events-none" style={{ backgroundImage: 'linear-gradient(currentColor 1px, transparent 1px), linear-gradient(90deg, currentColor 1px, transparent 1px)', backgroundSize: '60px 60px' }} />

          {/* Floating orbs */}
          <motion.div animate={{ y: [0, -20, 0] }} transition={{ duration: 6, repeat: Infinity, ease: 'easeInOut' }}
            className="absolute top-[15%] left-[8%] w-[280px] h-[280px] rounded-full blur-[80px] bg-[#034EA2]/10 dark:bg-[#034EA2]/30" />
          <motion.div animate={{ y: [0, 20, 0] }} transition={{ duration: 8, repeat: Infinity, ease: 'easeInOut', delay: 2 }}
            className="absolute bottom-[20%] right-[5%] w-[350px] h-[350px] rounded-full blur-[90px] bg-[#F37021]/10 dark:bg-[#F37021]/20" />

          <div className="relative z-10 w-full max-w-7xl mx-auto px-6">
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-16 items-center">
              {/* Left */}
              <div>
                <motion.div initial="hidden" animate="visible" variants={fadeIn} custom={0}
                  className="inline-flex items-center gap-2 px-4 py-1.5 mb-7 rounded-full border bg-orange-50/50 border-orange-200 dark:bg-[#F37021]/10 dark:border-[#F37021]/30">
                  <span className="w-1.5 h-1.5 rounded-full bg-[#F37021] shadow-[0_0_6px_#F37021]" />
                  <span className="text-[13px] font-semibold text-[#F37021]">Now live — EHub Platform v1.0</span>
                </motion.div>

                <motion.h1 initial="hidden" animate="visible" variants={fadeUp} custom={1}
                  className="text-[clamp(36px,5vw,62px)] font-extrabold leading-[1.1] tracking-tight mb-6 text-slate-900 dark:text-white">
                  Entrepreneurship Hub{' '}
                  <span className="text-transparent bg-clip-text bg-gradient-to-br from-[#F37021] via-[#f5a623] to-[#034EA2]">
                    Management
                  </span>
                  {' & Startup Incubation'}
                </motion.h1>

                <motion.p initial="hidden" animate="visible" variants={fadeUp} custom={2}
                  className="text-lg leading-relaxed mb-10 max-w-[500px] text-slate-600 dark:text-slate-400">
                  A digital platform designed to support the management, evaluation, storage, and long-term development of student startup projects.
                </motion.p>

                <motion.div initial="hidden" animate="visible" variants={fadeUp} custom={3}
                  className="flex flex-wrap gap-4">
                  <Link to="/register"
                    className="flex items-center gap-2 px-7 py-3.5 rounded-xl text-[15px] font-bold text-white bg-gradient-to-br from-[#F37021] to-[#e05a10] shadow-[0_0_30px_rgba(243,112,33,0.3)] hover:-translate-y-[2px] hover:shadow-[0_4px_40px_rgba(243,112,33,0.5)] transition-all">
                    Start for free <ArrowRight size={18} />
                  </Link>
                  <Link to="/login"
                    className="px-7 py-3.5 rounded-xl text-[15px] font-semibold transition-colors border border-slate-200 bg-white hover:bg-slate-50 text-slate-900 dark:border-slate-800 dark:bg-slate-800/50 dark:hover:bg-slate-800 dark:text-slate-100">
                    Sign in
                  </Link>
                </motion.div>

                {/* Stats */}
                <motion.div initial="hidden" animate="visible" variants={fadeUp} custom={4}
                  className="flex flex-wrap items-center gap-8 mt-12">
                  {stats.map((s, i) => (
                    <div key={s.label} className={`text-center ${i < stats.length - 1 ? 'pr-8 border-r border-slate-200 dark:border-slate-800' : ''}`}>
                      <p className="text-2xl font-extrabold leading-none text-slate-900 dark:text-white">{s.value}</p>
                      <p className="text-xs mt-1.5 text-slate-500 dark:text-slate-400">{s.label}</p>
                    </div>
                  ))}
                </motion.div>
              </div>

              {/* Right — Dashboard Mockup */}
              <motion.div initial={{ opacity: 0, x: 40 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: 0.4, duration: 0.8 }}
                className="hidden lg:block relative">
                <div className="absolute -inset-8 bg-[radial-gradient(ellipse_at_center,rgba(3,78,162,0.15)_0%,transparent_70%)] dark:bg-[radial-gradient(ellipse_at_center,rgba(3,78,162,0.3)_0%,transparent_70%)] blur-[20px]" />
                <div className="relative overflow-hidden rounded-[20px] border border-slate-200 dark:border-slate-700/50 bg-white/50 dark:bg-slate-900/40 backdrop-blur-xl shadow-[0_30px_80px_rgba(0,0,0,0.05)] dark:shadow-[0_30px_80px_rgba(0,0,0,0.5)]">
                  {/* Window bar */}
                  <div className="flex items-center gap-2 px-4 py-3.5 border-b border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/50">
                    {['#ef4444','#f59e0b','#22c55e'].map(c => <div key={c} className="w-2.5 h-2.5 rounded-full" style={{ background: c }} />)}
                    <div className="flex-1 h-6 ml-3 px-2.5 flex items-center rounded-md bg-white dark:bg-slate-900/50">
                      <span className="text-[11px] text-slate-400 dark:text-slate-500">ehub.platform.edu.vn</span>
                    </div>
                  </div>
                  <div className="p-5">
                    <div className="grid grid-cols-2 gap-3 mb-3.5">
                      {[
                        { label: 'Total Projects', val: '487', color: 'text-[#F37021]' },
                        { label: 'Active Classes', val: '24',  color: 'text-[#034EA2]' },
                        { label: 'Startup Teams',  val: '96',  color: 'text-[#0084c8]' },
                        { label: 'AI Reviews',     val: '312', color: 'text-[#51B848]' },
                      ].map(card => (
                        <div key={card.label} className="p-3.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-950/50">
                          <p className="text-[10px] font-semibold uppercase tracking-wider mb-1.5 text-slate-500 dark:text-slate-400">{card.label}</p>
                          <p className={`text-2xl font-extrabold ${card.color}`}>{card.val}</p>
                        </div>
                      ))}
                    </div>
                    <div className="p-4 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-950/50">
                      <p className="text-[11px] font-semibold mb-3 text-slate-500 dark:text-slate-400">PROJECT SUBMISSIONS</p>
                      <div className="flex items-end gap-1.5 h-[70px]">
                        {[30,55,40,70,50,85,60,75,45,90,65,80].map((h, i) => (
                          <motion.div key={i}
                            initial={{ height: 0 }} animate={{ height: `${h}%` }}
                            transition={{ delay: 0.8 + i * 0.05, duration: 0.5, ease: 'easeOut' }}
                            className={`flex-1 rounded-[4px] opacity-85 ${i%3===0 ? 'bg-[#F37021]' : i%3===1 ? 'bg-[#034EA2]' : 'bg-[#0084c8]'}`} />
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
        <section id="features" className="relative py-28 transition-colors bg-white dark:bg-slate-950">
          <div className="absolute top-0 left-1/2 -translate-x-1/2 w-[600px] h-px bg-gradient-to-r from-transparent via-slate-200 dark:via-slate-800 to-transparent" />
          <div className="max-w-7xl mx-auto px-6">
            <motion.div initial="hidden" whileInView="visible" viewport={{ once: true }} variants={fadeUp}
              className="text-center mb-16">
              <span className="inline-block px-4 py-1.5 rounded-full text-xs font-bold uppercase tracking-wider mb-5 border text-[#034EA2] bg-[#034EA2]/10 border-[#034EA2]/20 dark:text-[#5b9bd5] dark:bg-[#034EA2]/20 dark:border-[#034EA2]/40">Platform Features</span>
              <h2 className="text-[clamp(28px,4vw,48px)] font-extrabold tracking-tight mb-4 text-slate-900 dark:text-white">Everything in one place</h2>
              <p className="text-[17px] max-w-[520px] mx-auto text-slate-600 dark:text-slate-400">
                Built for educators, mentors, and students — EHub streamlines every step of the startup incubation journey.
              </p>
            </motion.div>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
              {features.map((f, i) => (
                <motion.div key={f.title}
                  initial="hidden" whileInView="visible" viewport={{ once: true }} variants={fadeUp} custom={i % 3}
                  whileHover={{ y: -6, transition: { duration: 0.25 } }}
                  className="group cursor-default p-7 rounded-[18px] transition-all duration-300 border border-slate-200 bg-slate-50 hover:shadow-lg dark:border-slate-800 dark:bg-slate-900/50 dark:hover:bg-slate-900"
                >
                  <div className="w-12 h-12 rounded-xl flex items-center justify-center mb-5 border" style={{ backgroundColor: `${f.accent}15`, borderColor: `${f.accent}30` }}>
                    <f.icon size={22} color={f.accent} className="group-hover:scale-110 transition-transform" />
                  </div>
                  <h3 className="text-[17px] font-bold mb-2.5 text-slate-900 dark:text-white">{f.title}</h3>
                  <p className="text-[14px] leading-[1.7] text-slate-600 dark:text-slate-400">{f.desc}</p>
                </motion.div>
              ))}
            </div>
          </div>
        </section>

        {/* ══════════════════ HOW IT WORKS ══════════════════ */}
        <section id="how-it-works" className="relative py-28 transition-colors bg-slate-50 dark:bg-slate-900">
          <div className="max-w-7xl mx-auto px-6">
            <motion.div initial="hidden" whileInView="visible" viewport={{ once: true }} variants={fadeUp}
              className="text-center mb-16">
              <span className="inline-block px-4 py-1.5 rounded-full text-xs font-bold uppercase tracking-wider mb-5 border text-[#F37021] bg-[#F37021]/10 border-[#F37021]/20 dark:border-[#F37021]/40">How It Works</span>
              <h2 className="text-[clamp(28px,4vw,48px)] font-extrabold tracking-tight text-slate-900 dark:text-white">Four simple steps</h2>
            </motion.div>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-8 relative">
              <div className="hidden lg:block absolute top-7 left-[12.5%] right-[12.5%] h-px bg-gradient-to-r from-[#F37021] to-[#034EA2] opacity-30 z-0" />
              {steps.map((s, i) => (
                <motion.div key={s.num} initial="hidden" whileInView="visible" viewport={{ once: true }} variants={fadeUp} custom={i}
                  className="relative z-10 text-center px-3">
                  <div className={`w-14 h-14 rounded-full mx-auto mb-5 flex items-center justify-center text-[17px] font-extrabold text-white shadow-lg ${i%2===0 ? 'bg-gradient-to-br from-[#F37021] to-[#e05a10] shadow-[#F37021]/40' : 'bg-gradient-to-br from-[#034EA2] to-[#023a78] shadow-[#034EA2]/40'}`}>
                    {s.num}
                  </div>
                  <h3 className="text-[16px] font-bold mb-2.5 text-slate-900 dark:text-white">{s.title}</h3>
                  <p className="text-[13.5px] leading-[1.65] text-slate-600 dark:text-slate-400">{s.desc}</p>
                </motion.div>
              ))}
            </div>
          </div>
        </section>

        {/* ══════════════════ STATS BANNER ══════════════════ */}
        <section className="transition-colors border-y border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-950">
          <div className="max-w-7xl mx-auto py-14 px-6 grid grid-cols-2 lg:grid-cols-4 gap-8">
            {stats.map((s, i) => (
              <motion.div key={s.label} initial="hidden" whileInView="visible" viewport={{ once: true }} variants={fadeUp} custom={i}
                className="text-center py-2">
                <s.icon size={28} color={i%2===0?'#F37021':'#034EA2'} className="mx-auto mb-3" />
                <p className="text-4xl font-black tracking-tight leading-none text-slate-900 dark:text-white">{s.value}</p>
                <p className="text-[13px] mt-2 text-slate-500 dark:text-slate-400">{s.label}</p>
              </motion.div>
            ))}
          </div>
        </section>

        {/* ══════════════════ CTA ══════════════════ */}
        <section id="about" className="relative py-28 text-center overflow-hidden transition-colors bg-slate-50 dark:bg-slate-900">
          <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[700px] h-[400px] bg-[radial-gradient(ellipse,rgba(3,78,162,0.1)_0%,rgba(243,112,33,0.05)_50%,transparent_70%)] dark:bg-[radial-gradient(ellipse,rgba(3,78,162,0.2)_0%,rgba(243,112,33,0.1)_50%,transparent_70%)] blur-[40px] pointer-events-none" />
          <motion.div initial="hidden" whileInView="visible" viewport={{ once: true }} variants={fadeUp}
            className="relative z-10 px-6">
            <div className="inline-flex items-center justify-center w-20 h-20 rounded-2xl mb-8 bg-gradient-to-br from-[#F37021] to-[#034EA2] shadow-[0_0_40px_rgba(243,112,33,0.3)]">
              <Rocket size={32} color="#fff" />
            </div>
            <h2 className="text-[clamp(28px,4vw,52px)] font-extrabold tracking-tight mb-5 text-slate-900 dark:text-white">Ready to launch your startup?</h2>
            <p className="text-[17px] max-w-[480px] mx-auto mb-10 text-slate-600 dark:text-slate-400">
              Join students and educators already using EHub to build, evaluate, and grow the next generation of startups.
            </p>
            <div className="flex flex-wrap justify-center gap-4">
              <Link to="/register"
                className="flex items-center gap-2 px-8 py-4 rounded-xl text-base font-bold text-white bg-gradient-to-br from-[#F37021] to-[#e05a10] shadow-[0_0_40px_rgba(243,112,33,0.3)] hover:-translate-y-0.5 transition-transform">
                Get started free <ArrowRight size={18} />
              </Link>
              <Link to="/login"
                className="px-8 py-4 rounded-xl text-base font-semibold transition-colors border border-slate-200 bg-white hover:bg-slate-100 text-slate-900 dark:border-slate-700 dark:bg-slate-800/50 dark:hover:bg-slate-800 dark:text-slate-100">
                Sign in instead
              </Link>
            </div>
          </motion.div>
        </section>
      </main>

      {/* ══════════════════ FOOTER ══════════════════ */}
      <footer className="py-8 px-6 transition-colors border-t border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-950">
        <div className="max-w-7xl mx-auto flex flex-col md:flex-row justify-between items-center gap-4">
          <div className="flex items-center gap-2.5">
            <img src={logo} alt="EHub" className="w-8 h-8 object-contain" />
            <span className="text-base font-extrabold">
              <span className="text-[#034EA2]">E</span>
              <span className="text-[#F37021]">HUB</span>
            </span>
          </div>
          <p className="text-[13px] text-center text-slate-500 dark:text-slate-400">
            © 2024 EHub — Entrepreneurship Hub Management & Startup Incubation Support Platform
          </p>
          <div className="flex gap-6">
            {['Features', 'How it works', 'About'].map(item => (
              <a key={item} href={`#${item.toLowerCase().replace(' ', '-')}`}
                className="text-[13px] transition-colors text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-slate-300"
              >{item}</a>
            ))}
          </div>
        </div>
      </footer>
    </div>
  );
};

export default Home;
