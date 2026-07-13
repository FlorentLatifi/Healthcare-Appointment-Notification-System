import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link } from 'react-router-dom';
import { Mail, ArrowLeft, CheckCircle2 } from 'lucide-react';
import apiClient from '../services/apiClient';
import { Button, Input } from '../components/ui';
import useApiError, { fieldErrorList } from '../hooks/useApiError';

/**
 * Step 1 of password reset: request a secure email link.
 * Always shows a generic confirmation (no account enumeration).
 */
export default function ForgotPasswordPage() {
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const {
    fieldErrors: apiFieldErrors,
    generalError,
    applyError,
    clearErrors: clearApiErrors,
  } = useApiError();

  const {
    register,
    handleSubmit,
    getValues,
    setError,
    formState: { errors },
  } = useForm({
    defaultValues: { email: '' },
  });

  const mergeFieldError = (name) => {
    const client = errors[name]?.message;
    const server = fieldErrorList(apiFieldErrors, name);
    if (client && server?.length) return [client, ...server];
    if (server?.length) return server;
    if (client) return client;
    return undefined;
  };

  const onSubmit = async (data) => {
    clearApiErrors();
    setSubmitting(true);
    try {
      await apiClient.post('/Auth/forgot-password', {
        email: data.email.trim(),
      });
      // Always treat as success for UX — API returns generic message even for unknown emails.
      setSubmitted(true);
    } catch (err) {
      const status = err.response?.status;
      if (status === 429) {
        applyError(err);
        return;
      }
      const parsed = applyError(err);
      Object.entries(parsed.fieldErrors || {}).forEach(([field, msgs]) => {
        if (msgs?.length) setError(field, { type: 'server', message: msgs[0] });
      });
      // For non-field failures that are still "soft" (network), stay on form with alert.
      if (!parsed.hasFieldErrors && status && status >= 500) {
        // keep generalError from applyError
      } else if (!parsed.hasFieldErrors && status !== 429) {
        // Unexpected 4xx: still show confirmation to avoid enumeration if server misbehaves.
        setSubmitted(true);
      }
    } finally {
      setSubmitting(false);
    }
  };

  if (submitted) {
    const email = getValues('email');
    return (
      <div className="min-h-screen flex items-center justify-center bg-bg px-4 sm:px-6 py-8 sm:py-12 w-full max-w-[100vw]">
        <div className="w-full max-w-sm min-w-0 text-center">
          <div className="bg-white rounded-xl shadow-card p-6 sm:p-8 border border-border-light">
            <div className="mx-auto w-12 h-12 rounded-full bg-status-confirmed-bg flex items-center justify-center mb-4">
              <CheckCircle2 size={24} className="text-status-confirmed-text" aria-hidden="true" />
            </div>
            <h1 className="text-xl sm:text-2xl font-semibold text-text tracking-tight m-0">
              Check your email
            </h1>
            <p className="text-sm text-text-muted mt-3 m-0 break-words" role="status">
              If an account exists for{' '}
              <span className="font-medium text-text">{email}</span>, we&apos;ve sent a password
              reset link. The link expires in about 60 minutes and can only be used once.
            </p>
            <p className="text-xs text-text-muted mt-4 m-0">
              Didn&apos;t get it? Check spam, or wait a few minutes and try again.
            </p>
            <div className="mt-6 space-y-2">
              <Button
                type="button"
                variant="secondary"
                className="w-full"
                onClick={() => setSubmitted(false)}
              >
                Try another email
              </Button>
              <Link
                to="/login"
                className="inline-flex items-center justify-center gap-1.5 w-full min-h-11 text-sm text-primary font-medium hover:text-primary-hover"
              >
                <ArrowLeft size={14} aria-hidden="true" />
                Back to login
              </Link>
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-bg px-4 sm:px-6 py-8 sm:py-12 w-full max-w-[100vw]">
      <div className="w-full max-w-sm min-w-0">
        <header className="text-center mb-6 sm:mb-8">
          <div className="mx-auto w-12 h-12 rounded-full bg-primary-50 flex items-center justify-center mb-3">
            <Mail size={22} className="text-primary" aria-hidden="true" />
          </div>
          <h1 className="text-xl sm:text-2xl font-semibold text-text tracking-tight">
            Forgot password?
          </h1>
          <p className="text-sm text-text-muted mt-1">
            Enter your account email and we&apos;ll send a secure reset link.
          </p>
        </header>

        <form
          onSubmit={handleSubmit(onSubmit)}
          className="bg-white rounded-xl shadow-card p-4 sm:p-6 border border-border-light min-w-0"
          noValidate
          aria-label="Forgot password form"
        >
          {generalError && (
            <div
              className="mb-4 rounded-lg border border-status-cancelled-text/30 bg-status-cancelled-bg px-3 py-2 text-sm text-status-cancelled-text break-words"
              role="alert"
            >
              {generalError}
            </div>
          )}

          <Input
            label="Email"
            type="email"
            autoComplete="email"
            autoCapitalize="none"
            spellCheck={false}
            error={mergeFieldError('email')}
            helperText="Use the email address on your account"
            {...register('email', {
              required: 'Email is required',
              pattern: {
                value: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
                message: 'Enter a valid email address',
              },
            })}
          />

          <Button type="submit" loading={submitting} className="w-full mt-2" size="lg">
            Send reset link
          </Button>
        </form>

        <p className="text-sm text-text-muted text-center mt-6">
          <Link
            to="/login"
            className="text-primary font-medium hover:text-primary-hover inline-flex min-h-11 items-center gap-1.5 px-1"
          >
            <ArrowLeft size={14} aria-hidden="true" />
            Back to login
          </Link>
        </p>
      </div>
    </div>
  );
}
