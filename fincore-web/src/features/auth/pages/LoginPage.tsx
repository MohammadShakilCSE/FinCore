import AuthLayout from '../../../components/layout/AuthLayout';
import LoginForm from '../components/LoginForm';

export default function LoginPage() {
  return <AuthLayout>
    <div className="welcome-icon" aria-hidden="true">&#8599;</div>
          <div className="eyebrow dark">GOOD TO SEE YOU AGAIN</div>
          <h2>Welcome back.</h2>
          <p className="intro">Sign in to your account and pick up where you left off.</p>

    <LoginForm />

          <p className="account-help">New to FinCore? <span>Ask your administrator for an account.</span></p>
          <div className="session-note"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true"><rect x="5" y="10" width="14" height="11" rx="3" /><path d="M8 10V7a4 4 0 0 1 8 0v3M12 14v3" /></svg><span>Your session stays in this tab.<br />Sign out when you’re done on a shared device.</span></div>
  </AuthLayout>;
}
