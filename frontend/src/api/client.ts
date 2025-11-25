class ApiClient {
  private base = import.meta.env.VITE_API_URL || 'http://localhost:4000/api';
  private token: string | null = null;

  setToken(token: string | null) {
    this.token = token;
  }

  async request(path: string, options: RequestInit = {}) {
    const headers: HeadersInit = {
      'Content-Type': 'application/json',
      ...(options.headers || {})
    };
    if (this.token) headers['Authorization'] = `Bearer ${this.token}`;
    const res = await fetch(`${this.base}${path}`, { ...options, headers });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  }

  get(path: string) {
    return this.request(path);
  }

  post(path: string, body: any) {
    return this.request(path, { method: 'POST', body: JSON.stringify(body) });
  }

  put(path: string, body: any) {
    return this.request(path, { method: 'PUT', body: JSON.stringify(body) });
  }

  delete(path: string) {
    return this.request(path, { method: 'DELETE' });
  }
}

export const apiClient = new ApiClient();
