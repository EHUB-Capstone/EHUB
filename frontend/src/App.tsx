import { lazy, Suspense } from 'react';
import { BrowserRouter as Router, Route, Routes } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { QueryClientProvider } from '@tanstack/react-query';
import { ThemeProvider } from './context/ThemeContext';
import { AuthProvider } from './context/AuthContext';
import { ProtectedRoute } from './routes/ProtectedRoute';
import { queryClient } from './lib/queryClient';
import DashboardLayout from './components/layout/DashboardLayout';
import ErrorBoundary from './components/ui/ErrorBoundary';

import { classFeatureFlags } from './config/classFeatureFlags';
import { classRouteAccess } from './config/classAccessPolicy';

const Home = lazy(() => import('./pages/Home'));
const Login = lazy(() => import('./pages/auth/Login'));
const Register = lazy(() => import('./pages/auth/Register'));
const ForgotPassword = lazy(() => import('./pages/auth/ForgotPassword'));
const ResetPassword = lazy(() => import('./pages/auth/ResetPassword'));
const AdminDashboard = lazy(() => import('./pages/admin/AdminDashboard'));
const ClassManagement = lazy(() => import('./pages/admin/ClassManagement'));
const SubjectManagement = lazy(() => import('./pages/admin/SubjectManagement'));
const SubjectDetail = lazy(() => import('./pages/admin/SubjectDetail'));
const UserManagement = lazy(() => import('./pages/admin/UserManagement'));
const AccountApprovals = lazy(() => import('./pages/admin/AccountApprovals'));
const LecturerDashboard = lazy(() => import('./pages/lecturer/LecturerDashboard'));
const LecturerClasses = lazy(() => import('./pages/lecturer/LecturerClasses'));
const MentorDashboard = lazy(() => import('./pages/mentor/MentorDashboard'));
const StudentDashboard = lazy(() => import('./pages/student/StudentDashboard'));
const IdeaForm = lazy(() => import('./pages/student/IdeaForm'));
const MyClasses = lazy(() => import('./pages/student/MyClasses'));
const MyTeam = lazy(() => import('./pages/student/MyTeam'));
const StudentClassDetail = lazy(() => import('./pages/student/StudentClassDetail'));
const AIAnalysis = lazy(() => import('./pages/common/AIAnalysis'));
const ExecutionBoard = lazy(() => import('./pages/common/ExecutionBoard'));
const GroupChat = lazy(() => import('./pages/common/GroupChat'));
const IdeaDetail = lazy(() => import('./pages/common/IdeaDetail'));
const MentoringSessions = lazy(() => import('./pages/common/MentoringSessions'));
const Rankings = lazy(() => import('./pages/common/Rankings'));
const StartupWorkspaceHub = lazy(() => import('./pages/workspace/StartupWorkspaceHub'));
const TeamWorkspace = lazy(() => import('./pages/workspace/TeamWorkspace'));
const ProposalEditor = lazy(() => import('./pages/workspace/ProposalEditor'));
const Workshops = lazy(() => import('./pages/workshops/Workshops'));
const DataBankPage = lazy(() => import('./features/data-bank/DataBankPage'));
const ClassDetail = lazy(() => import('./pages/shared/ClassDetail'));
const Forbidden = lazy(() => import('./pages/shared/Forbidden'));
const NotFound = lazy(() => import('./pages/shared/NotFound'));
const ProfileSettings = lazy(() => import('./pages/shared/ProfileSettings'));

const PageFallback = () => (
  <div className="flex min-h-56 items-center justify-center" role="status" aria-label="Loading page">
    <div className="h-8 w-8 animate-spin rounded-full border-2 border-slate-200 border-t-primary" />
  </div>
);

