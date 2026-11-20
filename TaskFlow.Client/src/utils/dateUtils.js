function isValidDateParts(year, month, day) {
  const date = new Date(year, month - 1, day)
  return date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day
}

export function getDateKey(value) {
  if (value === null || value === undefined || value === '') return null

  const rawValue = String(value).trim()
  const datePart = rawValue.match(/^(\d{4})-(\d{2})-(\d{2})(?:$|T)/)

  if (datePart) {
    const [, yearText, monthText, dayText] = datePart
    const year = Number(yearText)
    const month = Number(monthText)
    const day = Number(dayText)

    return isValidDateParts(year, month, day) ? `${yearText}-${monthText}-${dayText}` : null
  }

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null

  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export function toDateInputValue(value) {
  return getDateKey(value) ?? ''
}

export function formatDate(value, options = {}) {
  const dateKey = getDateKey(value)
  if (!dateKey) return null

  const [year, month, day] = dateKey.split('-').map(Number)
  return new Intl.DateTimeFormat('en', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    ...options,
  }).format(new Date(year, month - 1, day))
}

export function getTodayDateKey() {
  const today = new Date()
  const year = today.getFullYear()
  const month = String(today.getMonth() + 1).padStart(2, '0')
  const day = String(today.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}
