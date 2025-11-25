import React, { createContext, useContext, useEffect, useState } from 'react';
import { apiClient } from '../api/client';
import { Role } from '../types';

type User = { id: number; email: string; role: Role; name: string; employeeId?: number | null };

interface AuthContextValue {
  user: User | null;
  token: string | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export const AuthProvider = ({ children }: { children: React.ReactNode }) => {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);

  const saveSession = (nextToken: string, profile: User) => {
    setUser(profile);
    setToken(nextToken);
    apiClient.setToken(nextToken);
    localStorage.setItem('auth', JSON.stringify({ token: nextToken, user: profile }));
  };

  useEffect(() => {
    const stored = localStorage.getItem('auth');
    if (stored) {
      const parsed = JSON.parse(stored);
      const tokenValue: string = parsed.token;
      apiClient.setToken(tokenValue);
      setToken(tokenValue);
      apiClient
        .get('/auth/me')
        .then((profile) => saveSession(tokenValue, profile))
        .catch(() => logout());
    }
  }, []);

  const login = async (email: string, password: string) => {
    const res = await apiClient.post('/auth/login', { email, password });
    saveSession(res.token, res.user);
  };

  const logout = () => {
    setUser(null);
    setToken(null);
    apiClient.setToken(null);
    localStorage.removeItem('auth');
  };

  return <AuthContext.Provider value={{ user, token, login, logout }}>{children}</AuthContext.Provider>;
};

export const useAuth = () => {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within provider');
  return ctx;
};
