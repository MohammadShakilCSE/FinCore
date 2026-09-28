import { createContext, createElement, useContext, useEffect, useRef, useState, type PropsWithChildren } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { signIn } from '../api/authApi';
import type { LoginInput, Session } from '../types/auth.types';
import { getErrorMessage } from '../../../utils/error';
import { createTokenStorage } from '../../../lib/tokenStorage';

type AuthContextValue = {
  session: Session | null;
  busy: boolean;
  error: string;
  login: (input: LoginInput) => Promise<boolean>;
  logout: () => void;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: PropsWithChildren) {
  const [session, setSession] = useState<Session | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const pending = useRef(false);
  const [tokens] = useState(createTokenStorage);
  const queryClient = useQueryClient();

  useEffect(() => {
    document.title = session ? 'FinCore | Your account' : 'FinCore | Sign in';
    if (!session) return;
    const timer = window.setTimeout(() => {
      tokens.clear();
      queryClient.clear();
      setSession(null);
      setError('Your session has expired. Please sign in again.');
    }, Math.max(0, session.expiresAt - Date.now()));
    return () => window.clearTimeout(timer);
  }, [session, tokens, queryClient]);

  async function login({ email, password }: LoginInput) {
    if (pending.current) return false;
    pending.current = true;
    setBusy(true);
    setError('');
    try {
      const nextSession = await signIn(email, password);
      tokens.set(nextSession.accessToken);
      setSession(nextSession);
      return true;
    } catch (failure) {
      setError(getErrorMessage(failure));
      return false;
    } finally {
      pending.current = false;
      setBusy(false);
    }
  }

  function logout() {
    tokens.clear();
    queryClient.clear();
    setSession(null);
    setError('');
  }

  return createElement(AuthContext.Provider, { value: { session, busy, error, login, logout } }, children);
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used inside AuthProvider.');
  return context;
}
