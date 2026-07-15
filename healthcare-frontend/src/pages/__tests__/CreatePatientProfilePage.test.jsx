import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockNavigate, mockApplyProfileSession, mockApiClient } = vi.hoisted(() => ({
  mockNavigate: vi.fn(),
  mockApplyProfileSession: vi.fn(),
  mockApiClient: { post: vi.fn() },
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
}));

vi.mock('react-hot-toast', () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('../../services/apiClient', () => ({
  default: mockApiClient,
}));

vi.mock('../../context/AuthContext', () => ({
  useAuth: () => ({ applyProfileSession: mockApplyProfileSession }),
}));

import CreatePatientProfilePage from '../CreatePatientProfilePage';

describe('CreatePatientProfilePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockApplyProfileSession.mockResolvedValue({ patientId: 42 });
  });

  it('renders required fields', () => {
    render(<CreatePatientProfilePage />);
    expect(screen.getByRole('heading', { name: /create patient profile/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /create profile/i })).toBeInTheDocument();
  });

  it('applies session from create response then navigates to /dashboard', async () => {
    const sessionPayload = {
      id: 42,
      token: 'fresh-jwt',
      username: 'jdoe',
      role: 'Patient',
      patientId: 42,
      doctorId: null,
    };
    mockApiClient.post.mockResolvedValue({ data: { success: true, data: sessionPayload } });

    render(<CreatePatientProfilePage />);

    const inputs = document.querySelectorAll('input');
    await userEvent.type(inputs[0], 'John');
    await userEvent.type(inputs[1], 'Doe');
    await userEvent.type(inputs[2], 'john@test.com');
    await userEvent.type(inputs[3], '+355672345678');
    await userEvent.type(inputs[4], '1990-01-01');

    await userEvent.click(screen.getByRole('button', { name: /create profile/i }));

    await vi.waitFor(() => {
      expect(mockApiClient.post).toHaveBeenCalledWith('/Patients', expect.objectContaining({
        firstName: 'John',
        lastName: 'Doe',
        email: 'john@test.com',
      }));
    });

    expect(mockApplyProfileSession).toHaveBeenCalledWith(sessionPayload);
    expect(mockNavigate).toHaveBeenCalledWith('/dashboard');
  });
});
