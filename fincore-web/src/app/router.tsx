import { Navigate, Route, Routes } from 'react-router-dom';
import LoginPage from '../features/auth/pages/LoginPage';
import DashboardPage from '../features/dashboard/pages/DashboardPage';
import ProtectedRoute from '../routes/ProtectedRoute';
import GuestRoute from '../routes/GuestRoute';

export default function AppRouter() {
  return <Routes>
    <Route element={<GuestRoute />}><Route path="/login" element={<LoginPage />} /></Route>
    <Route element={<ProtectedRoute />}><Route path="/" element={<DashboardPage />} /></Route>
    <Route path="*" element={<Navigate to="/" replace />} />
  </Routes>;
}
