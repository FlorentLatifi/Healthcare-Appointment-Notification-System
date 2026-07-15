import { useCallback, useState } from 'react';

/**
 * Normalize ASP.NET / FluentValidation / ApiResponse errors into field-level maps.
 *
 * Handles:
 * - ValidationProblemDetails: { status: 400, errors: { Password: ["..."], "User.Email": ["..."] } }
 * - ProblemDetails with invalidParams (RFC 9457 style)
 * - App ApiResponse: { success: false, message, errors: ["..."] }
 * - Axios wrapper: error.response.data
 *
 * @param {unknown} error
 * @returns {{ fieldErrors: Record<string, string[]>, generalError: string | null }}
 */
export function parseApiError(error) {
  const empty = { fieldErrors: {}, generalError: null };

  if (!error) return empty;

  // Already-normalized payload (re-throw path / Axios interceptor)
  if (error.apiError && typeof error.apiError === 'object') {
    const fieldErrors = error.apiError.fieldErrors || {};
    const hasFieldErrors = Object.keys(fieldErrors).length > 0;
    return {
      fieldErrors,
      generalError: error.apiError.generalError ?? (hasFieldErrors ? null : error.message) ?? null,
      hasFieldErrors,
      isValidationError: hasFieldErrors || !!error.apiError.isValidationError,
      isRateLimited: !!error.apiError.isRateLimited,
      retryAfterSeconds: error.apiError.retryAfterSeconds ?? null,
    };
  }

  const status = error?.response?.status;
  const data = error?.response?.data ?? error?.data ?? null;
  const headers = error?.response?.headers;

  // 429 Too Many Requests — prefer Retry-After + server message ("try again in N seconds")
  if (status === 429) {
    const retrySeconds = parseRetryAfterSeconds(headers, data);
    const serverMsg =
      (typeof data?.errors === 'object' && !Array.isArray(data.errors)
        ? null
        : Array.isArray(data?.errors)
          ? data.errors.filter(Boolean).join(' ')
          : null)
      || (typeof data?.message === 'string' ? data.message : null)
      || (typeof data?.title === 'string' ? data.title : null);

    let generalError;
    if (serverMsg && /try again in \d+/i.test(serverMsg)) {
      generalError = serverMsg;
    } else if (retrySeconds != null) {
      if (retrySeconds >= 60) {
        const mins = Math.ceil(retrySeconds / 60);
        generalError = `Too many requests. Please try again in about ${mins} minute${mins === 1 ? '' : 's'}.`;
      } else {
        generalError = `Too many requests. Please try again in ${retrySeconds} second${retrySeconds === 1 ? '' : 's'}.`;
      }
    } else if (serverMsg && !/rate limit/i.test(serverMsg)) {
      generalError = serverMsg;
    } else {
      generalError = 'Too many requests. Please wait a moment and try again.';
    }

    return {
      fieldErrors: {},
      generalError,
      hasFieldErrors: false,
      isValidationError: false,
      isRateLimited: true,
      retryAfterSeconds: retrySeconds,
    };
  }

  // Network / non-HTTP
  if (!data) {
    return {
      fieldErrors: {},
      generalError: error?.message || 'Something went wrong. Please try again.',
    };
  }

  const fieldErrors = {};

  // ValidationProblemDetails / FluentValidation AspNetCore auto-validation
  // { errors: { Password: ["msg"], Email: ["msg"] } }
  if (data.errors && typeof data.errors === 'object' && !Array.isArray(data.errors)) {
    for (const [rawKey, messages] of Object.entries(data.errors)) {
      const key = normalizeFieldName(rawKey);
      const list = Array.isArray(messages) ? messages.filter(Boolean) : [String(messages)];
      if (!list.length) continue;
      fieldErrors[key] = [...(fieldErrors[key] || []), ...list];
    }
  }

  // RFC 9457 invalid-params style
  // { invalidParams: [{ name: "password", reason: "..." }] } or invalid-params
  const invalidParams = data.invalidParams || data['invalid-params'];
  if (Array.isArray(invalidParams)) {
    for (const p of invalidParams) {
      const key = normalizeFieldName(p?.name || p?.field || '');
      const msg = p?.reason || p?.message || p?.detail;
      if (!key || !msg) continue;
      fieldErrors[key] = [...(fieldErrors[key] || []), msg];
    }
  }

  // ApiResponse: { success: false, errors: ["..."], message: "..." }
  let generalError = null;
  if (Array.isArray(data.errors) && data.errors.length) {
    // If only password-ish messages, also attach to password field
    const msgs = data.errors.filter(Boolean).map(String);
    const passwordMsgs = msgs.filter((m) => /password/i.test(m));
    if (passwordMsgs.length) {
      fieldErrors.password = [...(fieldErrors.password || []), ...passwordMsgs];
    }
    const other = msgs.filter((m) => !/password/i.test(m));
    if (other.length) generalError = other.join(' ');
    else if (!Object.keys(fieldErrors).length) generalError = msgs.join(' ');
  }

  if (!generalError && typeof data.message === 'string' && data.message.trim()) {
    // Prefer field errors alone when ProblemDetails title is generic
    const genericTitles = new Set([
      'One or more validation errors occurred.',
      'Validation failed',
      'Bad Request',
    ]);
    if (!genericTitles.has(data.message) || !Object.keys(fieldErrors).length) {
      if (!Object.keys(fieldErrors).length || !genericTitles.has(data.message)) {
        generalError = data.message;
      }
    }
  }

  if (!generalError && typeof data.title === 'string' && data.title.trim()) {
    const genericTitles = new Set([
      'One or more validation errors occurred.',
      'Bad Request',
    ]);
    if (!Object.keys(fieldErrors).length && !genericTitles.has(data.title)) {
      generalError = data.title;
    }
  }

  if (!generalError && typeof data.detail === 'string' && data.detail.trim()) {
    generalError = data.detail;
  }

  // 400/422 with only field errors → no need for a second general banner
  if ((status === 400 || status === 422) && Object.keys(fieldErrors).length) {
    // keep generalError only if it adds unique info
    if (generalError) {
      const allField = Object.values(fieldErrors).flat().join(' ');
      if (allField.includes(generalError) || generalError === 'Registration failed') {
        generalError = null;
      }
    }
  }

  if (!Object.keys(fieldErrors).length && !generalError) {
    generalError = error?.message || 'Request failed. Please check your input.';
  }

  const hasFieldErrors = Object.keys(fieldErrors).length > 0;
  const isValidationError =
    hasFieldErrors ||
    status === 400 ||
    status === 422 ||
    (data?.success === false && Array.isArray(data?.errors));

  return {
    fieldErrors,
    generalError,
    hasFieldErrors,
    /** True when errors should be shown inline (not only as a toast). */
    isValidationError: hasFieldErrors || (isValidationError && !generalError),
  };
}

