import fs from 'fs';
import path from 'path';

// For ES modules, __dirname is not defined, we can use process.cwd() instead since we run it from frontend/
const srcDir = path.join(process.cwd(), 'src');

const structure = {
  'api': [
    'aiApi.ts', 'authApi.ts', 'axiosClient.ts', 'chatApi.ts', 'checkpointApi.ts',
    'classApi.ts', 'commentApi.ts', 'dashboardApi.ts', 'dataBankApi.ts', 'evaluationApi.ts',
    'mentoringApi.ts', 'milestoneApi.ts', 'notificationApi.ts', 'rankingApi.ts', 'shortcutApi.ts',
    'sprintApi.ts', 'startupApi.ts', 'subjectApi.ts', 'teamApi.ts', 'teamWorkspaceApi.ts',
    'trackingApi.ts', 'userApi.ts', 'weeklyTaskApi.ts', 'workshopApi.ts', 'workspaceApi.ts'
  ],
  'assets': [],
  'components': {
    'ai': ['RubricGeneratorTab.tsx', 'SentimentAnalysisTab.tsx', 'SimilarIdeasTab.tsx'],
    'auth': ['ProtectedRoute.tsx'],
    'class': [
      'AddStudentModal.tsx', 'AssignLectureModal.tsx', 'AssignMentorsModal.tsx', 'BulkCreateModal.tsx',
      'ClassDirectionOverview.tsx', 'EditScheduleModal.tsx', 'ImportStudentsModal.tsx', 'RenameClassModal.tsx',
      'ReviewTeamProposalModal.tsx', 'StudentTable.tsx', 'StudentTeamGeneratePanel.tsx', 'TeamGeneratePanel.tsx',
      'TeamList.tsx', 'TeamMemberEditModal.tsx', 'TeamSuggestionTooltip.tsx', 'VerifyMajorModal.tsx'
    ],
    'evaluation': [
      'CommentThread.tsx', 'EvaluationHistoryPage.tsx', 'EvaluationSummary.tsx', 'FeedbackDetailPage.tsx',
      'index.ts', 'PerformanceLevelBadge.tsx', 'RubricScoringForm.tsx'
    ],
    'layout': ['DashboardLayout.tsx', 'Navbar.tsx', 'NotificationDropdown.tsx', 'Sidebar.tsx'],
    'ui': [
      'Badge.tsx', 'Button.tsx', 'Card.tsx', 'ConfirmDialog.tsx', 'EmptyState.tsx', 'ErrorBoundary.tsx',
      'ErrorState.tsx', 'Input.tsx', 'LoadingSkeleton.tsx', 'Modal.tsx', 'PageHeader.tsx', 'ProgressBar.tsx',
      'StatCard.tsx'
    ],
    'workspace': {
      'checkpoints': ['CheckpointCard.tsx', 'CheckpointPanel.tsx', 'CheckpointSection.tsx', 'FeedbackThread.tsx', 'FileUploadZone.tsx'],
      'shortcuts': ['AddShortcutModal.tsx', 'QuickShortcuts.tsx', 'shortcutApi.ts'],
      '.': [
        'EvaluationPanel.tsx', 'InlineCommentUI.tsx', 'KanbanBoard.tsx', 'MentoringPanel.tsx', 'MilestoneModal.tsx',
        'MilestoneTimeline.tsx', 'ProgressSummary.tsx', 'ProjectDirectionCard.tsx', 'RankingTable.tsx', 'RubricForm.tsx',
        'SprintPanel.tsx', 'TaskCard.tsx', 'TaskModal.tsx', 'WeeklyRoadmapPlanner.tsx', 'WorkshopAttendanceManager.tsx',
        'WorkshopCheckInModal.tsx', 'WorkshopForm.tsx', 'WorkshopList.tsx', 'WorkshopPreviewModal.tsx', 'WorkspaceSelector.tsx'
      ]
    }
  },
  'constants': ['classSchedule.ts', 'majors.ts'],
  'context': ['AuthContext.tsx', 'ThemeContext.tsx'],
  'features': {
    'data-bank': ['DataBankPage.tsx'],
    'execution-board': {
      'components': ['BoardColumn.tsx', 'BoardFilters.tsx', 'BoardHeader.tsx', 'BoardSkeleton.tsx', 'BoardSummary.tsx', 'BoardViewToggle.tsx', 'MobileStatusTabs.tsx', 'TaskCard.tsx', 'TaskModal.tsx', 'TaskTableView.tsx'],
      'hooks': ['useDebounce.ts', 'useExecutionBoard.ts'],
      '.': ['boardUtils.ts', 'constants.ts']
    }
  },
  'hooks': ['useAuth.ts', 'usePresence.ts'],
  'lib': ['queryClient.ts'],
  'pages': {
    'admin': ['AdminDashboard.tsx', 'ClassManagement.tsx', 'SubjectManagement.tsx', 'UserManagement.tsx'],
    'auth': ['ForgotPassword.tsx', 'Login.tsx', 'Register.tsx', 'ResetPassword.tsx'],
    'common': ['AIAnalysis.tsx', 'ExecutionBoard.tsx', 'GroupChat.tsx', 'IdeaDetail.tsx', 'MentoringSessions.tsx', 'Rankings.tsx'],
    'lecturer': ['LecturerClasses.tsx', 'LecturerDashboard.tsx'],
    'mentor': ['index.ts', 'MentorDashboard.tsx', 'PastSessionList.tsx', 'ScheduleSessionModal.tsx', 'SessionCalendar.tsx', 'SessionNoteForm.tsx'],
    'shared': ['ClassDetail.tsx', 'Forbidden.tsx', 'NotFound.tsx', 'ProfileSettings.tsx'],
    'student': ['IdeaForm.tsx', 'MyClasses.tsx', 'MyTeam.tsx', 'StudentClassDetail.tsx', 'StudentDashboard.tsx'],
    'workshops': ['Workshops.tsx'],
    'workspace': ['PitchDeckUpload.tsx', 'ProposalEditor.tsx', 'ProposalPreview.tsx', 'StartupWorkspaceHub.tsx', 'TeamWorkspace.tsx', 'VersionHistory.tsx'],
    '.': ['Home.tsx']
  },
  'routes': ['ProtectedRoute.tsx'],
  'utils': ['cn.ts', 'teamDisplay.ts']
};

