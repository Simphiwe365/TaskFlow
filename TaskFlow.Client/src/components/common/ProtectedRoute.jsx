import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../../context/useAuth'

export default function ProtectedRoute({ requireAuth = true }) {
  const { isAuthenticated } = useAuth()

  if (requireAuth) {
    return isAuthenticated ? <Outlet /> : <Navigate to="/login" replace />
  }

  return isAuthenticated ? <Navigate to="/dashboard" replace /> : <Outlet />
}
