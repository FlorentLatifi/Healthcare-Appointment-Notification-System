import axios from 'axios';
import toast from 'react-hot-toast';
import { parseApiError } from '../hooks/useApiError';
import { formatRateLimitMessage, notifySessionExpired } from '../utils/sessionExpiry';

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true,
});

/** Paths that render 429 inline — avoid double toast + banner. */
const RATE_LIMIT_INLINE_PATHS = [
  '/Auth/login',
  '/Auth/register',
  '/Auth/forgot-password',
  '/Auth/reset-password',
];

let _getToken = null;
let _setToken = null;
/** Optional: apply full login/refresh payload (token + role + profile ids). */
let _applySession = null;
let _onAuthCleared = null;
let _isRefreshing = false;
let _failedQueue = [];

export function setTokenGetter(fn) {
  _getToken = fn;
}

export function setTokenSetter(fn) {
  _setToken = fn;
}

/**
 * Register a callback that receives the full refresh/login data payload
 * so role / patientId / doctorId stay in sync after silent refresh.
 */
export function setSessionApplier(fn) {
  _applySession = fn;
}

export function onAuthCleared(fn) {
  _onAuthCleared = fn;
}

function processQueue(error, token = null) {
  _failedQueue.forEach(({ resolve, reject }) => {
    if (error) {
      reject(error);
    } else {
      resolve(token);
    }
  });
  _failedQueue = [];
}

/** Attach normalized validation structure on every failed response. */
function attachApiError(error) {
  if (!error || error.apiError) return error;
  try {
    const parsed = parseApiError(error);
    error.apiError = parsed;
    error.isRateLimited = !!parsed.isRateLimited || error.response?.status === 429;
    error.retryAfterSeconds = parsed.retryAfterSeconds ?? null;
    error.isValidationError = !!(
      parsed.hasFieldErrors ||
      (error.response?.status === 400 || error.response?.status === 422) &&
        Object.keys(parsed.fieldErrors || {}).length > 0
    );
  } catch {
    // never break the interceptor
  }
  return error;
}

function shouldToastRateLimit(config) {
  const url = config?.url || '';
  return !RATE_LIMIT_INLINE_PATHS.some((p) => url.includes(p));
}

apiClient.interceptors.request.use((config) => {
  const token = typeof _getToken === 'function' ? _getToken() : null;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    attachApiError(error);

    const originalRequest = error.config;

    // Friendly rate-limit feedback for background calls (refresh, lists).
    // Auth forms show the same message inline via parseApiError / generalError.
    if (
      error.response?.status === 429
      && originalRequest
      && !originalRequest._rateLimitToasted
      && shouldToastRateLimit(originalRequest)
    ) {
      originalRequest._rateLimitToasted = true;
      const secs = error.retryAfterSeconds ?? error.apiError?.retryAfterSeconds ?? null;
      const msg = formatRateLimitMessage(secs, error.apiError?.generalError);
      const duration = secs != null
        ? Math.min(12000, Math.max(4000, (Number(secs) + 2) * 1000))
        : 6000;
      toast.error(msg, { id: 'rate-limit', duration });
    }

    if (
      !originalRequest ||
      originalRequest._retry ||
      error.response?.status !== 401 ||
      originalRequest.url === '/Auth/login' ||
      originalRequest.url === '/Auth/refresh'
    ) {
      return Promise.reject(error);
    }

    originalRequest._retry = true;

    if (_isRefreshing) {
      return new Promise((resolve, reject) => {
        _failedQueue.push({ resolve, reject });
      }).then((newToken) => {
        originalRequest.headers.Authorization = `Bearer ${newToken}`;
        return apiClient(originalRequest);
      });
    }

    _isRefreshing = true;

    try {
      const { data } = await apiClient.post('/Auth/refresh');
      if (!data.success || !data.data?.token) {
        throw new Error('Refresh failed');
      }
      const payload = data.data;
      const newToken = payload.token;

      if (typeof _applySession === 'function') {
        _applySession(payload);
      } else if (typeof _setToken === 'function') {
        _setToken(newToken);
      }

      processQueue(null, newToken);
      originalRequest.headers.Authorization = `Bearer ${newToken}`;
      return apiClient(originalRequest);
    } catch (refreshError) {
      attachApiError(refreshError);
      processQueue(refreshError);
      if (typeof _onAuthCleared === 'function') {
        _onAuthCleared();
      }
      // Do not leave the user on a broken authenticated page after token expiry.
      notifySessionExpired();
      return Promise.reject(refreshError);
    } finally {
      _isRefreshing = false;
    }
  },
);

export default apiClient;
