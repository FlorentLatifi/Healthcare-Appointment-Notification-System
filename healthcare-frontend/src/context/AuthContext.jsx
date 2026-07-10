import { createContext, useContext, useState, useCallback, useEffect } from 'react';
import apiClient, { setTokenGetter, setTokenSetter, onAuthCleared } from '../services/apiClient';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [token, setToken] = useState(null);
  const [patientId, setPatientId] = useState(null);
  const [doctorId, setDoctorId] = useState(null);
  const [loading, setLoading] = useState(false);

  const getToken = useCallback(() => token, [token]);

  useEffect(() => {
    setTokenGetter(getToken);
  }, [getToken]);

  useEffect(() => {
    setTokenSetter(setToken);
    onAuthCleared(() => {
      setToken(null);
      setUser(null);
      setPatientId(null);
      setDoctorId(null);
    });
  }, [setToken]);

  useEffect(() => {
    const restoreSession = async () => {
      try {
        const { data } = await apiClient.post('/Auth/refresh');
        if (data.success) {
          setToken(data.data.token);
          setUser({ username: data.data.username, role: data.data.role });
          setPatientId(data.data.patientId ?? null);
          setDoctorId(data.data.doctorId ?? null);
        }
      } catch {
        // No valid refresh cookie — user stays logged out
      }
    };
    restoreSession();
  }, []);

  const login = useCallback(async (username, password) => {
    setLoading(true);
    try {
      const { data } = await apiClient.post('/Auth/login', { username, password });
      if (!data.success) {
        throw new Error(data.errors?.[0] || data.message || 'Login failed');
      }
      setToken(data.data.token);
      setUser({ username: data.data.username, role: data.data.role });
      setPatientId(data.data.patientId ?? null);
      setDoctorId(data.data.doctorId ?? null);
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

  const logout = useCallback(async () => {
    try {
      await apiClient.post('/Auth/logout');
    } catch {
      // Even if server-side revocation fails, clear local state
    }
    setToken(null);
    setUser(null);
    setPatientId(null);
    setDoctorId(null);
  }, []);

  return (
    <AuthContext.Provider value={{ user, token, patientId, doctorId, loading, login, register, logout, setPatientId, setDoctorId, isAuthenticated: !!token }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