/**
 * Parse Retry-After (seconds or HTTP-date) from Axios headers / body.
 * @returns {number|null} whole seconds until retry, or null
 */
export function parseRetryAfterSeconds(headers, data) {
  const raw =
    headers?.['retry-after']
    ?? headers?.['Retry-After']
    ?? data?.retryAfter
    ?? data?.retryAfterSeconds
    ?? null;
  if (raw == null || raw === '') return null;
  const asNum = Number(raw);
  if (Number.isFinite(asNum) && asNum >= 0) return Math.max(1, Math.ceil(asNum));
  const asDate = Date.parse(String(raw));
  if (!Number.isNaN(asDate)) {
    const sec = Math.ceil((asDate - Date.now()) / 1000);
    return sec > 0 ? sec : 1;
  }
  return null;
}

/**
 * Map ASP.NET / FluentValidation property paths to form field names.
 * "Password" → "password", "User.Email" → "email", "$.password" → "password"
 */
function normalizeFieldName(raw) {
  if (!raw || typeof raw !== 'string') return 'form';
  let key = raw.trim();
  if (key.startsWith('$.')) key = key.slice(2);
  // Take last segment of dotted path
  const parts = key.split(/[.\[]/).filter(Boolean);
  key = parts[parts.length - 1] || key;
  key = key.replace(/\]/g, '');
  // camelCase first letter
  if (key.length) key = key.charAt(0).toLowerCase() + key.slice(1);
  return key;
}

/**
 * First message for a field (for Input `error` prop).
 * @param {Record<string, string[]>} fieldErrors
 * @param {string} field
 */
export function firstFieldError(fieldErrors, field) {
  const list = fieldErrors?.[field];
  return Array.isArray(list) && list.length ? list[0] : undefined;
}

/** All messages for a field (supports multi-message FluentValidation). */
export function fieldErrorList(fieldErrors, field) {
  const list = fieldErrors?.[field];
  return Array.isArray(list) && list.length ? list : undefined;
}

/**
 * All messages flattened (for toast fallback).
 */
export function flattenApiErrors({ fieldErrors = {}, generalError = null } = {}) {
  const parts = [
    ...Object.values(fieldErrors).flat(),
    ...(generalError ? [generalError] : []),
  ].filter(Boolean);
  return parts.length ? parts.join(' ') : null;
}

/**
 * React hook: hold field/general errors and apply parseApiError on catch.
 */
export default function useApiError() {
  const [fieldErrors, setFieldErrors] = useState({});
  const [generalError, setGeneralError] = useState(null);

  const clearErrors = useCallback(() => {
    setFieldErrors({});
    setGeneralError(null);
  }, []);

  const clearField = useCallback((field) => {
    setFieldErrors((prev) => {
      if (!prev[field]) return prev;
      const next = { ...prev };
      delete next[field];
      return next;
    });
  }, []);

  const applyError = useCallback((error) => {
    const parsed = parseApiError(error);
    setFieldErrors(parsed.fieldErrors);
    setGeneralError(parsed.generalError);
    return parsed;
  }, []);

  const setFieldError = useCallback((field, message) => {
    setFieldErrors((prev) => ({
      ...prev,
      [field]: Array.isArray(message) ? message : [message],
    }));
  }, []);

  return {
    fieldErrors,
    generalError,
    setGeneralError,
    setFieldErrors,
    setFieldError,
    clearErrors,
    clearField,
    applyError,
    parseApiError,
    firstFieldError: (field) => firstFieldError(fieldErrors, field),
    fieldErrorList: (field) => fieldErrorList(fieldErrors, field),
    hasFieldErrors: Object.keys(fieldErrors).length > 0,
  };
}
