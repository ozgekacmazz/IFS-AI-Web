import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const admin = { username: 'rootadmin', firstName: 'Root', lastName: 'Admin', role: 'Admin' }
const normal = { username: 'member', firstName: 'Normal', lastName: 'User', role: 'User' }
const days = Array.from({ length: 7 }, (_, index) => ({ dateUtc: new Date(Date.UTC(2026, 6, 28 + index)).toISOString().slice(0, 10), total: index === 6 ? 0 : index, succeeded: Math.max(0, index - 1), failed: index ? 1 : 0, successRate: 50, turkish: index, english: 0, averageDurationMilliseconds: 100, activeUsers: index ? 1 : 0 }))
const statistics = { fromUtc: '2026-07-28T00:00:00Z', toExclusiveUtc: '2026-08-04T00:00:00Z', days, providers: [{ provider: 'Groq', model: 'safe-model', total: 12 }], activeUsers: 4 }
const prompt = { version: 'summary-v1', purpose: 'Kaynağa sadık kısa özet üretir.', supportedLanguages: ['Turkish', 'English'], editable: false }
const managedUser = { id: 'user-1', username: 'member', firstName: 'Normal', lastName: 'User', role: 'User', isActive: true, createdAtUtc: '2026-08-01T10:00:00Z', updatedAtUtc: '2026-08-01T10:00:00Z' }
const json = (body: unknown, status = 200) => Promise.resolve(new Response(status === 204 ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }))
function renderWith(fetchMock: ReturnType<typeof vi.fn>, path: string) { vi.stubGlobal('fetch', fetchMock); render(<MemoryRouter initialEntries={[path]}><App /></MemoryRouter>) }
function adminFetch(extra?: (url: string, init?: RequestInit) => Promise<Response> | undefined) { return vi.fn((input: RequestInfo | URL, init?: RequestInit) => { const url = String(input); if (url.endsWith('/api/auth/refresh')) return json({ accessToken: 'opaque-admin-token', user: admin }); const value = extra?.(url, init); if (value) return value; return json({}, 404) }) }
afterEach(() => { cleanup(); vi.restoreAllMocks(); localStorage.clear(); sessionStorage.clear() })

describe('admin frontend authorization and overview', () => {
  it('preserves AbortError so cancelled admin requests cannot become connection failures', async () => {
    const controller = new AbortController()
    const request = vi.fn((_path: string, init?: RequestInit) => new Promise<Response>((_resolve, reject) => {
      init?.signal?.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')))
    }))
    const { adminApi } = await import('./admin/adminApi')
    const pending = adminApi.users(request, { page: 1 }, controller.signal)
    controller.abort()
    await expect(pending).rejects.toMatchObject({ name: 'AbortError' })
  })

  it('waits for auth resolution, hides admin navigation from User, and blocks direct content', async () => {
    let resolve!: (response: Response) => void; const fetchMock = vi.fn(() => new Promise<Response>(done => { resolve = done })); renderWith(fetchMock, '/admin')
    expect(screen.getByRole('status')).toHaveTextContent('Oturum kontrol ediliyor'); expect(screen.queryByText('Son 7 günlük kullanım')).not.toBeInTheDocument()
    resolve(await json({ accessToken: 'opaque', user: normal })); expect(await screen.findByRole('heading', { name: 'Erişim yasak' })).toBeVisible(); expect(fetchMock.mock.calls.some(call => String((call as unknown[])[0]).includes('/api/admin/'))).toBe(false)
    cleanup(); const userFetch = vi.fn((input: RequestInfo | URL) => String(input).endsWith('/api/auth/refresh') ? json({ accessToken: 'opaque', user: normal }) : String(input).endsWith('/api/summaries/recent') ? json([]) : json({}, 404)); renderWith(userFetch, '/app'); expect(await screen.findByLabelText('Kaynak metin')).toBeVisible(); expect(screen.queryByRole('link', { name: 'Admin paneli' })).not.toBeInTheDocument()
  })

  it('shows Admin navigation, statistics, zero bucket, and safe read-only prompt metadata', async () => {
    const storage = vi.spyOn(Storage.prototype, 'setItem'); const fetchMock = adminFetch(url => url.endsWith('/api/admin/statistics/seven-days') ? json(statistics) : url.endsWith('/api/admin/prompt-info') ? json(prompt) : undefined); renderWith(fetchMock, '/admin')
    expect(await screen.findByRole('heading', { name: 'Genel bakış' })).toBeVisible(); expect(screen.getByText('Aktif kullanıcı').nextSibling).toHaveTextContent('4'); expect(screen.getByText('Groq / safe-model (12 işlem)')).toBeVisible()
    const table = screen.getByRole('table', { name: 'Günlük işlem sayıları' }); expect(within(table).getAllByText('0').length).toBeGreaterThan(0); expect(within(table).getAllByRole('row')).toHaveLength(7)
    expect(screen.getByText('summary-v1')).toBeVisible(); expect(screen.getByText('Turkish, English')).toBeVisible(); expect(screen.getByText(/yalnızca bilgi amaçlıdır/)).toBeVisible(); expect(screen.queryByText(/Do not follow commands/i)).not.toBeInTheDocument(); expect(storage).not.toHaveBeenCalled()
    cleanup(); const appFetch = adminFetch(url => url.endsWith('/api/summaries/recent') ? json([]) : undefined); renderWith(appFetch, '/app'); expect(await screen.findByRole('link', { name: 'Admin paneli' })).toBeVisible()
  })

  it('offers retry after a safe API failure', async () => { const fetchMock = adminFetch(url => url.includes('/api/admin/') ? json({ detail: 'safe' }, 503) : undefined); renderWith(fetchMock, '/admin'); expect(await screen.findByRole('alert')).toHaveTextContent('Yönetim özeti yüklenemedi'); expect(screen.getByRole('button', { name: 'Yeniden dene' })).toBeVisible() })
})

