import axios from 'axios';

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true,
});

let _getToken = null;

export function setTokenGetter(fn) {
  _getToken = fn;
}

apiClient.interceptors.request.use((config) => {
  const token = typeof _getToken === 'function' ? _getToken() : null;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export default apiClient;
