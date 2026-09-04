import MockAdapter from 'axios-mock-adapter';
import axiosClient from '../api/axiosClient.ts';
import { registerClassMockHandlers } from './handlers/classMockHandlers.ts';
import { registerCoreMockHandlers } from './handlers/coreMockHandlers.ts';
import { registerTeamMockHandlers } from './handlers/teamMockHandlers.ts';
import { registerWorkspaceMockHandlers } from './handlers/workspaceMockHandlers.ts';
import { resetMockState } from './mockHelpers.ts';

declare global {
  interface Window {
    __EHUB_MOCK_API__?: {
      reset: () => void;
    };
  }
}

let activeMock: MockAdapter | null = null;

export function enableApiMocks(): void {
  if (activeMock) return;

  activeMock = new MockAdapter(axiosClient, {
    delayResponse: 250,
    onNoMatch: 'passthrough',
  });

  registerCoreMockHandlers(activeMock);
  registerClassMockHandlers(activeMock);
  registerTeamMockHandlers(activeMock);
  registerWorkspaceMockHandlers(activeMock);

  if (typeof window !== 'undefined') {
    window.__EHUB_MOCK_API__ = {
      reset: () => {
        resetMockState();
        window.location.reload();
      },
    };
  }

  console.info('[E-HUB] Frontend mock API enabled. Run window.__EHUB_MOCK_API__.reset() to restore fixtures.');
}
