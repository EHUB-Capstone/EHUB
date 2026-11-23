// @ts-nocheck
import React, { useState, useEffect, useCallback } from 'react';
import {
  getTeamMilestones,
  createMilestone,
  updateMilestone,
  deleteMilestone,
  getTeamTasks,
  createTask,
  updateTask,
  updateTaskStatus,
  deleteTask,
  getTeamProgress,
} from '../../api/sprintApi';
import { useAuth } from '../../hooks/useAuth';
import toast from 'react-hot-toast';
import { Plus, Loader2 } from 'lucide-react';
import ProgressSummary from './ProgressSummary';
import MilestoneTimeline from './MilestoneTimeline';
import KanbanBoard from './KanbanBoard';
import TaskModal from './TaskModal';
import MilestoneModal from './MilestoneModal';
import { useTranslation } from 'react-i18next';

export default function SprintPanel({ teamId, members = [], isEditable = false }) {
  const { t } = useTranslation('workspace');
  const { user } = useAuth();

  // ── Data state ───────────────────────────────────────────
  const [milestones, setMilestones] = useState([]);
  const [tasks, setTasks] = useState([]);
  const [progress, setProgress] = useState(null);

  // ── UI state ─────────────────────────────────────────────
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [actionLoading, setActionLoading] = useState(false);

  // ── Modal state ──────────────────────────────────────────
  const [taskModalOpen, setTaskModalOpen] = useState(false);
  const [milestoneModalOpen, setMilestoneModalOpen] = useState(false);
  const [editingTask, setEditingTask] = useState(null);
  const [editingMilestone, setEditingMilestone] = useState(null);

  // ── Data fetching ────────────────────────────────────────
  const fetchData = useCallback(async () => {
    if (!teamId) return;
    try {
      setError(null);
      const [milestonesRes, tasksRes, progressRes] = await Promise.all([
        getTeamMilestones(teamId),
        getTeamTasks(teamId),
        getTeamProgress(teamId),
      ]);
      setMilestones(milestonesRes?.data?.milestones || milestonesRes?.data || []);
      setTasks(tasksRes?.data?.tasks || tasksRes?.data || []);
      setProgress(progressRes?.data || progressRes || null);
    } catch (err) {
      const msg = err?.response?.data?.error || 'Failed to load sprint data';
      setError(msg);
      toast.error(msg);
    } finally {
      setLoading(false);
    }
  }, [teamId]);

  useEffect(() => {
    setLoading(true);
    fetchData();
  }, [fetchData]);

  // ── Milestone handlers ───────────────────────────────────
  const handleCreateMilestone = () => {
    setEditingMilestone(null);
    setMilestoneModalOpen(true);
  };

  const handleEditMilestone = (milestone) => {
    setEditingMilestone(milestone);
    setMilestoneModalOpen(true);
  };

  const handleMilestoneSave = async (data) => {
    try {
      setActionLoading(true);
      if (editingMilestone) {
        await updateMilestone(editingMilestone._id, data);
        toast.success(t('sprintPanel.milestoneUpdated'));
      } else {
        await createMilestone(teamId, data);
        toast.success(t('sprintPanel.milestoneCreated'));
      }
      setMilestoneModalOpen(false);
      setEditingMilestone(null);
      await fetchData();
    } catch (err) {
      toast.error(err?.response?.data?.error || t('milestoneModal.failedToSaveMilestone'));
    } finally {
      setActionLoading(false);
    }
  };

  const handleDeleteMilestone = async (milestoneId) => {
    if (!milestoneId) return;
    if (!window.confirm(t('sprintPanel.areYouSureYouWantTo'))) return;
    try {
      setActionLoading(true);
      await deleteMilestone(milestoneId);
      toast.success(t('sprintPanel.milestoneDeleted'));
      await fetchData();
    } catch (err) {
      toast.error(err?.response?.data?.error || t('sprintPanel.failedToDeleteMilestone'));
    } finally {
      setActionLoading(false);
    }
  };

  // ── Task handlers ────────────────────────────────────────
  const handleCreateTask = () => {
    setEditingTask(null);
    setTaskModalOpen(true);
  };

  const handleEditTask = (task) => {
    setEditingTask(task);
    setTaskModalOpen(true);
  };

  const handleTaskSave = async (data) => {
    try {
      setActionLoading(true);
      if (editingTask) {
        await updateTask(editingTask._id, data);
        toast.success(t('sprintPanel.taskUpdated'));
      } else {
        await createTask(teamId, data);
        toast.success(t('sprintPanel.taskCreated'));
      }
      setTaskModalOpen(false);
      setEditingTask(null);
      await fetchData();
    } catch (err) {
      toast.error(err?.response?.data?.error || t('sprintPanel.failedToSaveTask'));
    } finally {
      setActionLoading(false);
    }
  };

  const handleDeleteTask = async (taskId) => {
    if (!taskId) return;
    if (!window.confirm(t('sprintPanel.areYouSureYouWantTo2'))) return;
    try {
      setActionLoading(true);
      await deleteTask(taskId);
      toast.success(t('sprintPanel.taskDeleted'));
      await fetchData();
    } catch (err) {
      toast.error(err?.response?.data?.error || t('sprintPanel.failedToDeleteTask'));
    } finally {
      setActionLoading(false);
    }
  };

  const handleStatusChange = async (taskId, newStatus) => {
    if (!taskId || !newStatus) return;
    try {
      setActionLoading(true);
      await updateTaskStatus(taskId, { status: newStatus });
      toast.success(t('sprintPanel.taskStatusUpdated'));
      await fetchData();
    } catch (err) {
      toast.error(err?.response?.data?.error || t('sprintPanel.failedToUpdateTaskStatus'));
    } finally {
      setActionLoading(false);
    }
  };

  // ── Loading state ────────────────────────────────────────
  if (loading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="w-6 h-6 animate-spin text-blue-500 mr-2" />
        <span className="text-gray-500 text-sm">{t('sprintPanel.loadingSprintData')}</span>
      </div>
    );
  }

  // ── Error state ──────────────────────────────────────────
  if (error) {
    return (
      <div className="text-center p-8 bg-red-50 rounded-lg border border-red-200">
        <p className="text-red-600 text-sm font-medium mb-2">{error}</p>
        <button
          onClick={() => { setLoading(true); fetchData(); }}
          className="text-sm text-red-600 hover:text-red-800 font-medium underline"
        >
          {t('startupWorkspaceHub.tryAgain')}
        </button>
      </div>
    );
  }

  // ── Render ───────────────────────────────────────────────
  return (
    <div className="space-y-8">
      {/* Progress Summary */}
      <ProgressSummary progress={progress} milestones={milestones} tasks={tasks} />

      {/* Milestones Section */}
      <section>
        <div className="flex items-center justify-between mb-4">
          <div>
            <h2 className="text-lg font-medium text-gray-900">{t('sprintPanel.milestones')}</h2>
            <p className="text-sm text-gray-500">{t('sprintPanel.trackProjectMilestonesAndDeadlines')}</p>
          </div>
          {isEditable && (
            <button
              onClick={handleCreateMilestone}
              disabled={actionLoading}
              className="inline-flex items-center gap-1.5 px-4 py-2 bg-blue-600 text-white rounded-md text-sm font-medium hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              <Plus className="w-4 h-4" />
              {t('sprintPanel.addMilestone')}
            </button>
          )}
        </div>
        <MilestoneTimeline
          milestones={milestones}
          onEdit={handleEditMilestone}
          onDelete={handleDeleteMilestone}
          isEditable={isEditable}
        />
      </section>

      {/* Kanban Board Section */}
      <section>
        <div className="flex items-center justify-between mb-4">
          <div>
            <h2 className="text-lg font-medium text-gray-900">{t('sprintPanel.taskBoard')}</h2>
            <p className="text-sm text-gray-500">{t('sprintPanel.manageAndTrackTeamTasks')}</p>
          </div>
          {isEditable && (
            <button
              onClick={handleCreateTask}
              disabled={actionLoading}
              className="inline-flex items-center gap-1.5 px-4 py-2 bg-blue-600 text-white rounded-md text-sm font-medium hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              <Plus className="w-4 h-4" />
              {t('boardHeader.addTask')}
            </button>
          )}
        </div>
        <KanbanBoard
          tasks={tasks}
          onStatusChange={handleStatusChange}
          onEditTask={handleEditTask}
          isEditable={isEditable}
          members={members}
        />
      </section>

      {/* Milestone Modal */}
      {milestoneModalOpen && (
        <MilestoneModal
          isOpen={milestoneModalOpen}
          onClose={() => { setMilestoneModalOpen(false); setEditingMilestone(null); }}
          onSave={handleMilestoneSave}
          milestone={editingMilestone}
          loading={actionLoading}
        />
      )}

      {/* Task Modal */}
      {taskModalOpen && (
        <TaskModal
          isOpen={taskModalOpen}
          onClose={() => { setTaskModalOpen(false); setEditingTask(null); }}
          onSave={handleTaskSave}
          onDelete={handleDeleteTask}
          task={editingTask}
          milestones={milestones}
          members={members}
          loading={actionLoading}
        />
      )}
    </div>
  );
}
