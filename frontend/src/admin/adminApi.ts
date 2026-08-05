import type { AuthenticatedRequest } from '../auth/Auth'

export type AdminRole = 'User' | 'Admin'
export type AdminUser = { id: string; username: string; firstName: string; lastName: string; role: AdminRole; isActive: boolean; createdAtUtc: string; updatedAtUtc: string }
export type Page<T> = { items: T[]; total: number; page: number; pageSize: number }
export type UserFilters = { page: number; pageSize?: number; search?: string; role?: AdminRole | ''; isActive?: '' | 'true' | 'false' }
export type CreateUserInput = { username: string; firstName: string; lastName: string; password: string; passwordConfirmation: string; role: AdminRole }
export type AdminLog = { id: string; createdAtUtc: string; username: string; status: 'Succeeded' | 'Failed'; language: 'Turkish' | 'English'; provider: string; model: string; promptVersion: string; durationMilliseconds: number; inputCharacterCount: number; outputCharacterCount: number; failureCategory: string | null; inputPreview: string | null; summaryPreview: string | null; privacyExplanation: string | null }
export type LogFilters = { page: number; pageSize?: number; status?: '' | 'Succeeded' | 'Failed'; language?: '' | 'Turkish' | 'English'; user?: string; fromUtc?: string; toUtc?: string }
export type StatisticsDay = { dateUtc: string; total: number; succeeded: number; failed: number; successRate: number; turkish: number; english: number; averageDurationMilliseconds: number; activeUsers: number }
export type ProviderStatistic = { provider: string; model: string; total: number }
export type FeedbackSummary = { useful: number; notUseful: number; satisfactionRate: number }
export type SevenDayStatistics = { fromUtc: string; toExclusiveUtc: string; days: StatisticsDay[]; providers: ProviderStatistic[]; activeUsers: number; feedback?: FeedbackSummary }
export type PromptInfo = { version: string; purpose: string; supportedLanguages: string[]; editable: false }
export type FieldErrors = Record<string, string[]>

export class AdminApiError extends Error {
  constructor(public status: number, message: string, public fieldErrors: FieldErrors = {}) { super(message) }
}

async function send<T>(request: AuthenticatedRequest, path: string, init?: RequestInit): Promise<T> {
  let response: Response
  try { response = await request(path, init) } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new AdminApiError(0, 'Sunucuya ulaşılamadı. Bağlantınızı kontrol edip yeniden deneyin.')
  }
  if (!response.ok) throw await parseError(response)
  if (response.status === 204) return undefined as T
  return await response.json() as T
}

async function parseError(response: Response) {
  const fallback = response.status === 403 ? 'Bu işlem için yönetici yetkiniz bulunmuyor.' : response.status === 401 ? 'Oturumunuz kullanılamıyor. Lütfen yeniden giriş yapın.' : 'İşlem şu anda tamamlanamadı.'
  try {
    const body = await response.json() as { detail?: unknown; errors?: unknown }
    const detail = typeof body.detail === 'string' && body.detail.length <= 400 ? body.detail : fallback
    const fieldErrors: FieldErrors = {}
    if (body.errors && typeof body.errors === 'object' && !Array.isArray(body.errors)) for (const [key, value] of Object.entries(body.errors)) {
      if (Array.isArray(value)) fieldErrors[key] = value.filter((item): item is string => typeof item === 'string' && item.length <= 300)
    }
    return new AdminApiError(response.status, detail, fieldErrors)
  } catch { return new AdminApiError(response.status, fallback) }
}

function query(values: object) {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(values)) if (value !== undefined && value !== '') params.set(key, String(value))
  const text = params.toString(); return text ? `?${text}` : ''
}

export const adminApi = {
  users: (request: AuthenticatedRequest, filters: UserFilters, signal?: AbortSignal) => send<Page<AdminUser>>(request, `/api/admin/users${query(filters)}`, { signal }),
  createUser: (request: AuthenticatedRequest, input: CreateUserInput) => send<AdminUser>(request, '/api/admin/users', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(input) }),
  setStatus: (request: AuthenticatedRequest, id: string, isActive: boolean) => send<AdminUser>(request, `/api/admin/users/${encodeURIComponent(id)}/status`, { method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ isActive }) }),
  resetPassword: (request: AuthenticatedRequest, id: string, password: string, passwordConfirmation: string) => send<void>(request, `/api/admin/users/${encodeURIComponent(id)}/password`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ password, passwordConfirmation }) }),
  logs: (request: AuthenticatedRequest, filters: LogFilters, signal?: AbortSignal) => send<Page<AdminLog>>(request, `/api/admin/logs${query(filters)}`, { signal }),
  statistics: (request: AuthenticatedRequest, signal?: AbortSignal) => send<SevenDayStatistics>(request, '/api/admin/statistics/seven-days', { signal }),
  promptInfo: (request: AuthenticatedRequest, signal?: AbortSignal) => send<PromptInfo>(request, '/api/admin/prompt-info', { signal }),
}
