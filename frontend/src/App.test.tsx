import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const json = (body: unknown, status = 200) => Promise.resolve(new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }))
describe('Phase 2 authentication UI', () => {
  beforeEach(() => { vi.stubGlobal('fetch', vi.fn(() => json({}, 401))) })
  afterEach(() => { cleanup(); vi.restoreAllMocks() })
  it('keeps registration free of email and role inputs and validates confirmation', async () => {
    render(<MemoryRouter initialEntries={['/register']}><App /></MemoryRouter>)
    expect(screen.queryByLabelText(/e-posta/i)).not.toBeInTheDocument(); expect(screen.queryByLabelText(/rol/i)).not.toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Şifre'), { target: { value: 'uzun-bir-parola' } }); fireEvent.change(screen.getByLabelText('Şifre tekrarı'), { target: { value: 'farkli-bir-parola' } }); fireEvent.click(screen.getByRole('button', { name: 'Hesap oluştur' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Şifreler eşleşmiyor')
  })
  it('waits for restoration before redirecting anonymous users', async () => {
    render(<MemoryRouter initialEntries={['/app']}><App /></MemoryRouter>); expect(screen.getByRole('status')).toHaveTextContent('Oturum kontrol ediliyor'); expect(await screen.findByRole('heading', { name: 'Giriş yap' })).toBeVisible()
  })
  it('shows generic login failure', async () => {
    render(<MemoryRouter initialEntries={['/login']}><App /></MemoryRouter>); fireEvent.change(screen.getByLabelText('Kullanıcı adı'), { target: { value: 'someone' } }); fireEvent.change(screen.getByLabelText('Şifre'), { target: { value: 'wrong-password' } }); fireEvent.click(screen.getByRole('button', { name: 'Giriş yap' })); expect(await screen.findByRole('alert')).toHaveTextContent('Kullanıcı adı veya şifre geçersiz')
  })
  it('establishes authenticated UI without browser storage', async () => {
    const fetchMock = vi.mocked(fetch); fetchMock.mockImplementationOnce(() => json({}, 401)).mockImplementationOnce(() => json({ accessToken: 'opaque', user: { username: 'test', firstName: 'Test', lastName: 'User', role: 'User' } }))
    const storageSpy = vi.spyOn(Storage.prototype, 'setItem'); render(<MemoryRouter initialEntries={['/login']}><App /></MemoryRouter>); await waitFor(() => expect(fetchMock).toHaveBeenCalled())
    fireEvent.change(screen.getByLabelText('Kullanıcı adı'), { target: { value: 'test' } }); fireEvent.change(screen.getByLabelText('Şifre'), { target: { value: 'long-passphrase' } }); fireEvent.click(screen.getByRole('button', { name: 'Giriş yap' })); expect(await screen.findByText(/Kullanıcı adı: test/)).toBeVisible(); expect(storageSpy).not.toHaveBeenCalled()
  })
  it('shows forbidden content to a User on the Admin route', async () => {
    vi.mocked(fetch).mockImplementationOnce(() => json({ accessToken: 'opaque', user: { username: 'member', firstName: 'Member', lastName: 'User', role: 'User' } }))
    render(<MemoryRouter initialEntries={['/app/admin-check']}><App /></MemoryRouter>); expect(await screen.findByRole('heading', { name: 'Erişim yasak' })).toBeVisible()
  })
  it('allows an Admin to open the proof route', async () => {
    vi.mocked(fetch).mockImplementationOnce(() => json({ accessToken: 'opaque', user: { username: 'admin', firstName: 'Admin', lastName: 'User', role: 'Admin' } }))
    render(<MemoryRouter initialEntries={['/app/admin-check']}><App /></MemoryRouter>); expect(await screen.findByRole('heading', { name: 'Yönetici yetkilendirmesi' })).toBeVisible()
  })
  it('logout clears state and returns to login', async () => {
    vi.mocked(fetch).mockImplementationOnce(() => json({ accessToken: 'opaque', user: { username: 'member', firstName: 'Member', lastName: 'User', role: 'User' } })).mockImplementationOnce(() => Promise.resolve(new Response(null, { status: 204 })))
    render(<MemoryRouter initialEntries={['/app']}><App /></MemoryRouter>); fireEvent.click(await screen.findByRole('button', { name: 'Çıkış yap' })); expect(await screen.findByRole('heading', { name: 'Giriş yap' })).toBeVisible()
  })
})
