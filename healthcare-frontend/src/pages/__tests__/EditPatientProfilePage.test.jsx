import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockNavigate, mockApiClient, mockLogout } = vi.hoisted(() => ({
  mockNavigate: vi.fn(),
  mockApiClient: { get: vi.fn(), put: vi.fn(), delete: vi.fn() },
  mockLogout: vi.fn().mockResolvedValue(undefined),
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
  useAuth: () => ({ patientId: 42, logout: mockLogout }),
}));

import EditPatientProfilePage from '../EditPatientProfilePage';

const profile = {
  id: 42,
  firstName: 'Jane',
  lastName: 'Doe',
  email: 'jane@test.com',
  phoneNumber: '+355672345678',
  dateOfBirth: '1990-01-15T00:00:00Z',
  gender: 'Female',
  street: '1 Main',
  city: 'Tirana',
  state: 'AL',
  postalCode: '1001',
  country: 'Albania',
};

describe('EditPatientProfilePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockApiClient.get.mockResolvedValue({ data: { success: true, data: profile } });
    mockApiClient.put.mockResolvedValue({ data: { success: true } });
    mockApiClient.delete.mockResolvedValue({ status: 204 });
  });

  it('loads and prefills the form, then saves via PUT', async () => {
    render(<EditPatientProfilePage />);

    await waitFor(() => {
      expect(mockApiClient.get).toHaveBeenCalledWith('/Patients/42');
    });

    expect(await screen.findByDisplayValue('Jane')).toBeInTheDocument();
    expect(screen.getByDisplayValue('jane@test.com')).toBeInTheDocument();

    const first = screen.getByLabelText(/first name/i);
    await userEvent.clear(first);
    await userEvent.type(first, 'Janet');

    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));

    await waitFor(() => {
      expect(mockApiClient.put).toHaveBeenCalledWith(
        '/Patients/42',
        expect.objectContaining({ firstName: 'Janet', email: 'jane@test.com' }),
      );
    });
    expect(mockNavigate).toHaveBeenCalledWith('/dashboard');
  });

  it('deletes profile, logs out, and redirects to login', async () => {
    render(<EditPatientProfilePage />);
    await screen.findByDisplayValue('Jane');

    await userEvent.click(screen.getByRole('button', { name: /delete profile/i }));
    await userEvent.click(screen.getByRole('button', { name: /delete and sign out/i }));

    await waitFor(() => {
      expect(mockApiClient.delete).toHaveBeenCalledWith('/Patients/42');
      expect(mockLogout).toHaveBeenCalled();
      expect(mockNavigate).toHaveBeenCalledWith('/login', { replace: true });
    });
  });
});
