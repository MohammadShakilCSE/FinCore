import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../features/auth/hooks/useAuth';

export default function ProtectedRoute() {
  return useAuth().session ? <Outlet /> : <Navigate to="/login" replace />;
}