function App(): React.ReactElement {
  return (
    <ErrorBoundary>
      <ThemeProvider>
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <Router>
              <Toaster
                position="top-right"
                toastOptions={{
                  duration: 3500,
                  style: {
                    background: 'var(--app-toast-bg)',
                    color: 'var(--app-toast-color)',
                    border: '1px solid var(--app-toast-border)',
                    borderRadius: '12px',
                    fontFamily: 'Inter, sans-serif',
                    fontSize: '14px',
                    padding: '12px 16px',
                    boxShadow: '0 4px 16px -4px rgb(0 0 0 / 0.2)',
                  },
                  success: { iconTheme: { primary: '#51B848', secondary: '#fff' } },
                  error: { iconTheme: { primary: '#ef4444', secondary: '#fff' } },
                }}
              />

              <Suspense fallback={<PageFallback />}>
              <Routes>
                <Route path="/" element={<Home />} />
                <Route path="/login" element={<Login />} />
                <Route path="/register" element={<Register />} />
                <Route path="/forgot-password" element={<ForgotPassword />} />
                <Route path="/reset-password" element={<ResetPassword />} />
                <Route path="/reset-password/:token" element={<ResetPassword />} />

                <Route element={<ProtectedRoute><DashboardLayout /></ProtectedRoute>}>
                  <Route path="/admin" element={<ProtectedRoute allowedRoles={['ADMIN']}><AdminDashboard /></ProtectedRoute>} />
                  <Route path="/admin/users" element={<ProtectedRoute allowedRoles={['ADMIN']}><UserManagement /></ProtectedRoute>} />
                  <Route path="/admin/account-approvals" element={<ProtectedRoute allowedRoles={['ADMIN']}><AccountApprovals /></ProtectedRoute>} />
                  <Route path="/admin/classes" element={<ProtectedRoute allowedRoles={['ADMIN']}><ClassManagement /></ProtectedRoute>} />
                  <Route path="/admin/subjects" element={<ProtectedRoute allowedRoles={['ADMIN']}><SubjectManagement /></ProtectedRoute>} />
                  <Route path="/admin/subjects/:subjectCode" element={<ProtectedRoute allowedRoles={['ADMIN']}><SubjectDetail /></ProtectedRoute>} />

                  <Route path="/lecturer" element={<ProtectedRoute allowedRoles={[...classRouteAccess.lecturerArea]}><LecturerDashboard /></ProtectedRoute>} />
                  <Route path="/lecturer/classes" element={<ProtectedRoute allowedRoles={[...classRouteAccess.lecturerArea]}><LecturerClasses /></ProtectedRoute>} />
                  <Route path="/lecturer/data-bank" element={<ProtectedRoute allowedRoles={['ADMIN', 'LECTURER']}><DataBankPage /></ProtectedRoute>} />
                  <Route path="/mentor" element={<ProtectedRoute allowedRoles={['MENTOR']}><MentorDashboard /></ProtectedRoute>} />

                  <Route path="/classes/:id" element={<ProtectedRoute allowedRoles={[...classRouteAccess.classDetail]}><ClassDetail /></ProtectedRoute>} />

                  <Route path="/student" element={<ProtectedRoute allowedRoles={['STUDENT']}><StudentDashboard /></ProtectedRoute>} />
                  <Route path="/student/idea/new" element={<ProtectedRoute allowedRoles={['STUDENT']}><IdeaForm /></ProtectedRoute>} />
                  <Route path="/student/idea/:id" element={<ProtectedRoute allowedRoles={['STUDENT']}><IdeaDetail /></ProtectedRoute>} />
                  <Route path="/student/feedback" element={<ProtectedRoute allowedRoles={['STUDENT']}><IdeaDetail /></ProtectedRoute>} />
                  <Route path="/student/ai-analysis" element={<ProtectedRoute allowedRoles={['STUDENT']}><AIAnalysis /></ProtectedRoute>} />
                  <Route path="/student/ai-analysis/:startupIdeaId" element={<ProtectedRoute allowedRoles={['STUDENT']}><AIAnalysis /></ProtectedRoute>} />
                  {classFeatureFlags.studentSelfService && (
                    <>
                      <Route path="/student/classes" element={<ProtectedRoute allowedRoles={['STUDENT']}><MyClasses /></ProtectedRoute>} />
                      <Route path="/student/classes/:id" element={<ProtectedRoute allowedRoles={['STUDENT']}><StudentClassDetail /></ProtectedRoute>} />
                      <Route path="/student/team" element={<ProtectedRoute allowedRoles={['STUDENT']}><MyTeam /></ProtectedRoute>} />
                    </>
                  )}

                  <Route path="/workspace" element={<ProtectedRoute allowedRoles={['ADMIN', 'LECTURER', 'MENTOR']}><StartupWorkspaceHub /></ProtectedRoute>} />
                  <Route path="/student/workspace" element={<ProtectedRoute allowedRoles={['STUDENT']}><TeamWorkspace /></ProtectedRoute>} />
                  <Route path="/student/workspace/proposal" element={<ProtectedRoute allowedRoles={['STUDENT']}><ProposalEditor /></ProtectedRoute>} />
                  <Route path="/workspace/teams/:teamId" element={<TeamWorkspace />} />
                  <Route path="/workspace/teams/:teamId/proposal" element={<ProposalEditor />} />

                  <Route path="/rankings" element={<Rankings />} />
                  <Route path="/evaluations" element={<IdeaDetail />} />
                  <Route path="/executionboard" element={<ExecutionBoard />} />
                  <Route path="/sessions" element={<MentoringSessions />} />
                  <Route path="/workshops" element={<Workshops />} />
                  <Route path="/chat" element={<GroupChat />} />
                  <Route path="/settings" element={<ProfileSettings />} />
                </Route>

                <Route path="/403" element={<Forbidden />} />
                <Route path="/unauthorized" element={<Forbidden />} />
                <Route path="*" element={<NotFound />} />
              </Routes>
              </Suspense>
            </Router>
          </AuthProvider>
        </QueryClientProvider>
      </ThemeProvider>
    </ErrorBoundary>
  );
}

export default App;
