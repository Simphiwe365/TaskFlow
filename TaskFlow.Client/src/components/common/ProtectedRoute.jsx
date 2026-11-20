import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../../context/useAuth'

export default function ProtectedRoute({ requireAuth = true }) {
  const { isAuthenticated } = useAuth()
  const location = useLocation()

  if (requireAuth) {
    return isAuthenticated ? <Outlet /> : <Navigate to="/login" state={{ from: location }} replace />
  }

  return isAuthenticated ? <Navigate to="/dashboard" replace /> : <Outlet />
}