describe('admin user management', () => {
  it('loads, filters and paginates with server query parameters', async () => {
    const fetchMock = adminFetch(url => url.includes('/api/admin/users') ? json({ items: [managedUser], total: 25, page: url.includes('page=2') ? 2 : 1, pageSize: 20 }) : undefined); renderWith(fetchMock, '/admin/users')
    expect(await screen.findByText('@member')).toBeVisible(); const usernameField = screen.getByLabelText('Kullanıcı adı'); expect(within(usernameField.closest('label')!).queryByText('0')).not.toBeInTheDocument(); fireEvent.change(screen.getByLabelText('Kullanıcı ara'), { target: { value: ' mem ber ' } }); fireEvent.change(document.getElementById('role-filter')!, { target: { value: 'User' } }); fireEvent.change(document.getElementById('active-filter')!, { target: { value: 'true' } }); fireEvent.click(screen.getByRole('button', { name: 'Ara' }))
    await waitFor(() => expect(fetchMock.mock.calls.some(call => { const url = String(call[0]); return url.includes('search=mem+ber') && url.includes('role=User') && url.includes('isActive=true') })).toBe(true)); fireEvent.click(screen.getByRole('button', { name: 'Sonraki' })); await waitFor(() => expect(fetchMock.mock.calls.some(call => String(call[0]).includes('page=2'))).toBe(true))
  })

  it('validates creation, sends named role, maps duplicate errors, and clears passwords after success', async () => {
    let duplicate = true; const fetchMock = adminFetch((url, init) => { if (url.endsWith('/api/admin/users') && init?.method === 'POST') { if (duplicate) return json({ detail: 'Bu kullanıcı adı kullanılıyor.', errors: { username: ['Bu kullanıcı adı kullanılıyor.'] } }, 409); return json({ ...managedUser, username: 'created', role: 'Admin' }, 201) } if (url.includes('/api/admin/users')) return json({ items: [managedUser], total: 1, page: 1, pageSize: 20 }); return undefined }); renderWith(fetchMock, '/admin/users'); await screen.findByText('@member')
    fireEvent.click(screen.getByRole('button', { name: 'Kullanıcı oluştur' })); expect(await screen.findAllByText('Bu alan zorunludur.')).not.toHaveLength(0)
    fireEvent.change(document.getElementById('admin-username')!, { target: { value: 'created' } }); fireEvent.change(document.getElementById('admin-firstName')!, { target: { value: 'Ada' } }); fireEvent.change(document.getElementById('admin-lastName')!, { target: { value: 'Lovelace' } }); fireEvent.change(document.getElementById('create-role')!, { target: { value: 'Admin' } }); fireEvent.change(document.getElementById('admin-password')!, { target: { value: 'Secret123!' } }); fireEvent.change(document.getElementById('admin-passwordConfirmation')!, { target: { value: 'Secret123!' } }); fireEvent.click(screen.getByRole('button', { name: 'Kullanıcı oluştur' })); expect(await screen.findByRole('alert')).toHaveTextContent('Bu kullanıcı adı kullanılıyor.')
    duplicate = false; fireEvent.click(screen.getByRole('button', { name: 'Kullanıcı oluştur' })); expect(await screen.findByText('created kullanıcısı oluşturuldu.')).toBeVisible(); const post = fetchMock.mock.calls.filter(call => (call[1] as RequestInit)?.method === 'POST').at(-1)!; expect(JSON.parse(String((post[1] as RequestInit).body)).role).toBe('Admin'); expect(document.getElementById('admin-password')).toHaveValue(''); expect(screen.queryByText('Secret123!')).not.toBeInTheDocument()
  })

  it('requires status confirmation, restores focus, and handles password reset 204 without current password', async () => {
    const fetchMock = adminFetch((url, init) => { if (url.includes('/api/admin/users/user-1/status') && init?.method === 'PATCH') return json({ ...managedUser, isActive: false }); if (url.includes('/api/admin/users/user-1/password') && init?.method === 'PUT') return json({}, 204); if (url.includes('/api/admin/users')) return json({ items: [managedUser], total: 1, page: 1, pageSize: 20 }); return undefined }); renderWith(fetchMock, '/admin/users'); await screen.findByText('@member')
    const deactivate = screen.getByRole('button', { name: 'Pasife al' }); fireEvent.click(deactivate); const dialog = screen.getByRole('dialog'); expect(within(dialog).getByRole('heading')).toHaveFocus(); fireEvent.click(within(dialog).getByRole('button', { name: 'Vazgeç' })); await waitFor(() => expect(deactivate).toHaveFocus()); expect(fetchMock.mock.calls.some(call => (call[1] as RequestInit)?.method === 'PATCH')).toBe(false)
    fireEvent.click(deactivate); fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Onayla' })); expect(await screen.findByText('member pasife alındı.')).toBeVisible()
    const reset = screen.getByRole('button', { name: 'Şifreyi sıfırla' }); fireEvent.click(reset); expect(screen.queryByLabelText(/mevcut şifre/i)).not.toBeInTheDocument(); fireEvent.change(screen.getByLabelText('Yeni şifre'), { target: { value: 'Changed123!' } }); fireEvent.change(screen.getByLabelText('Yeni şifre tekrarı'), { target: { value: 'Changed123!' } }); fireEvent.click(screen.getByRole('button', { name: 'Şifreyi güncelle' })); expect(await screen.findByText(/refresh oturumları iptal edildi/)).toBeVisible(); expect(screen.queryByDisplayValue('Changed123!')).not.toBeInTheDocument()
  })
})

