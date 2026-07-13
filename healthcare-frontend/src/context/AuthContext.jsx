import { createContext, useContext, useState, useCallback, useEffect } from 'react';
import apiClient, { setTokenGetter, setTokenSetter, onAuthCleared } from '../services/apiClient';
import { parseApiError, flattenApiErrors } from '../hooks/useApiError';

const AuthContext = createContext(null);

function toUserFacingError(err, fallback) {
  const parsed = parseApiError(err);
  const message = flattenApiErrors(parsed) || err?.message || fallback;
  const e = new Error(message);
  e.apiError = parsed;
  e.cause = err;
  return e;
}

/**
 * Normalize profile claim ids from login/refresh payloads.
 * Treats missing, null, or 0 as "not linked" (Id=0 was the historical identity bug).
 */
function normalizeProfileId(value) {
  if (value == null || value === '') return null;
  const n = Number(value);
  if (!Number.isFinite(n) || n <= 0) return null;
  return n;
}

/**
 * Apply login/refresh payload to React state.
 * patientId / doctorId must come from the server JWT claims only — never client-side mutation.
 */
function sessionFromResponse(data) {
  return {
    token: data.token,
    user: { username: data.username, role: data.role },
    patientId: normalizeProfileId(data.patientId),
    doctorId: normalizeProfileId(data.doctorId),
  };
}

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [token, setToken] = useState(null);
  const [patientId, setPatientId] = useState(null);
  const [doctorId, setDoctorId] = useState(null);
  const [loading, setLoading] = useState(false);
  /** False until the initial /Auth/refresh (cookie restore) attempt finishes. */
  const [sessionReady, setSessionReady] = useState(false);

  const applySession = useCallback((payload) => {
    const session = sessionFromResponse(payload);
    setToken(session.token);
    setUser(session.user);
    setPatientId(session.patientId);
    setDoctorId(session.doctorId);
    return session;
  }, []);

  const clearSession = useCallback(() => {
    setToken(null);
    setUser(null);
    setPatientId(null);
    setDoctorId(null);
  }, []);

  const getToken = useCallback(() => token, [token]);

  useEffect(() => {
    setTokenGetter(getToken);
  }, [getToken]);

  useEffect(() => {
    setTokenSetter(setToken);
    onAuthCleared(() => {
      clearSession();
    });
  }, [clearSession]);

  useEffect(() => {
    let cancelled = false;
    const restoreSession = async () => {
      try {
        const { data } = await apiClient.post('/Auth/refresh');
        if (!cancelled && data.success) {
          applySession(data.data);
        }
      } catch {
        // No valid refresh cookie — user stays logged out
      } finally {
        if (!cancelled) setSessionReady(true);
      }
    };
    restoreSession();
    return () => {
      cancelled = true;
    };
  }, [applySession]);

  const login = useCallback(async (username, password) => {
    setLoading(true);
    try {
      const { data } = await apiClient.post('/Auth/login', { username, password });
      if (!data.success) {
        throw toUserFacingError(
          { response: { status: 400, data } },
          'Login failed',
        );
      }
      return applySession(data.data);
    } catch (err) {
      if (err.apiError) throw err;
      throw toUserFacingError(err, 'Login failed');
    } finally {
      setLoading(false);
    }
  }, [applySession]);

  /**
   * Re-issue access token (and rotate refresh cookie) from the server.
   * Call after profile creation so JWT patient_id / doctor_id claims match the DB link.
   */
  const refreshSession = useCallback(async () => {
    try {
      const { data } = await apiClient.post('/Auth/refresh');
      if (!data.success) {
        throw toUserFacingError(
          { response: { status: 400, data } },
          'Session refresh failed',
        );
      }
      return applySession(data.data);
    } catch (err) {
      if (err.apiError) throw err;
      throw toUserFacingError(err, 'Session refresh failed');
    }
  }, [applySession]);

  const register = useCallback(async (username, email, password, role) => {
    setLoading(true);
    try {
      const { data } = await apiClient.post('/Auth/register', {
        username, email, password, role: role || 'Patient',
      });
      if (!data.success) {
        throw toUserFacingError(
          { response: { status: 400, data } },
          'Registration failed',
        );
      }
      return data.data;
    } catch (err) {
      if (err.apiError) throw err;
      throw toUserFacingError(err, 'Registration failed');
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
    clearSession();
  }, [clearSession]);

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        patientId,
        doctorId,
        loading,
        sessionReady,
        login,
        register,
        logout,
        refreshSession,
        isAuthenticated: !!token,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
