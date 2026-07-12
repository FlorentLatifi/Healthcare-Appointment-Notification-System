import { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { Button } from './ui';
import { LogOut, Menu, X } from 'lucide-react';

export default function Navbar() {
  const { user, logout, patientId } = useAuth();
  const navigate = useNavigate();
  const loc = useLocation();
  const [menuOpen, setMenuOpen] = useState(false);

  const links = [
    { label: 'Dashboard', path: '/dashboard', show: true },
  ];

  if (user?.role === 'Patient') {
    links.push(
      { label: 'Doctors', path: '/doctors', show: true },
      { label: 'My Appointments', path: '/my-appointments', show: !!patientId },
    );
  }
  if (user?.role === 'Doctor') {
    links.push({ label: 'Doctor Dashboard', path: '/doctor-dashboard', show: true });
  }
  if (user?.role === 'Admin') {
    links.push({ label: 'Admin', path: '/admin', show: true });
  }

  const visibleLinks = links.filter((l) => l.show);

  return (
    <nav className="sticky top-0 z-50 bg-white border-b border-border-light shadow-card">
      <div className="max-w-6xl mx-auto px-4 sm:px-6 h-14 flex items-center gap-2 sm:gap-4">
        <div
          className="font-semibold text-base text-primary cursor-pointer mr-auto tracking-tight shrink-0"
          onClick={() => navigate('/dashboard')}
        >
          Healthcare
        </div>

        <div className="hidden sm:flex items-center gap-1">
          {visibleLinks.map((l) => (
            <button
              key={l.path}
              className={`text-sm px-2 py-1.5 transition-all duration-150 cursor-pointer focus-visible:outline-2 focus-visible:outline-primary rounded-md ${
                loc.pathname === l.path
                  ? 'text-primary font-medium'
                  : 'text-text-muted hover:text-text'
              }`}
              onClick={() => navigate(l.path)}
            >
              {l.label}
            </button>
          ))}
        </div>

        <Button
          variant="ghost"
          size="sm"
          className="ml-auto shrink-0"
          leftIcon={<LogOut size={14} />}
          onClick={() => { logout(); navigate('/login', { replace: true }); }}
        >
          <span className="hidden sm:inline">Logout</span>
        </Button>

        <button
          className="sm:hidden inline-flex items-center justify-center min-w-10 min-h-10 p-2 rounded-md text-text-muted hover:text-text hover:bg-surface cursor-pointer bg-transparent border-none focus-visible:outline-2 focus-visible:outline-primary"
          onClick={() => setMenuOpen(!menuOpen)}
          aria-label="Toggle navigation menu"
        >
          {menuOpen ? <X size={20} /> : <Menu size={20} />}
        </button>
      </div>

      {menuOpen && (
        <div className="sm:hidden border-t border-border-light bg-white px-4 py-2 space-y-1 pb-4">
          {visibleLinks.map((l) => (
            <button
              key={l.path}
              className={`w-full text-left text-sm px-3 py-2 rounded-md transition-all duration-150 cursor-pointer focus-visible:outline-2 focus-visible:outline-primary ${
                loc.pathname === l.path
                  ? 'text-primary font-medium bg-surface'
                  : 'text-text-muted hover:text-text hover:bg-surface'
              }`}
              onClick={() => { navigate(l.path); setMenuOpen(false); }}
            >
              {l.label}
            </button>
          ))}
          <button
            className="w-full text-left text-sm px-3 py-2 rounded-md text-text-muted hover:text-text hover:bg-surface transition-all duration-150 cursor-pointer focus-visible:outline-2 focus-visible:outline-primary flex items-center gap-2"
            onClick={() => { logout(); navigate('/login', { replace: true }); setMenuOpen(false); }}
          >
            <LogOut size={14} />
            Logout
          </button>
        </div>
      )}
    </nav>
  );
}
