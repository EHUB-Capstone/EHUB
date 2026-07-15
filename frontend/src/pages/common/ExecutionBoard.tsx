// @ts-nocheck
import { useCallback, useMemo, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  closestCorners,
  DndContext,
  DragOverlay,
  KeyboardSensor,
  PointerSensor,
  TouchSensor,
  useSensor,
  useSensors,
} from '@dnd-kit/core';
import { sortableKeyboardCoordinates } from '@dnd-kit/sortable';
import { AlertTriangle, CheckSquare, RotateCcw } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';
import { teamWorkspaceApi } from '../../api/teamWorkspaceApi';
import EmptyState from '../../components/ui/EmptyState';
import ConfirmDialog from '../../components/ui/ConfirmDialog';
import BoardColumn from '../../features/execution-board/components/BoardColumn';
import BoardFilters from '../../features/execution-board/components/BoardFilters';
import BoardHeader from '../../features/execution-board/components/BoardHeader';
import BoardSkeleton from '../../features/execution-board/components/BoardSkeleton';
import BoardSummary from '../../features/execution-board/components/BoardSummary';
import BoardViewToggle from '../../features/execution-board/components/BoardViewToggle';
import MobileStatusTabs from '../../features/execution-board/components/MobileStatusTabs';
import TaskCard from '../../features/execution-board/components/TaskCard';
import TaskTableView from '../../features/execution-board/components/TaskTableView';
import TaskModal from '../../features/execution-board/components/TaskModal';
import { EMPTY_GROUPED, STATUSES } from '../../features/execution-board/constants';
import { getTaskStatus, normalizeFilters } from '../../features/execution-board/boardUtils';
import { useDebounce } from '../../features/execution-board/hooks/useDebounce';
import {
  useTaskBoard,
  useTaskMutations,
  useTeamContext,
  useTeamMembers,
} from '../../features/execution-board/hooks/useExecutionBoard';

const DEFAULT_FILTERS = {
  week: 'ALL',
  assignee: 'ALL',
  priority: 'ALL',
  search: '',
};

const getInitialView = () => {
  if (typeof window === 'undefined') return 'kanban';
  const stored = window.localStorage.getItem('executionBoardView');
  return stored === 'table' || stored === 'kanban' ? stored : 'kanban';
};

