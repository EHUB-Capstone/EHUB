import axios from 'axios';
import type { AxiosRequestConfig, InternalAxiosRequestConfig } from 'axios';

// ─── Storage keys (legacy cleanup) ───────────────────────────────────────────
export const TOKEN_KEYS = {
  ACCESS:  'ehub_access_token',
  REFRESH: 'ehub_refresh_token',
} as const;

// ─── In-memory access token storage ──────────────────────────────────────────
let accessToken: string | null = null;

export const setAccessToken = (token: string | null) => {
  accessToken = token;
};

export const getAccessToken = () => accessToken;

// ─── Axios instance ──────────────────────────────────────────────────────────
// withCredentials: true allows sending and receiving cookies cross-origin
const axiosClient: any = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
  timeout: 15_000,
  withCredentials: true,
});

// ─── Request interceptor: attach Bearer token from memory ─────────────────────
axiosClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  if (accessToken && config.headers) {
    config.headers['Authorization'] = `Bearer ${accessToken}`;
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
  setAccessToken(null);
  // Clear legacy tokens if any
  localStorage.removeItem(TOKEN_KEYS.ACCESS);
  localStorage.removeItem(TOKEN_KEYS.REFRESH);
}

// ─── Response interceptor: auto-refresh on 401 ───────────────────────────
axiosClient.interceptors.response.use(
  response => response.data,
  async error => {
    const originalRequest = (error.config ?? {}) as AxiosRequestConfig & { _retry?: boolean };

    // Do not intercept 401 for auth endpoints
    const isAuthEndpoint = originalRequest.url?.includes('/auth/login') ||
                           originalRequest.url?.includes('/auth/google') ||
                           originalRequest.url?.includes('/auth/register') ||
                           originalRequest.url?.includes('/auth/forgot-password') ||
                           originalRequest.url?.includes('/auth/reset-password') ||
                           originalRequest.url?.includes('/auth/refresh-token') ||
                           originalRequest.url?.includes('/auth/logout');

    if (error.response?.status === 401 && !originalRequest._retry && !isAuthEndpoint) {
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
        // Call refresh-token endpoint without body, browser automatically forwards the cookie
        const { data } = await axios.post('/api/auth/refresh-token', null, { withCredentials: true });
        const newAccess: string = data.data.accessToken;

        setAccessToken(newAccess);
        onRefreshed(newAccess);
        _isRefreshing = false;

        if (originalRequest.headers) {
          (originalRequest.headers as Record<string, string>)['Authorization'] = `Bearer ${newAccess}`;
        }
        return axiosClient(originalRequest);
      } catch (refreshError) {
        _isRefreshing = false;
        clearAuth();
        window.location.href = '/login';
        return Promise.reject(refreshError);
      }
    }

    return Promise.reject(error);
  },
);

export default axiosClient;
