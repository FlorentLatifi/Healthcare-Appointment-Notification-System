import { describe, it, expect } from 'vitest';
import { parseApiError, firstFieldError, flattenApiErrors } from '../useApiError';

describe('parseApiError', () => {
  it('extracts FluentValidation / ValidationProblemDetails field errors', () => {
    const err = {
      response: {
        status: 400,
        data: {
          type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
          title: 'One or more validation errors occurred.',
          status: 400,
          errors: {
            Password: [
              'Password must be at least 12 characters',
              'Password must contain at least one uppercase letter',
            ],
            Email: ['Invalid email format'],
          },
        },
      },
    };

    const { fieldErrors, generalError } = parseApiError(err);

    expect(fieldErrors.password).toEqual([
      'Password must be at least 12 characters',
      'Password must contain at least one uppercase letter',
    ]);
    expect(fieldErrors.email).toEqual(['Invalid email format']);
    expect(generalError).toBeNull();
    expect(firstFieldError(fieldErrors, 'password')).toBe(
      'Password must be at least 12 characters',
    );
  });

  it('normalizes dotted property names', () => {
    const err = {
      response: {
        status: 400,
        data: { errors: { 'User.Username': ['Username is required'] } },
      },
    };
    const { fieldErrors } = parseApiError(err);
    expect(fieldErrors.username).toEqual(['Username is required']);
  });

  it('maps ApiResponse errors array and password messages to password field', () => {
    const err = {
      response: {
        status: 400,
        data: {
          success: false,
          message: 'Registration failed',
          errors: [
            'Password has been exposed in a data breach and cannot be used. Please choose a different password.',
          ],
        },
      },
    };
    const parsed = parseApiError(err);
    expect(parsed.fieldErrors.password?.[0]).toMatch(/data breach/i);
    expect(flattenApiErrors(parsed)).toMatch(/data breach/i);
  });

  it('handles invalidParams style', () => {
    const err = {
      response: {
        status: 422,
        data: {
          invalidParams: [{ name: 'role', reason: 'Role must be Patient or Doctor' }],
        },
      },
    };
    const { fieldErrors } = parseApiError(err);
    expect(fieldErrors.role).toEqual(['Role must be Patient or Doctor']);
  });

  it('returns network message when no response body', () => {
    const { fieldErrors, generalError } = parseApiError(new Error('Network Error'));
    expect(fieldErrors).toEqual({});
    expect(generalError).toBe('Network Error');
  });
});
