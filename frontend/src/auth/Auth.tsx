import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { Navigate, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { apiBaseUrl } from '../config/api'

export type User = { username: string; firstName: string; lastName: string; role: 'User' | 'Admin' }
type SessionResponse = { accessToken: string; accessTokenExpiresAtUtc: string; user: User }
type AuthValue = { user: User | null; ready: boolean; login: (username: string, password: string) => Promise<void>; logout: () => Promise<void>; request: (path: string, init?: RequestInit) => Promise<Response> }
const AuthContext = createContext<AuthValue | null>(null)
let accessToken: string | null = null
let refreshPromise: Promise<boolean> | null = null

async function sessionRequest(path: string, init?: RequestInit) {
  return fetch(`${apiBaseUrl}${path}`, { ...init, credentials: 'include', headers: { 'Content-Type': 'application/json', ...init?.headers } })
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null); const [ready, setReady] = useState(false); const mounted = useRef(true)
  const refresh = useCallback(async () => {
    if (!refreshPromise) refreshPromise = sessionRequest('/api/auth/refresh', { method: 'POST' }).then(async response => {
      if (!response.ok) { accessToken = null; if (mounted.current) setUser(null); return false }
      const data = await response.json() as SessionResponse; accessToken = data.accessToken; if (mounted.current) setUser(data.user); return true
    }).catch(() => { accessToken = null; if (mounted.current) setUser(null); return false }).finally(() => { refreshPromise = null })
    return refreshPromise
  }, [])
  useEffect(() => { mounted.current = true; void refresh().finally(() => setReady(true)); return () => { mounted.current = false } }, [refresh])
  const login = useCallback(async (username: string, password: string) => {
    const response = await sessionRequest('/api/auth/login', { method: 'POST', body: JSON.stringify({ username, password }) })
    if (!response.ok) throw new Error('Kullanıcı adı veya şifre geçersiz.')
    const data = await response.json() as SessionResponse; accessToken = data.accessToken; setUser(data.user)
  }, [])
  const logout = useCallback(async () => { try { await sessionRequest('/api/auth/logout', { method: 'POST' }) } finally { accessToken = null; setUser(null) } }, [])
  const request = useCallback(async (path: string, init?: RequestInit) => {
    const send = () => fetch(`${apiBaseUrl}${path}`, { ...init, headers: { ...init?.headers, ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}) } })
    let response = await send(); if (response.status === 401 && await refresh()) response = await send(); return response
  }, [refresh])
  const value = useMemo(() => ({ user, ready, login, logout, request }), [user, ready, login, logout, request])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
// eslint-disable-next-line react-refresh/only-export-components
export const useAuth = () => { const value = useContext(AuthContext); if (!value) throw new Error('AuthProvider gerekli'); return value }
export function ProtectedRoute() { const auth = useAuth(); const location = useLocation(); if (!auth.ready) return <p role="status">Oturum kontrol ediliyor…</p>; return auth.user ? <Outlet /> : <Navigate to="/login" state={{ from: location }} replace /> }
export function AdminRoute() { const { user } = useAuth(); return user?.role === 'Admin' ? <Outlet /> : <section className="page-card"><h1>Erişim yasak</h1><p>Bu alan yalnızca yöneticiler içindir.</p></section> }

export function LoginPage() {
  const { login } = useAuth(); const navigate = useNavigate(); const [error, setError] = useState(''); const [busy, setBusy] = useState(false)
  async function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); setBusy(true); setError(''); const data = new FormData(event.currentTarget); try { await login(String(data.get('username')), String(data.get('password'))); navigate('/app') } catch { setError('Kullanıcı adı veya şifre geçersiz.') } finally { setBusy(false) } }
  return <AuthForm title="Giriş yap" onSubmit={submit} error={error} busy={busy}><Field label="Kullanıcı adı" name="username" autoComplete="username" /><Field label="Şifre" name="password" type="password" autoComplete="current-password" /></AuthForm>
}
export function RegisterPage() {
  const navigate = useNavigate(); const [error, setError] = useState(''); const [busy, setBusy] = useState(false)
  async function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); setError(''); const data = new FormData(event.currentTarget); if (data.get('password') !== data.get('passwordConfirmation')) { setError('Şifreler eşleşmiyor.'); return } setBusy(true); try { const body = Object.fromEntries(data); const response = await sessionRequest('/api/auth/register', { method: 'POST', body: JSON.stringify(body) }); if (!response.ok) throw new Error(response.status === 409 ? 'Bu kullanıcı adı kullanılıyor.' : 'Kayıt bilgilerini kontrol edin.'); navigate('/login', { state: { registered: true } }) } catch (e) { setError(e instanceof Error ? e.message : 'Kayıt yapılamadı.') } finally { setBusy(false) } }
  return <AuthForm title="Hesap oluştur" onSubmit={submit} error={error} busy={busy}><Field label="Kullanıcı adı" name="username" autoComplete="username" /><Field label="Ad" name="firstName" autoComplete="given-name" /><Field label="Soyad" name="lastName" autoComplete="family-name" /><Field label="Şifre" name="password" type="password" autoComplete="new-password" /><Field label="Şifre tekrarı" name="passwordConfirmation" type="password" autoComplete="new-password" /></AuthForm>
}
function AuthForm({ title, onSubmit, error, busy, children }: { title: string; onSubmit: (e: FormEvent<HTMLFormElement>) => void; error: string; busy: boolean; children: ReactNode }) { return <section className="page-card"><h1>{title}</h1>{error && <p className="error" role="alert">{error}</p>}<form onSubmit={onSubmit} noValidate>{children}<button disabled={busy}>{busy ? 'Bekleyin…' : title}</button></form></section> }
function Field({ label, name, type = 'text', autoComplete }: { label: string; name: string; type?: string; autoComplete: string }) { return <label>{label}<input name={name} type={type} autoComplete={autoComplete} required aria-required="true" /></label> }
