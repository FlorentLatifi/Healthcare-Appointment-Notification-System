import { useForm } from 'react-hook-form';
import { useNavigate, Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useAuth } from '../context/AuthContext';
import { Button, Input } from '../components/ui';
import useApiError, { fieldErrorList } from '../hooks/useApiError';

export default function LoginPage() {
  const { login, loading } = useAuth();
  const navigate = useNavigate();
  const {
    fieldErrors: apiFieldErrors,
    generalError,
    applyError,
    clearErrors: clearApiErrors,
  } = useApiError();

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm({
    defaultValues: { username: '', password: '' },
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
    try {
      await login(data.username, data.password);
      toast.success('Login successful');
      navigate('/dashboard', { replace: true });
    } catch (err) {
      const parsed = applyError(err);
      Object.entries(parsed.fieldErrors || {}).forEach(([field, msgs]) => {
        if (msgs?.length) setError(field, { type: 'server', message: msgs[0] });
      });
      // Auth failures are usually general (bad credentials) — toast those
      if (!parsed.hasFieldErrors) {
        toast.error(parsed.generalError || err.message || 'Login failed');
      }
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-bg px-4 sm:px-6 py-8 sm:py-12">
      <div className="w-full max-w-sm">
        <div className="text-center mb-6 sm:mb-8">
          <h1 className="text-xl sm:text-2xl font-semibold text-text tracking-tight">Login</h1>
          <p className="text-sm text-text-muted mt-1">Sign in to your account</p>
        </div>
        <form
          onSubmit={handleSubmit(onSubmit)}
          className="bg-white rounded-xl shadow-card p-4 sm:p-6 border border-border-light"
          noValidate
        >
          {generalError && (
            <div
              className="mb-4 rounded-lg border border-status-cancelled-text/30 bg-status-cancelled-bg px-3 py-2 text-sm text-status-cancelled-text"
              role="alert"
            >
              {generalError}
            </div>
          )}
          <Input
            label="Username"
            autoComplete="username"
            error={mergeFieldError('username')}
            {...register('username', { required: 'Username is required' })}
          />
          <Input
            label="Password"
            type="password"
            autoComplete="current-password"
            error={mergeFieldError('password')}
            {...register('password', { required: 'Password is required' })}
          />
          <Button type="submit" disabled={loading} className="w-full mt-2" size="lg">
            {loading ? 'Logging in...' : 'Login'}
          </Button>
        </form>
        <p className="text-sm text-text-muted text-center mt-6">
          Don&apos;t have an account?{' '}
          <Link to="/register" className="text-primary font-medium hover:text-primary-hover">Register</Link>
        </p>
      </div>
    </div>
  );
}
