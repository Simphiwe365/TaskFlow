import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Eye, EyeOff, ListChecks, LockKeyhole, Mail, UserRound } from 'lucide-react'
import { useAuth } from '../context/useAuth'

export default function Register() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [username, setUsername] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [passwordVisible, setPasswordVisible] = useState(false)
  const [confirmPasswordVisible, setConfirmPasswordVisible] = useState(false)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (event) => {
    event.preventDefault()
    if (loading) return

    setError('')

    if (!username.trim() || !email.trim() || !password) {
      setError('Username, email and password are required.')
      return
    }

    if (password.length < 8) {
      setError('Password must be at least 8 characters long.')
      return
    }

    if (password !== confirmPassword) {
      setError('Passwords do not match.')
      return
    }

    setLoading(true)

    try {
      const response = await register({ username: username.trim(), email: email.trim(), password })
      if (response?.token) {
        navigate('/dashboard', { replace: true })
      } else {
        navigate('/login', { replace: true })
      }
    } catch (submitError) {
      setError(submitError.message)
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="auth-page">
      <section className="auth-card register-card">
        <Link className="auth-brand" to="/login"><span className="brand-mark"><ListChecks size={20} /></span><span>TaskFlow</span></Link>
        <div className="auth-heading">
          <p className="eyebrow">Get started</p>
          <h1>Create your account</h1>
          <p>Set up your workspace and start organizing work.</p>
        </div>
        <form className="auth-form" onSubmit={handleSubmit}>
          <label>
            <span>Username</span>
            <div className="input-with-icon"><UserRound size={18} aria-hidden="true" /><input type="text" value={username} onChange={(event) => setUsername(event.target.value)} placeholder="Choose a username" autoComplete="username" required /></div>
          </label>
          <label>
            <span>Email</span>
            <div className="input-with-icon"><Mail size={18} aria-hidden="true" /><input type="email" value={email} onChange={(event) => setEmail(event.target.value)} placeholder="name@company.com" autoComplete="email" required /></div>
          </label>
          <label>
            <span>Password</span>
            <div className="input-with-icon">
              <LockKeyhole size={18} aria-hidden="true" />
              <input type={passwordVisible ? 'text' : 'password'} value={password} onChange={(event) => setPassword(event.target.value)} placeholder="Create a password" autoComplete="new-password" minLength={8} required />
              <button className="password-toggle" type="button" aria-label={passwordVisible ? 'Hide password' : 'Show password'} aria-pressed={passwordVisible} onClick={() => setPasswordVisible((visible) => !visible)}>
                {passwordVisible ? <EyeOff size={17} aria-hidden="true" /> : <Eye size={17} aria-hidden="true" />}
              </button>
            </div>
          </label>
          <label>
            <span>Confirm password</span>
            <div className="input-with-icon">
              <LockKeyhole size={18} aria-hidden="true" />
              <input type={confirmPasswordVisible ? 'text' : 'password'} value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} placeholder="Confirm your password" autoComplete="new-password" minLength={8} required />
              <button className="password-toggle" type="button" aria-label={confirmPasswordVisible ? 'Hide confirmation password' : 'Show confirmation password'} aria-pressed={confirmPasswordVisible} onClick={() => setConfirmPasswordVisible((visible) => !visible)}>
                {confirmPasswordVisible ? <EyeOff size={17} aria-hidden="true" /> : <Eye size={17} aria-hidden="true" />}
              </button>
            </div>
          </label>
          {error && <p className="auth-error" role="alert">{error}</p>}
          <button className="auth-submit" type="submit" disabled={loading}>{loading ? 'Creating account...' : 'Create account'}</button>
        </form>
        <p className="auth-switch">Already have an account? <Link to="/login">Sign in</Link></p>
      </section>
    </main>
  )
}
