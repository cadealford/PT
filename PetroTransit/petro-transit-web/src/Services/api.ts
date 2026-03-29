import axios from 'axios';
import { getToken, clearToken } from './authStorage';

// Production (Lightsail Docker): frontend + API share the same domain.
// Nginx proxies /api/* to the .NET container, so baseURL can be "/api".
//
// Local dev: default to your local .NET API URL.
const API_BASE = import.meta.env.VITE_API_BASE_URL ?? (import.meta.env.DEV ? 'http://localhost:5270' : '');
const api = axios.create({
  baseURL: `${API_BASE}/api`,
});

// Request interceptor to attach the JWT token to every request
api.interceptors.request.use(
  config => {
    const token = getToken();
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  error => Promise.reject(error)
);

// Response interceptor to handle token expiration/unauthorized errors
api.interceptors.response.use(
  response => response,
  error => {
    if (error.response && error.response.status === 401) {
      clearToken();
      if (window.location.pathname !== '/login') {
        window.location.href = '/login';
      }
    }
    return Promise.reject(error);
  }
);

export default api;