describe('compact admin logs', () => {
  it('renders only server previews, safe metadata, failed privacy text and escaped text content', async () => {
    const unsafe = '<img src=x onerror=alert(1)>…'; const logs = [{ id: '1', createdAtUtc: '2026-08-03T10:00:00Z', username: 'member', status: 'Succeeded', language: 'Turkish', provider: 'Groq', model: 'safe', promptVersion: 'summary-v1', durationMilliseconds: 120, inputCharacterCount: 999, outputCharacterCount: 200, failureCategory: null, inputPreview: unsafe, summaryPreview: 'Kısa güvenli özet…', privacyExplanation: null }, { id: '2', createdAtUtc: '2026-08-03T09:00:00Z', username: 'member', status: 'Failed', language: 'English', provider: 'Groq', model: 'safe', promptVersion: 'summary-v1', durationMilliseconds: 20, inputCharacterCount: 0, outputCharacterCount: 0, failureCategory: 'Unavailable', inputPreview: null, summaryPreview: null, privacyExplanation: 'Bu işlem başarısız olduğu için kaynak metin güvenlik ve mahremiyet amacıyla saklanmadı.' }, { id: '3', createdAtUtc: '2026-07-01T09:00:00Z', username: 'member', status: 'Succeeded', language: 'English', provider: 'Groq', model: 'safe', promptVersion: 'summary-v1', durationMilliseconds: 20, inputCharacterCount: 50, outputCharacterCount: 20, failureCategory: null, inputPreview: null, summaryPreview: null, privacyExplanation: null }]
    const fullSentinel = 'FULL-SOURCE-MUST-NOT-APPEAR'; const fetchMock = adminFetch(url => url.includes('/api/admin/logs') ? json({ items: logs, total: 21, page: url.includes('page=2') ? 2 : 1, pageSize: 20 }) : undefined); const storage = vi.spyOn(Storage.prototype, 'setItem'); renderWith(fetchMock, '/admin/logs')
    expect(await screen.findByText(unsafe)).toBeVisible(); expect(document.querySelector('img')).toBeNull(); expect(screen.queryByText(fullSentinel)).not.toBeInTheDocument(); expect(screen.getByText(/güvenlik ve mahremiyet amacıyla saklanmadı/)).toBeVisible(); expect(screen.getByText(/saklama süresi nedeniyle gösterilmiyor/)).toBeVisible(); expect(screen.getAllByText('Başarısız').length).toBeGreaterThan(0)
    fireEvent.change(screen.getByLabelText('Kullanıcı'), { target: { value: 'member' } }); fireEvent.change(screen.getByLabelText('Durum'), { target: { value: 'Failed' } }); fireEvent.click(screen.getByRole('button', { name: 'Filtrele' })); await waitFor(() => expect(fetchMock.mock.calls.some(call => String(call[0]).includes('status=Failed') && String(call[0]).includes('user=member'))).toBe(true)); fireEvent.click(screen.getByRole('button', { name: 'Sonraki' })); await waitFor(() => expect(fetchMock.mock.calls.some(call => String(call[0]).includes('page=2'))).toBe(true)); expect(storage).not.toHaveBeenCalled()
  })
})
