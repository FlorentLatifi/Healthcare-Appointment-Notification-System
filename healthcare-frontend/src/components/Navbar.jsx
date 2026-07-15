import { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { Button } from './ui';
import NotificationsMenu from './NotificationsMenu';
import { LogOut, Menu, X } from 'lucide-react';

export default function Navbar() {
  const { user, logout, patientId, isAuthenticated } = useAuth();
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
    links.push(
      { label: 'Analytics', path: '/admin/analytics', show: true },
      { label: 'Audit Logs', path: '/admin/audit-logs', show: true },
      { label: 'Doctors', path: '/admin', show: true },
      { label: 'Patients', path: '/admin/patients', show: true },
    );
  }

  const visibleLinks = links.filter((l) => l.show);

  const isActivePath = (path) => {
    if (path === '/admin') {
      // Doctors catalog only — not analytics / patients / audit sub-routes.
      return loc.pathname === '/admin' || loc.pathname === '/admin/doctors';
    }
    return loc.pathname === path || loc.pathname.startsWith(`${path}/`);
  };

  return (
    <nav className="sticky top-0 z-50 bg-white border-b border-border-light shadow-card" aria-label="Main">
      <div className="max-w-6xl mx-auto px-4 sm:px-6 h-14 flex items-center gap-2 sm:gap-3 min-w-0">
        <button
          type="button"
          className="font-semibold text-base text-primary cursor-pointer mr-auto tracking-tight shrink-0 bg-transparent border-none p-0 min-h-11 inline-flex items-center focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary rounded-md"
          onClick={() => navigate('/dashboard')}
          aria-label="Healthcare home"
        >
          Healthcare
        </button>

        <div className="hidden sm:flex items-center gap-0.5 flex-wrap justify-end max-w-[min(100%,28rem)] lg:max-w-none">
          {visibleLinks.map((l) => (
            <button
              type="button"
              key={l.path}
              className={`text-sm min-h-11 px-2.5 py-1.5 transition-all duration-150 cursor-pointer focus-visible:outline-2 focus-visible:outline-primary rounded-md ${
                isActivePath(l.path)
                  ? 'text-primary font-medium'
                  : 'text-text-muted hover:text-text'
              }`}
              aria-current={isActivePath(l.path) ? 'page' : undefined}
              onClick={() => navigate(l.path)}
            >
              {l.label}
            </button>
          ))}
        </div>

        <div className="flex items-center gap-1 shrink-0">
          <NotificationsMenu enabled={!!isAuthenticated && !!user?.role} />
          <Button
            variant="ghost"
            size="sm"
            className="shrink-0"
            leftIcon={<LogOut size={14} />}
            aria-label="Logout"
            onClick={() => { logout(); navigate('/login', { replace: true }); }}
          >
            <span className="hidden sm:inline" aria-hidden="true">Logout</span>
          </Button>
        </div>

        <button
          type="button"
          className="sm:hidden inline-flex items-center justify-center min-w-11 min-h-11 p-2 rounded-md text-text-muted hover:text-text hover:bg-surface cursor-pointer bg-transparent border-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
          onClick={() => setMenuOpen(!menuOpen)}
          aria-label="Toggle navigation menu"
          aria-expanded={menuOpen}
          aria-controls="mobile-nav-menu"
        >
          {menuOpen ? <X size={20} /> : <Menu size={20} />}
        </button>
      </div>

      {menuOpen && (
        <div
          id="mobile-nav-menu"
          className="sm:hidden border-t border-border-light bg-white px-4 py-2 space-y-1 pb-4"
          role="navigation"
          aria-label="Mobile"
        >
          {visibleLinks.map((l) => (
            <button
              type="button"
              key={l.path}
              className={`w-full text-left text-sm min-h-11 px-3 py-2.5 rounded-md transition-all duration-150 cursor-pointer focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary ${
                isActivePath(l.path)
                  ? 'text-primary font-medium bg-surface'
                  : 'text-text-muted hover:text-text hover:bg-surface'
              }`}
              aria-current={isActivePath(l.path) ? 'page' : undefined}
              onClick={() => { navigate(l.path); setMenuOpen(false); }}
            >
              {l.label}
            </button>
          ))}
          {isAuthenticated && user?.role && (
            <button
              type="button"
              className={`w-full text-left text-sm min-h-11 px-3 py-2.5 rounded-md transition-all duration-150 cursor-pointer focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary ${
                isActivePath('/notifications')
                  ? 'text-primary font-medium bg-surface'
                  : 'text-text-muted hover:text-text hover:bg-surface'
              }`}
              onClick={() => { navigate('/notifications'); setMenuOpen(false); }}
            >
              Notifications
            </button>
          )}
          <button
            type="button"
            className="w-full text-left text-sm min-h-11 px-3 py-2.5 rounded-md text-text-muted hover:text-text hover:bg-surface transition-all duration-150 cursor-pointer focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary flex items-center gap-2"
            onClick={() => { logout(); navigate('/login', { replace: true }); setMenuOpen(false); }}
          >
            <LogOut size={14} aria-hidden="true" />
            Logout
          </button>
        </div>
      )}
    </nav>
  );
}
