import { useState } from 'react';
import { Outlet } from 'react-router-dom';
import Sidebar from './Sidebar';
import Navbar from './Navbar';

const DashboardLayout = (): React.ReactElement => {
  const [sidebarOpen, setSidebarOpen] = useState(false);

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
          <Outlet />
        </main>
      </div>
    </div>
  );
};

export default DashboardLayout;
