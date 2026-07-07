import axios from 'axios';

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true,
});

let _getToken = null;
let _setToken = null;
let _onAuthCleared = null;
let _isRefreshing = false;
let _failedQueue = [];

export function setTokenGetter(fn) {
  _getToken = fn;
}

export function setTokenSetter(fn) {
  _setToken = fn;
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
    const originalRequest = error.config;

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
      if (!data.success) {
        throw new Error('Refresh failed');
      }
      const newToken = data.data.token;
      if (typeof _setToken === 'function') {
        _setToken(newToken);
      }
      processQueue(null, newToken);
      originalRequest.headers.Authorization = `Bearer ${newToken}`;
      return apiClient(originalRequest);
    } catch (refreshError) {
      processQueue(refreshError);
      if (typeof _onAuthCleared === 'function') {
        _onAuthCleared();
      }
      return Promise.reject(refreshError);
    } finally {
      _isRefreshing = false;
    }
  },
);

export default apiClient;
