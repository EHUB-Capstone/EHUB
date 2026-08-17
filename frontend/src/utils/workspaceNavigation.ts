export const WORKSPACE_TABS = ['overview', 'roadmap', 'shortcut'] as const;

export type WorkspaceTab = (typeof WORKSPACE_TABS)[number];

export function resolveWorkspaceTab(search = ''): WorkspaceTab {
  const requestedTab = new URLSearchParams(search).get('tab');
  return WORKSPACE_TABS.includes(requestedTab as WorkspaceTab)
    ? requestedTab as WorkspaceTab
    : 'overview';
}
