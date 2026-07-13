import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { KeyRound, ArrowLeft, CheckCircle2, AlertCircle } from 'lucide-react';
import apiClient from '../services/apiClient';
import { Button, Input, PasswordStrength } from '../components/ui';
import useApiError, { fieldErrorList } from '../hooks/useApiError';

/**
 * Step 2 of password reset: set a new strong password using email + token from the email link.
 * Expected URL: /reset-password?email=...&token=...
 */
export default function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [submitting, setSubmitting] = useState(false);
  const [success, setSuccess] = useState(false);

  const emailFromUrl = useMemo(
    () => (searchParams.get('email') || '').trim(),
    [searchParams],
  );
  const tokenFromUrl = useMemo(
    () => (searchParams.get('token') || '').trim(),
    [searchParams],
  );

  const linkInvalid = !emailFromUrl || !tokenFromUrl;

  const {
    fieldErrors: apiFieldErrors,
    generalError,
    applyError,
    clearErrors: clearApiErrors,
  } = useApiError();

  const {
    register,
    handleSubmit,
    watch,
    setError,
    formState: { errors },
  } = useForm({
    defaultValues: {
      email: emailFromUrl,
      newPassword: '',
      confirmPassword: '',
    },
  });

  const password = watch('newPassword', '');

  const mergeFieldError = (name) => {
    const client = errors[name]?.message;
    const server = fieldErrorList(apiFieldErrors, name)
      || fieldErrorList(apiFieldErrors, name === 'newPassword' ? 'password' : name);
    if (client && server?.length) return [client, ...server];
    if (server?.length) return server;
    if (client) return client;
    return undefined;
  };

  const onSubmit = async (data) => {
    if (linkInvalid) return;
    clearApiErrors();

    if (data.newPassword !== data.confirmPassword) {
      setError('confirmPassword', { type: 'validate', message: 'Passwords do not match' });
      return;
    }

    setSubmitting(true);
    try {
      const { data: res } = await apiClient.post('/Auth/reset-password', {
        email: emailFromUrl,
        token: tokenFromUrl,
        newPassword: data.newPassword,
      });

      if (res?.success === false) {
        const parsed = applyError({ response: { status: 400, data: res } });
        Object.entries(parsed.fieldErrors || {}).forEach(([field, msgs]) => {
          if (msgs?.length) {
            const formField = field.toLowerCase() === 'password' ? 'newPassword' : field;
            setError(formField, { type: 'server', message: msgs[0] });
          }
        });
        if (!parsed.hasFieldErrors) {
          toast.error(parsed.generalError || res.message || 'Password reset failed');
        }
        return;
      }

      setSuccess(true);
      toast.success('Password reset successfully');
    } catch (err) {
      const parsed = applyError(err);
      Object.entries(parsed.fieldErrors || {}).forEach(([field, msgs]) => {
        if (msgs?.length) {
          const formField = field.toLowerCase() === 'password' || field.toLowerCase() === 'newpassword'
            ? 'newPassword'
            : field;
          setError(formField, { type: 'server', message: msgs[0] });
        }
      });
      if (!parsed.hasFieldErrors) {
        toast.error(parsed.generalError || err.message || 'Password reset failed');
      }
    } finally {
      setSubmitting(false);
    }
  };

  if (linkInvalid) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-bg px-4 sm:px-6 py-8 sm:py-12 w-full max-w-[100vw]">
        <div className="w-full max-w-sm min-w-0 text-center">
          <div className="bg-white rounded-xl shadow-card p-6 sm:p-8 border border-border-light">
            <div className="mx-auto w-12 h-12 rounded-full bg-status-cancelled-bg flex items-center justify-center mb-4">
              <AlertCircle size={24} className="text-status-cancelled-text" aria-hidden="true" />
            </div>
            <h1 className="text-xl font-semibold text-text tracking-tight m-0">Invalid reset link</h1>
            <p className="text-sm text-text-muted mt-3 m-0">
              This password reset link is missing required information or is malformed.
              Please request a new link.
            </p>
            <div className="mt-6 space-y-2">
              <Button className="w-full" onClick={() => navigate('/forgot-password')}>
                Request new link
              </Button>
              <Link
                to="/login"
                className="inline-flex items-center justify-center gap-1.5 w-full min-h-11 text-sm text-primary font-medium"
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

  if (success) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-bg px-4 sm:px-6 py-8 sm:py-12 w-full max-w-[100vw]">
        <div className="w-full max-w-sm min-w-0 text-center">
          <div className="bg-white rounded-xl shadow-card p-6 sm:p-8 border border-border-light">
            <div className="mx-auto w-12 h-12 rounded-full bg-status-confirmed-bg flex items-center justify-center mb-4">
              <CheckCircle2 size={24} className="text-status-confirmed-text" aria-hidden="true" />
            </div>
            <h1 className="text-xl font-semibold text-text tracking-tight m-0">Password updated</h1>
            <p className="text-sm text-text-muted mt-3 m-0" role="status">
              Your password has been reset. Sign in with your new password. Any other devices
              have been signed out.
            </p>
            <Button className="w-full mt-6" size="lg" onClick={() => navigate('/login', { replace: true })}>
              Go to login
            </Button>
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
            <KeyRound size={22} className="text-primary" aria-hidden="true" />
          </div>
          <h1 className="text-xl sm:text-2xl font-semibold text-text tracking-tight">
            Set new password
          </h1>
          <p className="text-sm text-text-muted mt-1 break-words">
            Choose a strong password for{' '}
            <span className="font-medium text-text">{emailFromUrl}</span>
          </p>
        </header>

        <form
          onSubmit={handleSubmit(onSubmit)}
          className="bg-white rounded-xl shadow-card p-4 sm:p-6 border border-border-light min-w-0"
          noValidate
          aria-label="Reset password form"
        >
          {generalError && (
            <div
              className="mb-4 rounded-lg border border-status-cancelled-text/30 bg-status-cancelled-bg px-3 py-2 text-sm text-status-cancelled-text break-words"
              role="alert"
            >
              {generalError}
            </div>
          )}

          {/* Hidden fields keep autocomplete / form semantics without editable email */}
          <input type="hidden" value={emailFromUrl} readOnly autoComplete="username" />
          <input type="hidden" value={tokenFromUrl} readOnly />

          <Input
            label="New password"
            type="password"
            autoComplete="new-password"
            error={mergeFieldError('newPassword')}
            helperText={
              mergeFieldError('newPassword')
                ? undefined
                : 'Min 12 characters with upper, lower, number, and special character'
            }
            {...register('newPassword', {
              required: 'New password is required',
              minLength: {
                value: 12,
                message: 'Password must be at least 12 characters',
              },
              validate: {
                upper: (v) => /[A-Z]/.test(v) || 'Password must contain at least one uppercase letter',
                lower: (v) => /[a-z]/.test(v) || 'Password must contain at least one lowercase letter',
                number: (v) => /[0-9]/.test(v) || 'Password must contain at least one number',
                special: (v) =>
                  /[^a-zA-Z0-9]/.test(v) || 'Password must contain at least one special character',
              },
            })}
          />
          <PasswordStrength password={password} />
          <Input
            label="Confirm new password"
            type="password"
            autoComplete="new-password"
            error={mergeFieldError('confirmPassword')}
            {...register('confirmPassword', {
              required: 'Please confirm your password',
            })}
          />

          <Button type="submit" loading={submitting} className="w-full mt-2" size="lg">
            Reset password
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
