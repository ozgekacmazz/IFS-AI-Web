const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5099'

export const apiBaseUrl = configuredBaseUrl.replace(/\/$/, '')
