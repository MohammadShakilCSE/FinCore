import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../features/auth/hooks/useAuth';

export default function GuestRoute() {
  return useAuth().session ? <Navigate to="/" replace /> : <Outlet />;
}
