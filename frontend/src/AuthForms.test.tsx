import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const json = (body: unknown, status = 200) => Promise.resolve(new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }))
function renderRegistration() { render(<MemoryRouter initialEntries={['/register']}><App /></MemoryRouter>) }
function fillRegistration(password = 'Valid123!', confirmation = password) {
  fireEvent.change(screen.getByLabelText('Kullanıcı adı'), { target: { value: 'member' } })
  fireEvent.change(screen.getByLabelText('Ad'), { target: { value: 'Özge' } })
  fireEvent.change(screen.getByLabelText('Soyad'), { target: { value: "Kacmaz" } })
  fireEvent.change(screen.getByLabelText('Şifre'), { target: { value: password } })
  fireEvent.change(screen.getByLabelText('Şifre tekrarı'), { target: { value: confirmation } })
}

describe('authentication form hardening', () => {
  beforeEach(() => vi.stubGlobal('fetch', vi.fn(() => json({}, 401))))
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('shows the complete password policy and each local rule', async () => {
    renderRegistration(); expect(screen.getByText(/En az 8, en fazla 128 karakter/)).toBeVisible(); fillRegistration('short', 'short'); fireEvent.click(screen.getByRole('button', { name: 'Hesap oluştur' }))
    expect(await screen.findByText('Şifre en az 8 karakter olmalıdır.')).toBeVisible(); expect(screen.getByText('Şifre en az bir büyük harf içermelidir.')).toBeVisible(); expect(screen.getByText('Şifre en az bir rakam içermelidir.')).toBeVisible(); expect(screen.getByText('Şifre en az bir noktalama veya özel karakter içermelidir.')).toBeVisible(); expect(screen.getByLabelText('Şifre')).toHaveFocus()
  })

  it.each([
    ['valid123!', 'Şifre en az bir büyük harf içermelidir.'],
    ['VALID123!', 'Şifre en az bir küçük harf içermelidir.'],
    ['ValidPass!', 'Şifre en az bir rakam içermelidir.'],
    ['Valid1234', 'Şifre en az bir noktalama veya özel karakter içermelidir.'],
    [`Aa1!${'a'.repeat(125)}`, 'Şifre en fazla 128 karakter olmalıdır.'],
  ])('rejects password rule violation safely', async (password, message) => { renderRegistration(); fillRegistration(password, password); fireEvent.click(screen.getByRole('button', { name: 'Hesap oluştur' })); expect(await screen.findByText(message)).toBeVisible() })

  it('renders backend field errors on the matching accessible field', async () => {
    vi.mocked(fetch).mockImplementation(url => String(url).endsWith('/api/auth/register') ? json({ errors: { username: ['Bu kullanıcı adı kullanılıyor.'], password: ['Sunucu şifre doğrulaması.'] } }, 409) : json({}, 401))
    renderRegistration(); fillRegistration(); fireEvent.click(screen.getByRole('button', { name: 'Hesap oluştur' })); expect(await screen.findByText('Bu kullanıcı adı kullanılıyor.')).toBeVisible(); expect(screen.getByLabelText('Kullanıcı adı')).toHaveAttribute('aria-invalid', 'true'); expect(screen.getByText('Sunucu şifre doğrulaması.')).toBeVisible(); expect(screen.getByLabelText('Kullanıcı adı')).toHaveFocus()
  })

  it.each([[400, { unexpected: true }], [500, { title: 'Beklenmeyen hata' }]])('uses a safe fallback for malformed or unexpected status %s', async (status, body) => {
    vi.mocked(fetch).mockImplementation(url => String(url).endsWith('/api/auth/register') ? json(body, status) : json({}, 401)); renderRegistration(); fillRegistration(); fireEvent.click(screen.getByRole('button', { name: 'Hesap oluştur' })); expect(await screen.findByRole('alert')).toHaveTextContent('Şu anda kayıt işlemi tamamlanamadı')
  })

  it('prevents duplicate registration and navigates after success', async () => {
    let complete!: (response: Response) => void; const fetchMock = vi.mocked(fetch); fetchMock.mockImplementation(url => String(url).endsWith('/api/auth/register') ? new Promise(done => { complete = done }) : json({}, 401)); renderRegistration(); fillRegistration(); const submit = screen.getByRole('button', { name: 'Hesap oluştur' }); fireEvent.click(submit); expect(await screen.findByRole('button', { name: 'Bekleyin…' })).toBeDisabled(); fireEvent.click(submit); expect(fetchMock.mock.calls.filter(call => String(call[0]).endsWith('/api/auth/register'))).toHaveLength(1); complete(await json({}, 201)); expect(await screen.findByRole('heading', { name: 'Giriş yap' })).toBeVisible()
  })

  it('toggles every existing password field independently without submitting', async () => {
    renderRegistration(); const password = screen.getByLabelText('Şifre'); const confirmation = screen.getByLabelText('Şifre tekrarı'); fireEvent.change(password, { target: { value: 'Valid123!' } }); const toggles = screen.getAllByRole('button', { name: 'Şifreyi göster' }); expect(toggles).toHaveLength(2); expect(toggles[0].textContent).toBe(''); expect(toggles[0].querySelector('svg')).not.toBeNull(); fireEvent.click(toggles[0]); expect(password).toHaveAttribute('type', 'text'); expect(confirmation).toHaveAttribute('type', 'password'); expect(password).toHaveValue('Valid123!'); expect(fetch).not.toHaveBeenCalledWith(expect.stringContaining('/api/auth/register'), expect.anything()); cleanup(); render(<MemoryRouter initialEntries={['/login']}><App /></MemoryRouter>); const loginPassword = screen.getByLabelText('Şifre'); fireEvent.click(screen.getByRole('button', { name: 'Şifreyi göster' })); expect(loginPassword).toHaveAttribute('type', 'text'); expect(screen.getByRole('button', { name: 'Şifreyi gizle' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('submits trimmed identity fields but preserves the password exactly', async () => {
    const fetchMock = vi.mocked(fetch); fetchMock.mockImplementation(url => String(url).endsWith('/api/auth/register') ? json({}, 201) : json({}, 401)); renderRegistration(); fillRegistration(' Valid123! ', ' Valid123! '); fireEvent.change(screen.getByLabelText('Kullanıcı adı'), { target: { value: ' member ' } }); fireEvent.click(screen.getByRole('button', { name: 'Hesap oluştur' })); await waitFor(() => expect(fetchMock.mock.calls.some(call => String(call[0]).endsWith('/api/auth/register'))).toBe(true)); const call = fetchMock.mock.calls.find(item => String(item[0]).endsWith('/api/auth/register'))!; const body = JSON.parse(String((call[1] as RequestInit).body)); expect(body.username).toBe('member'); expect(body.password).toBe(' Valid123! ')
  })
})
