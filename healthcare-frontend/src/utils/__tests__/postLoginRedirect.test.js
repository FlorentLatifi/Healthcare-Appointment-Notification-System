import { describe, it, expect } from 'vitest';
import { postLoginPath } from '../postLoginRedirect';

describe('postLoginPath', () => {
  it('sends Admin to /admin', () => {
    expect(postLoginPath({ role: 'Admin' })).toBe('/admin');
    expect(postLoginPath({ user: { role: 'Admin' } })).toBe('/admin');
  });

  it('sends Doctor to doctor dashboard', () => {
    expect(postLoginPath({ role: 'Doctor', doctorId: null })).toBe('/doctor-dashboard');
    expect(postLoginPath({ role: 'Doctor', doctorId: 5 })).toBe('/doctor-dashboard');
  });

  it('sends unlinked Patient to create profile', () => {
    expect(postLoginPath({ role: 'Patient', patientId: null })).toBe('/create-patient');
    expect(postLoginPath({ role: 'Patient', patientId: 0 })).toBe('/create-patient');
  });

  it('sends linked Patient to dashboard', () => {
    expect(postLoginPath({ role: 'Patient', patientId: 12 })).toBe('/dashboard');
  });

  it('falls back to dashboard for unknown roles', () => {
    expect(postLoginPath({ role: '' })).toBe('/dashboard');
    expect(postLoginPath({})).toBe('/dashboard');
  });
});
