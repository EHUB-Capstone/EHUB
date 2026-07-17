import { BrowserRouter as Router, Route, Routes } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { QueryClientProvider } from '@tanstack/react-query';
import { ThemeProvider } from './context/ThemeContext';
import { AuthProvider } from './context/AuthContext';
import { ProtectedRoute } from './routes/ProtectedRoute';
import { queryClient } from './lib/queryClient';
import DashboardLayout from './components/layout/DashboardLayout';
import ErrorBoundary from './components/ui/ErrorBoundary';

import Home from './pages/Home';
import Login from './pages/auth/Login';
import Register from './pages/auth/Register';
import ForgotPassword from './pages/auth/ForgotPassword';
import ResetPassword from './pages/auth/ResetPassword';

import AdminDashboard from './pages/admin/AdminDashboard';
import ClassManagement from './pages/admin/ClassManagement';
import SubjectManagement from './pages/admin/SubjectManagement';
import UserManagement from './pages/admin/UserManagement';

import LecturerDashboard from './pages/lecturer/LecturerDashboard';
import LecturerClasses from './pages/lecturer/LecturerClasses';
import MentorDashboard from './pages/mentor/MentorDashboard';

import StudentDashboard from './pages/student/StudentDashboard';
import IdeaForm from './pages/student/IdeaForm';
import MyClasses from './pages/student/MyClasses';
import MyTeam from './pages/student/MyTeam';
import StudentClassDetail from './pages/student/StudentClassDetail';

import AIAnalysis from './pages/common/AIAnalysis';
import ExecutionBoard from './pages/common/ExecutionBoard';
import GroupChat from './pages/common/GroupChat';
import IdeaDetail from './pages/common/IdeaDetail';
import MentoringSessions from './pages/common/MentoringSessions';
import Rankings from './pages/common/Rankings';

import StartupWorkspaceHub from './pages/workspace/StartupWorkspaceHub';
import TeamWorkspace from './pages/workspace/TeamWorkspace';
import ProposalEditor from './pages/workspace/ProposalEditor';
import Workshops from './pages/workshops/Workshops';
import DataBankPage from './features/data-bank/DataBankPage';

import ClassDetail from './pages/shared/ClassDetail';
import Forbidden from './pages/shared/Forbidden';
import NotFound from './pages/shared/NotFound';
import ProfileSettings from './pages/shared/ProfileSettings';

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
                  <Route path="/admin/classes" element={<ProtectedRoute allowedRoles={['ADMIN']}><ClassManagement /></ProtectedRoute>} />
                  <Route path="/admin/subjects" element={<ProtectedRoute allowedRoles={['ADMIN']}><SubjectManagement /></ProtectedRoute>} />

                  <Route path="/lecturer" element={<ProtectedRoute allowedRoles={['LECTURER', 'MENTOR']}><LecturerDashboard /></ProtectedRoute>} />
                  <Route path="/lecturer/classes" element={<ProtectedRoute allowedRoles={['LECTURER', 'MENTOR']}><LecturerClasses /></ProtectedRoute>} />
                  <Route path="/lecturer/data-bank" element={<ProtectedRoute allowedRoles={['ADMIN', 'LECTURER']}><DataBankPage /></ProtectedRoute>} />
                  <Route path="/mentor" element={<ProtectedRoute allowedRoles={['MENTOR']}><MentorDashboard /></ProtectedRoute>} />

                  <Route path="/classes/:id" element={<ProtectedRoute allowedRoles={['ADMIN', 'LECTURER', 'MENTOR']}><ClassDetail /></ProtectedRoute>} />

                  <Route path="/student" element={<ProtectedRoute allowedRoles={['STUDENT']}><StudentDashboard /></ProtectedRoute>} />
                  <Route path="/student/idea/new" element={<ProtectedRoute allowedRoles={['STUDENT']}><IdeaForm /></ProtectedRoute>} />
                  <Route path="/student/idea/:id" element={<ProtectedRoute allowedRoles={['STUDENT']}><IdeaDetail /></ProtectedRoute>} />
                  <Route path="/student/feedback" element={<ProtectedRoute allowedRoles={['STUDENT']}><IdeaDetail /></ProtectedRoute>} />
                  <Route path="/student/ai-analysis" element={<ProtectedRoute allowedRoles={['STUDENT']}><AIAnalysis /></ProtectedRoute>} />
                  <Route path="/student/ai-analysis/:startupIdeaId" element={<ProtectedRoute allowedRoles={['STUDENT']}><AIAnalysis /></ProtectedRoute>} />
                  <Route path="/student/classes" element={<ProtectedRoute allowedRoles={['STUDENT']}><MyClasses /></ProtectedRoute>} />
                  <Route path="/student/classes/:id" element={<ProtectedRoute allowedRoles={['STUDENT']}><StudentClassDetail /></ProtectedRoute>} />
                  <Route path="/student/team" element={<ProtectedRoute allowedRoles={['STUDENT']}><MyTeam /></ProtectedRoute>} />

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
            </Router>
          </AuthProvider>
        </QueryClientProvider>
      </ThemeProvider>
    </ErrorBoundary>
  );
}

export default App;