export default function ExecutionBoard() {
  const { user } = useAuth();
  const location = useLocation();
  const queryTeamId = useMemo(() => new URLSearchParams(location.search).get('teamId'), [location.search]);

  const [filters, setFilters] = useState(DEFAULT_FILTERS);
  const [activeMobileStatus, setActiveMobileStatus] = useState('TODO');
  const [modalOpen, setModalOpen] = useState(false);
  const [editingTask, setEditingTask] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [activeTask, setActiveTask] = useState(null);
  const [activeOverStatus, setActiveOverStatus] = useState(null);
  const [view, setView] = useState(getInitialView);

  const debouncedSearch = useDebounce(filters.search, 180);
  const queryFilters = useMemo(() => ({
    ...filters,
    search: debouncedSearch,
  }), [filters, debouncedSearch]);

  const role = user?.role?.toUpperCase() || '';
  const isPrivileged = role === 'ADMIN' || role === 'LECTURER' || role === 'MENTOR';

  const teamContextQuery = useTeamContext({ user, queryTeamId });
  const teamId = teamContextQuery.data || null;
  const workspaceContextQuery = useQuery({
    queryKey: ['execution-board', 'workspace-context', teamId],
    enabled: Boolean(teamId),
    staleTime: 60_000,
    queryFn: async () => {
      const response = await teamWorkspaceApi.getWorkspaceContext(teamId);
      return response.data || response;
    },
  });
  const teamMembersQuery = useTeamMembers(teamId);
  const teamMembers = teamMembersQuery.data || [];
  const boardQuery = useTaskBoard({ teamId, filters: queryFilters });

  const board = boardQuery.data || { tasks: [], grouped: EMPTY_GROUPED, summary: null };
  const boardParams = useMemo(() => normalizeFilters(queryFilters), [queryFilters]);
  const taskById = useMemo(() => {
    const map = new Map();
    board.tasks.forEach((task) => map.set(task._id, task));
    return map;
  }, [board.tasks]);
  const taskStatusById = useMemo(() => {
    const map = new Map();
    STATUSES.forEach((status) => {
      (board.grouped?.[status] || []).forEach((task) => map.set(task._id, status));
    });
    return map;
  }, [board.grouped]);
  const boardKey = useMemo(
    () => ['execution-board', 'task-board', teamId, boardParams],
    [teamId, boardParams]
  );
  const tableTasks = useMemo(() => (
    STATUSES.flatMap((status) => board.grouped?.[status] || [])
  ), [board.grouped]);

  const closeModal = useCallback(() => {
    setModalOpen(false);
    setEditingTask(null);
  }, []);

  const mutations = useTaskMutations({
    boardKey,
    teamId,
    filters: queryFilters,
    onCloseModal: closeModal,
  });

  const isTeamMemberContext = Boolean(teamId) && (role === 'STUDENT' || role === 'USER');
  const accessMode = workspaceContextQuery.data?.accessMode || workspaceContextQuery.data?.selectedWorkspace?.accessMode || null;
  const isReadOnly = accessMode === 'READ_ONLY';
  const canCreateTeamTask = !isReadOnly && (isPrivileged || isTeamMemberContext);
  const canUpdateStatus = !isReadOnly && (isPrivileged || isTeamMemberContext);

  const permissions = useMemo(() => ({
    canUpdateStatus,
    canEditTask: (task) => {
      if (isReadOnly) return false;
      if (isPrivileged) return true;
      if (!isTeamMemberContext) return false;
      const createdById = String(task?.createdBy?._id || task?.createdBy || '');
      return createdById === String(user?._id || '');
    },
    canDeleteTask: (task) => {
      if (isReadOnly) return false;
      if (isPrivileged) return true;
      if (!isTeamMemberContext) return false;
      const createdById = String(task?.createdBy?._id || task?.createdBy || '');
      return createdById === String(user?._id || '');
    },
  }), [canUpdateStatus, isPrivileged, isReadOnly, isTeamMemberContext, user?._id]);

  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: { distance: 6 },
    }),
    useSensor(TouchSensor, {
      activationConstraint: { delay: 120, tolerance: 8 },
    }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    })
  );

  const updateFilters = useCallback((patch) => {
    setFilters((current) => ({ ...current, ...patch }));
  }, []);

  const clearFilters = useCallback(() => {
    setFilters(DEFAULT_FILTERS);
  }, []);

  const handleViewChange = useCallback((nextView) => {
    setView(nextView);
    window.localStorage.setItem('executionBoardView', nextView);
  }, []);

  const handleCreate = useCallback(() => {
    setEditingTask(null);
    setModalOpen(true);
  }, []);

  const handleEdit = useCallback((task) => {
    setEditingTask(task);
    setModalOpen(true);
  }, []);

  const handleSave = useCallback((payload) => {
    if (isReadOnly) return;
    mutations.saveTask.mutate({ task: editingTask, payload });
  }, [editingTask, isReadOnly, mutations.saveTask]);

  const handleStatusChange = useCallback((taskId, status) => {
    if (isReadOnly) return;
    mutations.changeStatus.mutate({ taskId, status });
  }, [isReadOnly, mutations.changeStatus]);

  const handleSwipeStatusChange = useCallback((taskId, status) => {
    if (isReadOnly) return;
    setActiveMobileStatus(status);
    mutations.changeStatus.mutate({ taskId, status });
  }, [isReadOnly, mutations.changeStatus]);

  const getStatusFromOver = useCallback((over) => {
    if (!over?.id) return null;
    const overId = String(over.id);
    if (overId.startsWith('column-tab-')) return overId.replace('column-tab-', '');
    if (overId.startsWith('column-')) return overId.replace('column-', '');
    return taskStatusById.get(over.id) || null;
  }, [taskStatusById]);

  const handleDragStart = useCallback((event) => {
    const task = event.active.data.current?.task || taskById.get(event.active.id);
    setActiveTask(task || null);
    setActiveOverStatus(task ? getTaskStatus(task) : null);
  }, [taskById]);

  const handleDragOver = useCallback((event) => {
    setActiveOverStatus(getStatusFromOver(event.over));
  }, [getStatusFromOver]);

  const handleDragEnd = useCallback((event) => {
    if (isReadOnly) {
      setActiveTask(null);
      setActiveOverStatus(null);
      return;
    }
    const taskId = event.active.id;
    const nextStatus = getStatusFromOver(event.over);
    const currentStatus = taskStatusById.get(taskId);

    setActiveTask(null);
    setActiveOverStatus(null);

    if (!nextStatus || !currentStatus || nextStatus === currentStatus) return;
    setActiveMobileStatus(nextStatus);
    mutations.changeStatus.mutate({ taskId, status: nextStatus });
  }, [getStatusFromOver, isReadOnly, mutations.changeStatus, taskStatusById]);

  const handleDragCancel = useCallback(() => {
    setActiveTask(null);
    setActiveOverStatus(null);
  }, []);

  const handleDeleteConfirm = useCallback(() => {
    if (!deleteTarget || isReadOnly) return;
    mutations.removeTask.mutate(deleteTarget, {
      onSettled: () => setDeleteTarget(null),
    });
  }, [deleteTarget, isReadOnly, mutations.removeTask]);

  const initialLoading = teamContextQuery.isLoading || (Boolean(teamId) && (boardQuery.isLoading || workspaceContextQuery.isLoading));

  if (initialLoading) {
    return <BoardSkeleton />;
  }

  if (!teamId) {
    return (
      <EmptyState
        icon={CheckSquare}
        title={role === 'STUDENT' ? 'No Team Assigned' : 'Team Workspace Required'}
        description={role === 'STUDENT'
          ? 'You need to be part of a team to view the execution board.'
          : 'Open a specific Team Workspace or pass a teamId to view this board.'}
      />
    );
  }

  if (boardQuery.isError) {
    return (
      <div className="rounded-xl border border-red-200 bg-red-50 p-8 text-center">
        <AlertTriangle className="mx-auto mb-3 h-8 w-8 text-red-400" />
        <p className="font-semibold text-red-700">{boardQuery.error?.message || 'Failed to load execution board'}</p>
        <button
          type="button"
          onClick={() => boardQuery.refetch()}
          className="mt-4 inline-flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-semibold text-red-700 transition-colors hover:bg-red-100"
        >
          <RotateCcw className="h-4 w-4" />
          Retry
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
        <BoardHeader canCreate={canCreateTeamTask} onCreate={handleCreate} />
        {isReadOnly && (
          <div className="mt-4 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm font-medium text-amber-800">
            This execution board is read-only because you are viewing an archived workspace.
          </div>
        )}
        <div className="mt-5 flex items-center justify-between gap-3 border-t border-slate-100 pt-5">
          <div>
            <p className="text-sm font-semibold text-slate-800">
              {view === 'kanban' ? 'Kanban View' : 'Table View'}
            </p>
            <p className="text-xs text-slate-500">
              {view === 'kanban'
                ? 'Drag tasks across statuses for execution.'
                : 'Review task details, deadlines, progress, and notes.'}
            </p>
          </div>
          <BoardViewToggle view={view} onChange={handleViewChange} />
        </div>
        <BoardSummary summary={board.summary} />
        <BoardFilters
          filters={filters}
          onChange={updateFilters}
          onClear={clearFilters}
          teamMembers={teamMembers}
          isFetching={boardQuery.isFetching || teamMembersQuery.isFetching}
        />
      </section>

      {view === 'kanban' ? (
        <DndContext
          sensors={sensors}
          collisionDetection={closestCorners}
          onDragStart={handleDragStart}
          onDragOver={handleDragOver}
          onDragEnd={handleDragEnd}
          onDragCancel={handleDragCancel}
        >
          <MobileStatusTabs
            activeStatus={activeMobileStatus}
            onChange={setActiveMobileStatus}
            grouped={board.grouped}
          />

          <div className="grid grid-cols-1 gap-4 md:hidden">
            <BoardColumn
              status={activeMobileStatus}
              tasks={board.grouped?.[activeMobileStatus] || []}
              permissions={permissions}
              onEditTask={handleEdit}
              onDeleteTask={setDeleteTarget}
              onStatusChange={handleStatusChange}
              onSwipeStatusChange={handleSwipeStatusChange}
              enableSwipe
              activeOverStatus={activeOverStatus}
              />
          </div>

          <div className="hidden gap-4 md:grid md:grid-cols-5">
            {STATUSES.map((status) => (
              <BoardColumn
                key={status}
                status={status}
                tasks={board.grouped?.[status] || []}
                permissions={permissions}
                onEditTask={handleEdit}
                onDeleteTask={setDeleteTarget}
                onStatusChange={handleStatusChange}
                activeOverStatus={activeOverStatus}
              />
            ))}
          </div>

          <DragOverlay dropAnimation={{ duration: 180, easing: 'cubic-bezier(0.2, 0, 0, 1)' }}>
            {activeTask ? (
              <div className="w-[300px]">
                <TaskCard
                  task={activeTask}
                  canEdit={false}
                  canDelete={false}
                  canUpdateStatus={false}
                  onEdit={() => {}}
                  onDelete={() => {}}
                  onStatusChange={() => {}}
                  isOverlay
                />
              </div>
            ) : null}
          </DragOverlay>
        </DndContext>
      ) : (
        <TaskTableView
          tasks={tableTasks}
          permissions={permissions}
          onEditTask={handleEdit}
          onDeleteTask={setDeleteTarget}
          onStatusChange={handleStatusChange}
        />
      )}

      <TaskModal
        isOpen={modalOpen}
        onClose={closeModal}
        onSave={handleSave}
        task={editingTask}
        teamMembers={teamMembers}
        loading={mutations.saveTask.isPending}
      />

      <ConfirmDialog
        isOpen={Boolean(deleteTarget)}
        onClose={() => setDeleteTarget(null)}
        onConfirm={handleDeleteConfirm}
        title="Delete Task"
        description={
          deleteTarget?.title
            ? `Are you sure you want to delete "${deleteTarget.title}"? This action cannot be undone.`
            : 'Are you sure you want to delete this task? This action cannot be undone.'
        }
        confirmText="Delete Task"
        isSubmitting={mutations.removeTask.isPending}
      />
    </div>
  );
}
