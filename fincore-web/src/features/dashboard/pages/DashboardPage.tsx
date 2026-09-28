import AuthLayout from '../../../components/layout/AuthLayout';
import { useAuth } from '../../auth/hooks/useAuth';

// Account confirmation until the wallet dashboard is implemented.
export default function DashboardPage() {
  const { session, logout } = useAuth();
  if (!session) return null;
  return <AuthLayout><div className="welcome-icon" aria-hidden="true">&#10003;</div><div className="success" role="status">
          <div className="eyebrow dark">YOU’RE ALL SET</div>
          <h2>Welcome, {session.user.name}.</h2>
          <p>You’re signed in to your FinCore account.</p>
          <div className="account"><span>YOUR ACCOUNT</span><strong>{session.user.email}</strong><small>Authenticated and ready for what’s next.</small></div>
          <button className="primary" onClick={logout}>Sign out <span aria-hidden="true">↗</span></button>
        </div></AuthLayout>;
}
