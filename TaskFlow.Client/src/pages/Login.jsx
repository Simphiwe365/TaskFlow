import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ListChecks, LockKeyhole, Mail } from 'lucide-react'
import { useAuth } from '../context/useAuth'

export default function Login() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [identifier, setIdentifier] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (event) => {
    event.preventDefault()
    if (loading) return

    setError('')
    setLoading(true)

    try {
      await login({ identifier, password })
      navigate('/dashboard', { replace: true })
    } catch (submitError) {
      setError(submitError.message)
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="auth-page">
      <section className="auth-card">
        <Link className="auth-brand" to="/dashboard"><span className="brand-mark"><ListChecks size={20} /></span><span>TaskFlow</span></Link>
        <div className="auth-heading">
          <p className="eyebrow">Welcome back</p>
          <h1>Sign in to TaskFlow</h1>
          <p>Manage projects and keep your team moving.</p>
        </div>
        <form className="auth-form" onSubmit={handleSubmit}>
          <label>
            <span>Email or username</span>
            <div className="input-with-icon"><Mail size={18} /><input type="text" value={identifier} onChange={(event) => setIdentifier(event.target.value)} placeholder="name@company.com" autoComplete="username" required /></div>
          </label>
          <label>
            <span>Password</span>
            <div className="input-with-icon"><LockKeyhole size={18} /><input type="password" value={password} onChange={(event) => setPassword(event.target.value)} placeholder="Enter your password" autoComplete="current-password" required /></div>
          </label>
          {error && <p className="auth-error" role="alert">{error}</p>}
          <div className="auth-options"><label><input type="checkbox" /> Remember me</label><button type="button">Forgot password?</button></div>
          <button className="auth-submit" type="submit" disabled={loading}>{loading ? 'Signing in...' : 'Sign in'}</button>
        </form>
        <p className="auth-switch">New to TaskFlow? <Link to="/register">Create an account</Link></p>
      </section>
    </main>
  )
}
