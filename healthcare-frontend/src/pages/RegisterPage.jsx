import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useAuth } from '../context/AuthContext';

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
    <div style={{ maxWidth: 400, margin: '80px auto', padding: '0 16px' }}>
      <h1>Register</h1>
      <form onSubmit={handleSubmit}>
        <div style={{ marginBottom: 12 }}>
          <label>Username</label><br />
          <input name="username" value={form.username} onChange={handleChange} required minLength={3}
            style={{ width: '100%', padding: 8 }} />
        </div>
        <div style={{ marginBottom: 12 }}>
          <label>Email</label><br />
          <input name="email" type="email" value={form.email} onChange={handleChange} required
            style={{ width: '100%', padding: 8 }} />
        </div>
        <div style={{ marginBottom: 12 }}>
          <label>Password</label><br />
          <input name="password" type="password" value={form.password} onChange={handleChange} required minLength={8}
            style={{ width: '100%', padding: 8 }} />
        </div>
        <div style={{ marginBottom: 12 }}>
          <label>Confirm Password</label><br />
          <input name="confirmPassword" type="password" value={form.confirmPassword}
            onChange={handleChange} required minLength={8}
            style={{ width: '100%', padding: 8 }} />
        </div>
        <div style={{ marginBottom: 12 }}>
          <label>Role</label><br />
          <select name="role" value={form.role} onChange={handleChange} style={{ width: '100%', padding: 8 }}>
            <option value="Patient">Patient</option>
            <option value="Doctor">Doctor</option>
            <option value="Admin">Admin</option>
          </select>
        </div>
        <button type="submit" disabled={loading} style={{ width: '100%', padding: 10 }}>
          {loading ? 'Registering...' : 'Register'}
        </button>
      </form>
      <p style={{ marginTop: 16 }}>
        Already have an account? <Link to="/login">Login</Link>
      </p>
    </div>
  );
}
