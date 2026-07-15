// @ts-nocheck
// src/hooks/usePresence.js
import { useEffect, useRef, useContext } from 'react';
import io from 'socket.io-client';
import { AuthContext } from '../context/AuthContext';

const SOCKET_URL = (import.meta.env.VITE_API_URL || 'http://localhost:5000').replace(/\/api$/, '');

/**
 * Hook kết nối socket.io và emit 'user_online' khi user đã đăng nhập.
 * Tự động disconnect khi unmount hoặc logout.
 * Dùng ở App level để mọi trang đều báo presence.
 */
export function usePresence() {
  const { user } = useContext(AuthContext);
  const socketRef = useRef(null);

  useEffect(() => {
    const userId = user?._id || user?.id;
    if (!userId) {
      // Nếu chưa login, disconnect socket cũ nếu có
      if (socketRef.current) {
        socketRef.current.disconnect();
        socketRef.current = null;
      }
      return;
    }

    // Tạo socket connection
    const socket = io(SOCKET_URL, {
      withCredentials: true,
      transports: ['websocket', 'polling'],
    });
    socketRef.current = socket;

    socket.on('connect', () => {
      socket.emit('user_online', userId);
    });

    // Cleanup khi unmount hoặc user thay đổi
    return () => {
      socket.disconnect();
      socketRef.current = null;
    };
  }, [user?._id, user?.id]);

  return socketRef;
}
