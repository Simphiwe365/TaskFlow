import api from './api'

function getAuthError(error) {
  const data = error?.response?.data

  if (typeof data?.message === 'string' && data.message.trim()) {
    return new Error(data.message)
  }

  if (data?.errors) {
    const messages = Object.values(data.errors).flat().filter(Boolean)
    if (messages.length > 0) {
      return new Error(messages.join(' '))
    }
  }

  if (typeof data?.detail === 'string' && data.detail.trim()) {
    return new Error(data.detail)
  }

  if (typeof data?.title === 'string' && data.title.trim()) {
    return new Error(data.title)
  }

  if (error?.request) {
    return new Error('Unable to reach the TaskFlow API. Check that the backend is running.')
  }

  return new Error('An unexpected authentication error occurred.')
}

export async function register({ username, email, password }) {
  try {
    const response = await api.post('/auth/register', {
      username,
      email,
      password,
    })
    return response.data
  } catch (error) {
    throw getAuthError(error)
  }
}

export async function login({ identifier, password }) {
  try {
    const response = await api.post('/auth/login', {
      identifier,
      password,
    })
    return response.data
  } catch (error) {
    throw getAuthError(error)
  }
}
