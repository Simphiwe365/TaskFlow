import { useEffect, useMemo, useState } from 'react'
import { login as loginRequest, register as registerRequest } from '../services/authService'
import { AuthContext } from './useAuth'

const TOKEN_KEY = 'taskflow_token'

export function AuthProvider({ children }) {
  const [token, setToken] = useState(() => localStorage.getItem(TOKEN_KEY))

  useEffect(() => {
    const handleUnauthorized = () => {
      localStorage.removeItem(TOKEN_KEY)
      setToken(null)
    }

    window.addEventListener('taskflow:unauthorized', handleUnauthorized)
    return () => window.removeEventListener('taskflow:unauthorized', handleUnauthorized)
  }, [])

  const login = async (credentials) => {
    const response = await loginRequest(credentials)

    if (!response?.token) {
      throw new Error('The login response did not contain a valid token.')
    }

    localStorage.setItem(TOKEN_KEY, response.token)
    setToken(response.token)
    return response
  }

  const register = async (registration) => {
    const response = await registerRequest(registration)

    if (response?.token) {
      localStorage.setItem(TOKEN_KEY, response.token)
      setToken(response.token)
    }

    return response
  }

  const logout = () => {
    localStorage.removeItem(TOKEN_KEY)
    setToken(null)
  }

  const value = useMemo(() => ({
    token,
    isAuthenticated: Boolean(token),
    login,
    register,
    logout,
  }), [token])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
