import { useState, useCallback, useEffect } from 'react';
import apiClient from '../services/apiClient';

export function useApi() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const execute = useCallback(async (apiCall) => {
    setLoading(true);
    setError(null);
    try {
      const response = await apiCall();
      const body = response.data;
      if (body.success) {
        return body.data !== undefined ? body.data : body;
      }
      const msg = body.errors?.join('. ') || body.message || 'Request failed';
      setError(msg);
      throw new Error(msg);
    } catch (err) {
      if (!err.response) setError(err.message);
      else if (err.response?.data?.errors || err.response?.data?.message) {
        setError(err.response.data.errors?.join('. ') || err.response.data.message);
      }
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  return { loading, error, execute };
}

export function useFetch(url, options = {}) {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const { execute } = useApi();

  const fetchData = useCallback(async (overrideUrl, overrideOptions) => {
    if (!(overrideUrl ?? url)) return;
    setLoading(true);
    setError(null);
    try {
      const result = await execute(() =>
        apiClient.get(overrideUrl ?? url, overrideOptions ?? options),
      );
      setData(result);
    } catch (err) {
      setError(err.response?.data?.message || err.message);
    } finally {
      setLoading(false);
    }
  }, [url, options, execute]);

  useEffect(() => { fetchData(); }, [fetchData]);

  return { data, loading, error, refetch: fetchData };
}
