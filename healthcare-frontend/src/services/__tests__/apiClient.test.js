import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import MockAdapter from 'axios-mock-adapter';
import apiClient, { setTokenGetter, setTokenSetter, onAuthCleared } from '../apiClient';

describe('apiClient response interceptor', () => {
  let mock;
  let currentToken;
  const getToken = vi.fn(() => currentToken);
  const setToken = vi.fn((token) => { currentToken = token; });
  const clearAuth = vi.fn();

  beforeEach(() => {
    currentToken = 'initial-token';
    mock = new MockAdapter(apiClient);
    setTokenGetter(getToken);
    setTokenSetter(setToken);
    onAuthCleared(clearAuth);
  });

  afterEach(() => {
    mock.restore();
    vi.clearAllMocks();
  });

  it('concurrent 401s trigger exactly one refresh call and both requests retry with new token', async () => {
    const newToken = 'refreshed-token';
    let refreshCount = 0;

    mock.onGet('/api/data').replyOnce(401);
    mock.onGet('/api/data').reply((config) => {
      expect(config.headers.Authorization).toBe(`Bearer ${newToken}`);
      return [200, { data: 'ok' }];
    });

    mock.onGet('/api/other').replyOnce(401);
    mock.onGet('/api/other').reply((config) => {
      expect(config.headers.Authorization).toBe(`Bearer ${newToken}`);
      return [200, { data: 'also-ok' }];
    });

    mock.onPost('/Auth/refresh').reply(() => {
      refreshCount++;
      return [200, { success: true, data: { token: newToken } }];
    });

    const [res1, res2] = await Promise.all([
      apiClient.get('/api/data'),
      apiClient.get('/api/other'),
    ]);

    expect(refreshCount).toBe(1);
    expect(res1.data).toEqual({ data: 'ok' });
    expect(res2.data).toEqual({ data: 'also-ok' });
    expect(setToken).toHaveBeenCalledWith(newToken);
  });

  it('single 401 retries original request with new token on successful refresh', async () => {
    const newToken = 'fresh-token';

    mock.onGet('/api/profile').replyOnce(401);
    mock.onGet('/api/profile').reply((config) => {
      expect(config.headers.Authorization).toBe(`Bearer ${newToken}`);
      return [200, { id: 1, name: 'test' }];
    });

    mock.onPost('/Auth/refresh').reply(200, {
      success: true,
      data: { token: newToken },
    });

    const response = await apiClient.get('/api/profile');

    expect(response.data).toEqual({ id: 1, name: 'test' });
    expect(setToken).toHaveBeenCalledWith(newToken);
  });

  it('failed refresh clears auth state instead of retrying indefinitely', async () => {
    mock.onGet('/api/profile').reply(401);

    mock.onPost('/Auth/refresh').reply(401);

    await expect(apiClient.get('/api/profile')).rejects.toThrow();

    expect(clearAuth).toHaveBeenCalledTimes(1);
    expect(setToken).not.toHaveBeenCalled();
  });

  it('401 from /Auth/login does not trigger refresh', async () => {
    mock.onPost('/Auth/login').reply(401);

    await expect(
      apiClient.post('/Auth/login', { username: 'u', password: 'p' }),
    ).rejects.toThrow();

    expect(setToken).not.toHaveBeenCalled();
    expect(clearAuth).not.toHaveBeenCalled();
  });

  it('401 from /Auth/refresh does not trigger refresh loop', async () => {
    mock.onPost('/Auth/refresh').reply(401);

    await expect(apiClient.post('/Auth/refresh')).rejects.toThrow();

    expect(setToken).not.toHaveBeenCalled();
    expect(clearAuth).not.toHaveBeenCalled();
  });
});
