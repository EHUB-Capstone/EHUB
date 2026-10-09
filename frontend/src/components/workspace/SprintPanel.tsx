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

export default function SprintPanel({ teamId, members = [], isEditable = false }) {
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
        toast.success('Milestone updated');
      } else {
        await createMilestone(teamId, data);
        toast.success('Milestone created');
      }
      setMilestoneModalOpen(false);
      setEditingMilestone(null);
      await fetchData();
    } catch (err) {
      toast.error(err?.response?.data?.error || 'Failed to save milestone');
    } finally {
      setActionLoading(false);
    }
  };

  const handleDeleteMilestone = async (milestoneId) => {
    if (!milestoneId) return;
    if (!window.confirm('Are you sure you want to delete this milestone?')) return;
    try {
      setActionLoading(true);
      await deleteMilestone(milestoneId);
      toast.success('Milestone deleted');
      await fetchData();
    } catch (err) {
      toast.error(err?.response?.data?.error || 'Failed to delete milestone');
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
        toast.success('Task updated');
      } else {
        await createTask(teamId, data);
        toast.success('Task created');
      }
      setTaskModalOpen(false);
      setEditingTask(null);
      await fetchData();
    } catch (err) {
      toast.error(err?.response?.data?.error || 'Failed to save task');
    } finally {
      setActionLoading(false);
    }
  };

  const handleDeleteTask = async (taskId) => {
    if (!taskId) return;
    if (!window.confirm('Are you sure you want to delete this task?')) return;
    try {
      setActionLoading(true);
      await deleteTask(taskId);
      toast.success('Task deleted');
      await fetchData();
    } catch (err) {
      toast.error(err?.response?.data?.error || 'Failed to delete task');
    } finally {
      setActionLoading(false);
    }
  };

  const handleStatusChange = async (taskId, newStatus) => {
    if (!taskId || !newStatus) return;
    try {
      setActionLoading(true);
      await updateTaskStatus(taskId, { status: newStatus });
      toast.success('Task status updated');
      await fetchData();
    } catch (err) {
      toast.error(err?.response?.data?.error || 'Failed to update task status');
    } finally {
      setActionLoading(false);
    }
  };

  // ── Loading state ────────────────────────────────────────
  if (loading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="w-6 h-6 animate-spin text-blue-500 mr-2" />
        <span className="text-gray-500 text-sm">Loading sprint data…</span>
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
          Try again
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
            <h2 className="text-lg font-medium text-gray-900">Milestones</h2>
            <p className="text-sm text-gray-500">Track project milestones and deadlines.</p>
          </div>
          {isEditable && (
            <button
              onClick={handleCreateMilestone}
              disabled={actionLoading}
              className="inline-flex items-center gap-1.5 px-4 py-2 bg-blue-600 text-white rounded-md text-sm font-medium hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              <Plus className="w-4 h-4" />
              Add Milestone
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
            <h2 className="text-lg font-medium text-gray-900">Task Board</h2>
            <p className="text-sm text-gray-500">Manage and track team tasks.</p>
          </div>
          {isEditable && (
            <button
              onClick={handleCreateTask}
              disabled={actionLoading}
              className="inline-flex items-center gap-1.5 px-4 py-2 bg-blue-600 text-white rounded-md text-sm font-medium hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              <Plus className="w-4 h-4" />
              Add Task
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
