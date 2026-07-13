import { useForm } from 'react-hook-form';
import { useNavigate, Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useAuth } from '../context/AuthContext';
import { Button, Input, Select, PasswordStrength } from '../components/ui';
import useApiError, { fieldErrorList } from '../hooks/useApiError';

const defaultValues = {
  username: '',
  email: '',
  password: '',
  confirmPassword: '',
  role: 'Patient',
};

export default function RegisterPage() {
  const { register: registerUser, loading } = useAuth();
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
    watch,
    setError,
    clearErrors,
    formState: { errors },
  } = useForm({
    defaultValues,
    mode: 'onSubmit',
  });

  const password = watch('password', '');

  const mergeFieldError = (name) => {
    const client = errors[name]?.message;
    const server = fieldErrorList(apiFieldErrors, name) || [];
    const list = [...(client ? [client] : []), ...server];
    const unique = [...new Set(list)];
    if (!unique.length) return undefined;
    return unique.length === 1 ? unique[0] : unique;
  };

  const onSubmit = async (data) => {
    clearApiErrors();
    clearErrors();

    if (data.password !== data.confirmPassword) {
      setError('confirmPassword', { type: 'validate', message: 'Passwords do not match' });
      return;
    }

    try {
      await registerUser(data.username, data.email, data.password, data.role);
      toast.success('Registration successful! Please login.');
      navigate('/login', { replace: true });
    } catch (err) {
      const parsed = applyError(err);
      // Map server field errors into RHF so formState stays consistent
      Object.entries(parsed.fieldErrors || {}).forEach(([field, msgs]) => {
        if (msgs?.length) {
          setError(field, { type: 'server', message: msgs[0] });
        }
      });
      // Toast only for non-field (general) failures — not for validation inline errors
      if (!parsed.hasFieldErrors && (parsed.generalError || err.message)) {
        toast.error(parsed.generalError || err.message);
      }
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-bg px-4 sm:px-6 py-8 sm:py-12 w-full max-w-[100vw]">
      <div className="w-full max-w-sm min-w-0">
        <header className="text-center mb-6 sm:mb-8">
          <h1 className="text-xl sm:text-2xl font-semibold text-text tracking-tight">Register</h1>
          <p className="text-sm text-text-muted mt-1">Create a new account</p>
        </header>
        <form
          onSubmit={handleSubmit(onSubmit)}
          className="bg-white rounded-xl shadow-card p-4 sm:p-6 border border-border-light min-w-0"
          noValidate
          aria-label="Registration form"
        >
          {generalError && (
            <div
              id="register-general-error"
              className="mb-4 rounded-lg border border-status-cancelled-text/30 bg-status-cancelled-bg px-3 py-2 text-sm text-status-cancelled-text break-words"
              role="alert"
            >
              {generalError}
            </div>
          )}

          <Input
            label="Username"
            autoComplete="username"
            autoCapitalize="none"
            spellCheck={false}
            error={mergeFieldError('username')}
            helperText="Letters, numbers, dashes, underscores (min 3)"
            {...register('username', {
              required: 'Username is required',
              minLength: { value: 3, message: 'Username must be at least 3 characters' },
              pattern: {
                value: /^[a-zA-Z0-9_-]+$/,
                message: 'Username can only contain letters, numbers, dashes and underscores',
              },
            })}
          />
          <Input
            label="Email"
            type="email"
            autoComplete="email"
            error={mergeFieldError('email')}
            {...register('email', {
              required: 'Email is required',
              pattern: {
                value: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
                message: 'Invalid email format',
              },
            })}
          />
          <Input
            label="Password"
            type="password"
            autoComplete="new-password"
            error={mergeFieldError('password')}
            helperText={
              mergeFieldError('password')
                ? undefined
                : 'Min 12 characters with upper, lower, number, and special character'
            }
            {...register('password', {
              required: 'Password is required',
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
            label="Confirm Password"
            type="password"
            autoComplete="new-password"
            error={mergeFieldError('confirmPassword')}
            {...register('confirmPassword', {
              required: 'Please confirm your password',
            })}
          />
          <Select
            label="Role"
            error={mergeFieldError('role')}
            {...register('role', { required: 'Role is required' })}
          >
            <option value="Patient">Patient</option>
            <option value="Doctor">Doctor</option>
          </Select>
          <Button type="submit" disabled={loading} className="w-full mt-2" size="lg">
            {loading ? 'Registering...' : 'Register'}
          </Button>
        </form>
        <p className="text-sm text-text-muted text-center mt-6">
          Already have an account?{' '}
          <Link
            to="/login"
            className="text-primary font-medium hover:text-primary-hover inline-flex min-h-11 items-center px-1"
          >
            Login
          </Link>
        </p>
      </div>
    </div>
  );
}
