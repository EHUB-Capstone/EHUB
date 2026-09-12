import { getAccessToken } from './axiosClient';

interface ProjectDirectionChangedRealtimeEvent {
  eventType: 'ProjectDirectionSubmitted' | 'ProjectDirectionReviewed';
  classId: string;
  teamId: string;
  direction: {
    id: string;
    teamId: string;
    title: string;
    summary: string;
    status: string;
    submittedAtUtc?: string | null;
    reviewedAtUtc?: string | null;
    rowVersion: string;
    startupIndustries?: string[];
    reviews?: Array<{
      id: string;
      fromStatus: string;
      toStatus: string;
      comment: string;
      reviewedByUserId: string;
      occurredAtUtc: string;
    }>;
  };
}

interface ProjectDirectionNotificationReadyRealtimeEvent {
  eventType: 'ProjectDirectionNotificationReady';
  classId: string;
  teamId: string;
}

export type ProjectDirectionRealtimeEvent =
  | ProjectDirectionChangedRealtimeEvent
  | ProjectDirectionNotificationReadyRealtimeEvent;

type EventHandler = (event: ProjectDirectionRealtimeEvent) => void;
type ConnectedHandler = (reconnected: boolean) => void;

const handlers = new Set<EventHandler>();
const connectedHandlers = new Set<ConnectedHandler>();
let socket: WebSocket | null = null;
let reconnectTimer: number | null = null;
let reconnectAttempt = 0;
let hasConnected = false;
let explicitlyStopped = false;
let closeWhenConnected = false;

const websocketUrl = (): string => {
  const url = new URL('/api/realtime/project-directions', window.location.origin);
  url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
  return url.toString();
};

const scheduleReconnect = () => {
  if (explicitlyStopped || handlers.size === 0 || reconnectTimer !== null) return;
  const delay = Math.min(1_000 * 2 ** reconnectAttempt, 15_000);
  reconnectAttempt += 1;
  reconnectTimer = window.setTimeout(() => {
    reconnectTimer = null;
    connect();
  }, delay);
};

const connect = () => {
  if (import.meta.env.VITE_ENABLE_API_MOCKS === 'true' || handlers.size === 0) return;
  if (socket?.readyState === WebSocket.OPEN || socket?.readyState === WebSocket.CONNECTING) return;
  const accessToken = getAccessToken();
  if (!accessToken) {
    scheduleReconnect();
    return;
  }

  explicitlyStopped = false;
  let connection: WebSocket;
  try {
    connection = new WebSocket(websocketUrl(), [
      'ehub-project-directions',
      `ehub-bearer.${accessToken}`,
    ]);
  } catch {
    scheduleReconnect();
    return;
  }
  socket = connection;

  connection.addEventListener('open', () => {
    if (socket !== connection) return;
    if (closeWhenConnected || explicitlyStopped || handlers.size === 0) {
      closeWhenConnected = false;
      socket = null;
      connection.close(1000, 'No active subscribers');
      return;
    }
    const reconnected = hasConnected;
    hasConnected = true;
    reconnectAttempt = 0;
    connectedHandlers.forEach((handler) => handler(reconnected));
  });
  connection.addEventListener('message', (message) => {
    try {
      const event = JSON.parse(String(message.data)) as ProjectDirectionRealtimeEvent;
      if (!event?.eventType || !event.teamId) return;
      if (event.eventType !== 'ProjectDirectionNotificationReady' && !event.direction) return;
      handlers.forEach((handler) => handler(event));
    } catch {
      // Ignore malformed frames and keep the connection alive.
    }
  });
  connection.addEventListener('close', () => {
    if (socket === connection) socket = null;
    scheduleReconnect();
  });
  connection.addEventListener('error', () => connection.close());
};

const stopWhenUnused = () => {
  if (handlers.size > 0) return;
  explicitlyStopped = true;
  if (reconnectTimer !== null) window.clearTimeout(reconnectTimer);
  reconnectTimer = null;
  if (socket?.readyState === WebSocket.CONNECTING) {
    // React StrictMode immediately unsubscribes and resubscribes effects in development.
    // Let the handshake finish instead of closing a CONNECTING socket, which browsers
    // report as a failed WebSocket connection. A new subscriber cancels this close.
    closeWhenConnected = true;
  } else {
    socket?.close(1000, 'No active subscribers');
    socket = null;
  }
  hasConnected = false;
  reconnectAttempt = 0;
};

export const subscribeProjectDirectionRealtime = (
  handler: EventHandler,
  onConnected?: ConnectedHandler,
): (() => void) => {
  handlers.add(handler);
  if (onConnected) connectedHandlers.add(onConnected);
  explicitlyStopped = false;
  closeWhenConnected = false;
  connect();
  if (onConnected && socket?.readyState === WebSocket.OPEN) {
    queueMicrotask(() => {
      if (connectedHandlers.has(onConnected)) onConnected(hasConnected);
    });
  }
  return () => {
    handlers.delete(handler);
    if (onConnected) connectedHandlers.delete(onConnected);
    stopWhenUnused();
  };
};
