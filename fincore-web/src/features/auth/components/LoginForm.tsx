import { useState, type FormEvent } from 'react';
import { useAuth } from '../hooks/useAuth';
import Button from '../../../components/ui/Button';
import Input from '../../../components/ui/Input';

export default function LoginForm() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [visible, setVisible] = useState(false);

  const { login, busy, error } = useAuth();
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (await login({ email, password })) setPassword('');
  }
  return (<form onSubmit={submit} aria-busy={busy}>
            <label htmlFor="email">Email address</label>
            <Input id="email" name="email" type="email" autoComplete="username" placeholder="you@example.com" required value={email} disabled={busy} onChange={e => setEmail(e.target.value)} />
            <label htmlFor="password">Password</label>
            <div className="password-field"><Input id="password" name="password" type={visible ? 'text' : 'password'} autoComplete="current-password" placeholder="Enter your password" required value={password} disabled={busy} onChange={e => setPassword(e.target.value)} /><Button type="button" className="show-password" aria-label={visible ? 'Hide password' : 'Show password'} aria-pressed={visible} onClick={() => setVisible(!visible)}>{visible ? 'Hide' : 'Show'}</Button></div>
            {error && <div className="error" role="alert">{error}</div>}
            <Button type="submit" className="primary" disabled={busy}>{busy ? 'Signing you in…' : 'Sign in'}<span aria-hidden="true">{busy ? <span className="spinner" /> : '→'}</span></Button>
          </form>);
}
