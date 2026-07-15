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
  useAuth: () => ({ doctorId: 7, logout: mockLogout }),
}));

import EditDoctorProfilePage from '../EditDoctorProfilePage';

const profile = {
  id: 7,
  firstName: 'Ada',
  lastName: 'Lovelace',
  email: 'ada@clinic.com',
  phoneNumber: '+355672345678',
  licenseNumber: 'MED-12345',
  specialties: ['Cardiology'],
  consultationFeeAmount: 80,
  consultationFeeCurrency: 'USD',
  yearsOfExperience: 12,
};

describe('EditDoctorProfilePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockApiClient.get.mockResolvedValue({ data: { success: true, data: profile } });
    mockApiClient.put.mockResolvedValue({ data: { success: true } });
    mockApiClient.delete.mockResolvedValue({ status: 204 });
  });

  it('loads doctor profile and saves via PUT', async () => {
    render(<EditDoctorProfilePage />);

    await waitFor(() => {
      expect(mockApiClient.get).toHaveBeenCalledWith('/Doctors/7');
    });

    expect(await screen.findByDisplayValue('Ada')).toBeInTheDocument();
    expect(screen.getByDisplayValue('MED-12345')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));

    await waitFor(() => {
      expect(mockApiClient.put).toHaveBeenCalledWith(
        '/Doctors/7',
        expect.objectContaining({
          firstName: 'Ada',
          licenseNumber: 'MED-12345',
          consultationFeeAmount: 80,
          yearsOfExperience: 12,
        }),
      );
    });
    expect(mockNavigate).toHaveBeenCalledWith('/doctor-dashboard');
  });

  it('deletes doctor profile and logs out', async () => {
    render(<EditDoctorProfilePage />);
    await screen.findByDisplayValue('Ada');

    await userEvent.click(screen.getByRole('button', { name: /delete profile/i }));
    await userEvent.click(screen.getByRole('button', { name: /delete and sign out/i }));

    await waitFor(() => {
      expect(mockApiClient.delete).toHaveBeenCalledWith('/Doctors/7');
      expect(mockLogout).toHaveBeenCalled();
      expect(mockNavigate).toHaveBeenCalledWith('/login', { replace: true });
    });
  });
});
