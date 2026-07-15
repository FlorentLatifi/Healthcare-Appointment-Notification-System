import { createContext, useContext, useState, useCallback, useEffect } from 'react';
import apiClient, {
  setTokenGetter,
  setTokenSetter,
  setSessionApplier,
  onAuthCleared,
} from '../services/apiClient';
import { parseApiError, flattenApiErrors } from '../hooks/useApiError';

const AuthContext = createContext(null);

function toUserFacingError(err, fallback) {
  const parsed = parseApiError(err);
  const message = flattenApiErrors(parsed) || err?.message || fallback;
  const e = new Error(message);
  e.apiError = parsed;
  e.cause = err;
  e.code = err?.code;
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
 * True when login/refresh payload can drive role-based UI.
 * Empty role was the root cause of the "blank dashboard after login" bug.
 * (Not exported — keeps AuthContext a components/hooks module for react-refresh.)
 */
function isCompleteSessionPayload(data) {
  if (!data || typeof data !== 'object') return false;
  if (!data.token || typeof data.token !== 'string') return false;
  if (!data.role || typeof data.role !== 'string' || !data.role.trim()) return false;
  return true;
}

/**
 * Apply login/refresh payload to React state.
 * patientId / doctorId must come from the server JWT claims only — never client-side mutation.
 */
function sessionFromResponse(data) {
  return {
    token: data.token,
    user: { username: data.username || '', role: data.role },
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

  const applySession = useCallback((payload, { requireComplete = true } = {}) => {
    if (requireComplete && !isCompleteSessionPayload(payload)) {
      const e = new Error(
        'Session is incomplete (missing role or token). Please sign in again.',
      );
      e.code = 'INCOMPLETE_SESSION';
      throw e;
    }
    if (!payload?.token) {
      return null;
    }
    // Soft path (requireComplete false): still skip applying empty-role sessions.
    if (!payload.role || !String(payload.role).trim()) {
      return null;
    }
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
    // Full session apply on silent refresh so patientId/doctorId/role stay in sync.
    setSessionApplier((payload) => {
      try {
        applySession(payload, { requireComplete: true });
      } catch {
        clearSession();
      }
    });
    onAuthCleared(() => {
      clearSession();
    });
  }, [clearSession, applySession]);

  useEffect(() => {
    let cancelled = false;
    const restoreSession = async () => {
      try {
        const { data } = await apiClient.post('/Auth/refresh');
        if (!cancelled && data.success && data.data) {
          // Soft apply: incomplete payloads leave the user logged out (no blank UI).
          applySession(data.data, { requireComplete: false });
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
      if (!isCompleteSessionPayload(data.data)) {
        const e = new Error(
          'Login succeeded but the server did not return a user role. Please contact support or try again.',
        );
        e.code = 'INCOMPLETE_SESSION';
        throw e;
      }
      return applySession(data.data, { requireComplete: true });
    } catch (err) {
      if (err.apiError || err.code === 'INCOMPLETE_SESSION') throw err;
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
      if (!isCompleteSessionPayload(data.data)) {
        clearSession();
        const e = new Error('Session refresh returned an incomplete session.');
        e.code = 'INCOMPLETE_SESSION';
        throw e;
      }
      return applySession(data.data, { requireComplete: true });
    } catch (err) {
      if (err.apiError || err.code === 'INCOMPLETE_SESSION') throw err;
      throw toUserFacingError(err, 'Session refresh failed');
    }
  }, [applySession, clearSession]);

  /**
   * Apply session fields returned from profile-create endpoints
   * (POST /Patients, POST /Doctors self-service). Falls back to /Auth/refresh
   * when the server did not include a token (e.g. admin catalog create).
   */
  const applyProfileSession = useCallback(async (profilePayload) => {
    if (profilePayload?.token && profilePayload?.role) {
      return applySession(
        {
          token: profilePayload.token,
          username: profilePayload.username,
          role: profilePayload.role,
          patientId: profilePayload.patientId,
          doctorId: profilePayload.doctorId,
        },
        { requireComplete: true },
      );
    }
    return refreshSession();
  }, [applySession, refreshSession]);

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
        applyProfileSession,
        isAuthenticated: !!token && !!user?.role,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

// Context modules export the provider + consumer hook; HMR only cares about the provider tree.
// eslint-disable-next-line react-refresh/only-export-components -- useAuth is the standard companion export
export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
