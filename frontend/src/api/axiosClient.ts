import axios from 'axios';
import type { AxiosRequestConfig, InternalAxiosRequestConfig } from 'axios';

// ─── Storage keys ───────────────────────────────────────────────────────────
export const TOKEN_KEYS = {
  ACCESS:  'ehub_access_token',
  REFRESH: 'ehub_refresh_token',
} as const;

// ─── Axios instance ──────────────────────────────────────────────────────────
// Vite proxy forwards /api/* → http://localhost:5000
const axiosClient: any = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
  timeout: 15_000,
});

// ─── Request interceptor: attach Bearer token ─────────────────────────────
axiosClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = localStorage.getItem(TOKEN_KEYS.ACCESS);
  if (token && config.headers) {
    config.headers['Authorization'] = `Bearer ${token}`;
  }
  return config;
});

// ─── Flag to prevent infinite refresh loops ──────────────────────────────
let _isRefreshing = false;
let _refreshSubscribers: Array<(token: string) => void> = [];

function subscribeTokenRefresh(cb: (token: string) => void) {
  _refreshSubscribers.push(cb);
}

function onRefreshed(token: string) {
  _refreshSubscribers.forEach(cb => cb(token));
  _refreshSubscribers = [];
}

function clearAuth() {
  localStorage.removeItem(TOKEN_KEYS.ACCESS);
  localStorage.removeItem(TOKEN_KEYS.REFRESH);
}

// ─── Response interceptor: auto-refresh on 401 ───────────────────────────
axiosClient.interceptors.response.use(
  // The WDP feature modules expect the response body, rather than Axios's
  // wrapper object. Auth calls below still consume the C# ApiResponse shape.
  response => response.data,
  async error => {
    const originalRequest = error.config as AxiosRequestConfig & { _retry?: boolean };

    // Do not intercept 401 for auth endpoints (prevents page reload loop on login failure)
    const isAuthEndpoint = originalRequest.url?.includes('/auth/login') ||
                           originalRequest.url?.includes('/auth/google') ||
                           originalRequest.url?.includes('/auth/register') ||
                           originalRequest.url?.includes('/auth/refresh-token');

    // Only intercept 401 and only retry once
    if (error.response?.status === 401 && !originalRequest._retry && !isAuthEndpoint) {
      const refreshToken = localStorage.getItem(TOKEN_KEYS.REFRESH);

      if (!refreshToken) {
        clearAuth();
        window.location.href = '/login';
        return Promise.reject(error);
      }

      if (_isRefreshing) {
        // Queue requests while refreshing
        return new Promise(resolve => {
          subscribeTokenRefresh(token => {
            if (originalRequest.headers) {
              (originalRequest.headers as Record<string, string>)['Authorization'] = `Bearer ${token}`;
            }
            resolve(axiosClient(originalRequest));
          });
        });
      }

      originalRequest._retry = true;
      _isRefreshing = true;

      try {
        const { data } = await axios.post('/api/auth/refresh-token', { refreshToken });
        const newAccess: string  = data.data.accessToken;
        const newRefresh: string = data.data.refreshToken;

        localStorage.setItem(TOKEN_KEYS.ACCESS,  newAccess);
        localStorage.setItem(TOKEN_KEYS.REFRESH, newRefresh);

        onRefreshed(newAccess);
        _isRefreshing = false;

        if (originalRequest.headers) {
          (originalRequest.headers as Record<string, string>)['Authorization'] = `Bearer ${newAccess}`;
        }
        return axiosClient(originalRequest);
      } catch {
        _isRefreshing = false;
        clearAuth();
        window.location.href = '/login';
        return Promise.reject(error);
      }
    }

    return Promise.reject(error);
  },
);

export default axiosClient;
