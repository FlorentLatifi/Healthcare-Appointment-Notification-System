import { createContext, useContext, useState, useCallback, useEffect } from 'react';
import apiClient, { setTokenGetter } from '../services/apiClient';

/*
 * Auth state is kept ONLY in React memory (context), NOT in localStorage.
 *
 * WHY: Storing tokens in localStorage/sessionStorage exposes them to XSS
 * attacks — any injected script can read them. Keeping them in memory
 * means they survive only for the current page session and are lost on
 * reload (forcing re-login). This is a deliberate security trade-off:
 * convenience (persisted sessions) is sacrificed for resilience against
 * token theft via XSS.
 *
 * For a production app, consider refresh tokens with httpOnly cookies
 * instead.
 */

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [token, setToken] = useState(null);
  const [patientId, setPatientId] = useState(null);
  const [loading, setLoading] = useState(false);

  const getToken = useCallback(() => token, [token]);

  useEffect(() => {
    setTokenGetter(getToken);
  }, [getToken]);

  const login = useCallback(async (username, password) => {
    setLoading(true);
    try {
      const { data } = await apiClient.post('/Auth/login', { username, password });
      if (!data.success) {
        throw new Error(data.errors?.[0] || data.message || 'Login failed');
      }
      setToken(data.data.token);
      setUser({ username: data.data.username, role: data.data.role });
      return data.data;
    } finally {
      setLoading(false);
    }
  }, []);

  const register = useCallback(async (username, email, password, role) => {
    setLoading(true);
    try {
      const { data } = await apiClient.post('/Auth/register', {
        username, email, password, role: role || 'Patient',
      });
      if (!data.success) {
        throw new Error(data.errors?.[0] || data.message || 'Registration failed');
      }
      return data.data;
    } finally {
      setLoading(false);
    }
  }, []);

  const logout = useCallback(() => {
    setToken(null);
    setUser(null);
    setPatientId(null);
  }, []);

  return (
    <AuthContext.Provider value={{ user, token, patientId, loading, login, register, logout, setPatientId, isAuthenticated: !!token }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
