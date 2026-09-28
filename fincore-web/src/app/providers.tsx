import { useState, type PropsWithChildren } from 'react';
import { QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from '../features/auth/hooks/useAuth';
import { createQueryClient } from '../lib/queryClient';

export default function Providers({ children }: PropsWithChildren) {
  const [queryClient] = useState(createQueryClient);
  return <QueryClientProvider client={queryClient}><AuthProvider>{children}</AuthProvider></QueryClientProvider>;
}
