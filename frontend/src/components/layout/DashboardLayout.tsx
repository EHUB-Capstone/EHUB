import { lazy, Suspense, useEffect, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import Sidebar from './Sidebar';
import Navbar from './Navbar';
import { releaseFeatureFlags } from '../../config/releaseFeatureFlags';
import { isShowcaseAssistantRoute } from '../../utils/showcaseAssistant';
import i18n, { loadAllNamespaces } from '../../i18n';
import { useTranslation } from 'react-i18next';

const ShowcaseAssistant = lazy(() => import('../../features/showcase/components/ShowcaseAssistant'));

const DashboardLayout = (): React.ReactElement => {
  const { t } = useTranslation();
  const [sidebarOpen, setSidebarOpen] = useState(false);

  // Feature translations are separate chunks; fetch the rest in the background so later screens open instantly.
  useEffect(() => {
    const prefetch = () => { void loadAllNamespaces(i18n.language); };
    if (typeof window.requestIdleCallback === 'function') {
      const handle = window.requestIdleCallback(prefetch, { timeout: 4000 });
      return () => window.cancelIdleCallback(handle);
    }
    const timer = window.setTimeout(prefetch, 2000);
    return () => window.clearTimeout(timer);
  }, []);
  const { pathname } = useLocation();
  // Rendered here, not inside each page, so the conversation survives moving between the gallery and a project.
  const showAssistant = releaseFeatureFlags.showcase && isShowcaseAssistantRoute(pathname);

  return (
    <div className="dashboard-shell min-h-screen">
      {/* Mobile overlay */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-40 lg:hidden"
          onClick={() => setSidebarOpen(false)}
          aria-hidden="true"
        />
      )}

      <Sidebar
        mobileOpen={sidebarOpen}
        onMobileClose={() => setSidebarOpen(false)}
      />

      <div className="lg:ml-[260px] flex flex-col min-h-screen transition-all duration-300">
        <Navbar onMenuClick={() => setSidebarOpen(true)} />
        <main className="flex-1 p-4 sm:p-6 lg:p-8 max-w-[1400px] w-full mx-auto">
          <Suspense fallback={<div className="flex min-h-56 items-center justify-center" role="status" aria-label={t('states.loadingPage')}><div className="h-8 w-8 animate-spin rounded-full border-2 border-slate-200 border-t-primary" /></div>}>
            <Outlet />
          </Suspense>
        </main>
      </div>
      {showAssistant && <Suspense fallback={null}><ShowcaseAssistant /></Suspense>}
    </div>
  );
};

export default DashboardLayout;
