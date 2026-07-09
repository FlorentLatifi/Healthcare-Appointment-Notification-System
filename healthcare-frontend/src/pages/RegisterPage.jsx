import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useAuth } from '../context/AuthContext';
import { Button, Input, Select } from '../components/ui';

export default function RegisterPage() {
  const { register, loading } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({
    username: '', email: '', password: '', confirmPassword: '', role: 'Patient',
  });

  const handleChange = (e) => {
    setForm((prev) => ({ ...prev, [e.target.name]: e.target.value }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (form.password !== form.confirmPassword) {
      toast.error('Passwords do not match');
      return;
    }
    try {
      await register(form.username, form.email, form.password, form.role);
      toast.success('Registration successful! Please login.');
      navigate('/login', { replace: true });
    } catch (err) {
      toast.error(err.message);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-bg px-4">
      <div className="w-full max-w-sm">
        <div className="text-center mb-8">
          <h1 className="text-2xl font-semibold text-text tracking-tight">Register</h1>
          <p className="text-sm text-text-muted mt-1">Create a new account</p>
        </div>
        <form onSubmit={handleSubmit} className="bg-white rounded-xl shadow-card p-6 border border-border-light">
          <Input label="Username" name="username" value={form.username} onChange={handleChange} required minLength={3} autoComplete="username" />
          <Input label="Email" name="email" type="email" value={form.email} onChange={handleChange} required autoComplete="email" />
          <Input label="Password" name="password" type="password" value={form.password} onChange={handleChange} required minLength={8} autoComplete="new-password" />
          <Input label="Confirm Password" name="confirmPassword" type="password" value={form.confirmPassword} onChange={handleChange} required minLength={8} autoComplete="new-password" />
          <Select label="Role" name="role" value={form.role} onChange={handleChange}>
            <option value="Patient">Patient</option>
            <option value="Doctor">Doctor</option>
            <option value="Admin">Admin</option>
          </Select>
          <Button type="submit" disabled={loading} className="w-full mt-2" size="lg">
            {loading ? 'Registering...' : 'Register'}
          </Button>
        </form>
        <p className="text-sm text-text-muted text-center mt-6">
          Already have an account?{' '}
          <Link to="/login" className="text-primary font-medium hover:text-primary-hover">Login</Link>
        </p>
      </div>
    </div>
  );
}