const stubComponent = (name) => "import React from 'react';\n\nexport const " + name + ": React.FC = () => {\n  return <div>" + name + "</div>;\n};\n";

const stubFile = (name) => {
    if (name.endsWith('.tsx') && name !== 'main.tsx' && !name.includes('App.')) {
        const baseName = name.replace('.tsx', '');
        return stubComponent(baseName);
    }
    return "// TODO: Implement " + name + "\n";
}

function processStructure(basePath, obj) {
  if (Array.isArray(obj)) {
    obj.forEach(file => {
      const filePath = path.join(basePath, file);
      if (!fs.existsSync(filePath)) {
        fs.writeFileSync(filePath, stubFile(file), 'utf8');
        console.log("Created " + filePath);
      }
    });
  } else if (typeof obj === 'object') {
    for (const [key, value] of Object.entries(obj)) {
      if (key === '.') {
        processStructure(basePath, value);
      } else {
        const dirPath = path.join(basePath, key);
        if (!fs.existsSync(dirPath)) {
          fs.mkdirSync(dirPath, { recursive: true });
          console.log("Created directory " + dirPath);
        }
        processStructure(dirPath, value);
      }
    }
  }
}

Object.keys(structure).forEach(dir => {
    const dirPath = path.join(srcDir, dir);
    if (!fs.existsSync(dirPath)) {
        fs.mkdirSync(dirPath, { recursive: true });
    }
});

processStructure(srcDir, structure);

console.log('Scaffolding complete!');
