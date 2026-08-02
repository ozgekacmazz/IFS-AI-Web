import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type FormEvent, type ReactNode, type RefObject } from 'react'
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
export function PublicOnlyRoute() { const auth = useAuth(); return auth.ready && auth.user ? <Navigate to="/app" replace /> : <Outlet /> }
export function AdminRoute() { const { user } = useAuth(); return user?.role === 'Admin' ? <Outlet /> : <section className="page-card"><h1>Erişim yasak</h1><p>Bu alan yalnızca yöneticiler içindir.</p></section> }

export function LoginPage() {
  const { login } = useAuth(); const navigate = useNavigate(); const [error, setError] = useState(''); const [busy, setBusy] = useState(false)
  async function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); if (busy) return; const data = new FormData(event.currentTarget); const username = String(data.get('username') ?? '').trim(); const password = String(data.get('password') ?? ''); if (!username || !password) { setError('Kullanıcı adı ve şifre zorunludur.'); return } setBusy(true); setError(''); try { await login(username, password); navigate('/app') } catch { setError('Kullanıcı adı veya şifre geçersiz.') } finally { setBusy(false) } }
  return <AuthForm title="Giriş yap" onSubmit={submit} error={error} busy={busy}><Field label="Kullanıcı adı" name="username" autoComplete="username" /><Field label="Şifre" name="password" type="password" autoComplete="current-password" /></AuthForm>
}
type FieldName = 'username' | 'firstName' | 'lastName' | 'password' | 'passwordConfirmation'
type FieldErrors = Partial<Record<FieldName, string[]>>
const fieldOrder: FieldName[] = ['username', 'firstName', 'lastName', 'password', 'passwordConfirmation']
const passwordHint = 'En az 8, en fazla 128 karakter; büyük harf, küçük harf, rakam ve noktalama/özel karakter içermelidir.'

export function RegisterPage() {
  const navigate = useNavigate(); const [error, setError] = useState(''); const [errors, setErrors] = useState<FieldErrors>({}); const [busy, setBusy] = useState(false)
  const usernameRef = useRef<HTMLInputElement>(null); const firstNameRef = useRef<HTMLInputElement>(null); const lastNameRef = useRef<HTMLInputElement>(null); const passwordRef = useRef<HTMLInputElement>(null); const confirmationRef = useRef<HTMLInputElement>(null)
  const focusFirst = (next: FieldErrors) => queueMicrotask(() => { const refs: Record<FieldName, RefObject<HTMLInputElement | null>> = { username: usernameRef, firstName: firstNameRef, lastName: lastNameRef, password: passwordRef, passwordConfirmation: confirmationRef }; const first = fieldOrder.find(key => next[key]?.length); if (first) refs[first].current?.focus() })
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (busy) return; setError(''); setErrors({}); const data = new FormData(event.currentTarget)
    const body = { username: String(data.get('username') ?? '').trim(), firstName: String(data.get('firstName') ?? '').trim(), lastName: String(data.get('lastName') ?? '').trim(), password: String(data.get('password') ?? ''), passwordConfirmation: String(data.get('passwordConfirmation') ?? '') }
    const local = validateRegistration(body); if (Object.keys(local).length) { setErrors(local); setError('Lütfen işaretlenen alanları düzeltin.'); focusFirst(local); return }
    setBusy(true)
    try {
      const response = await sessionRequest('/api/auth/register', { method: 'POST', body: JSON.stringify(body) })
      if (!response.ok) {
        if (response.status === 400 || response.status === 409) {
          const mapped = await safeFieldErrors(response); if (Object.keys(mapped).length) { setErrors(mapped); setError('Lütfen işaretlenen alanları düzeltin.'); focusFirst(mapped); return }
        }
        setError('Şu anda kayıt işlemi tamamlanamadı. Lütfen daha sonra tekrar deneyin.'); return
      }
      navigate('/login', { state: { registered: true } })
    } catch { setError('Şu anda kayıt işlemi tamamlanamadı. Lütfen daha sonra tekrar deneyin.') } finally { setBusy(false) }
  }
  return <AuthForm title="Hesap oluştur" onSubmit={submit} error={error} busy={busy}><Field inputRef={usernameRef} label="Kullanıcı adı" name="username" autoComplete="username" error={errors.username} /><Field inputRef={firstNameRef} label="Ad" name="firstName" autoComplete="given-name" error={errors.firstName} /><Field inputRef={lastNameRef} label="Soyad" name="lastName" autoComplete="family-name" error={errors.lastName} /><Field inputRef={passwordRef} label="Şifre" name="password" type="password" autoComplete="new-password" hint={passwordHint} error={errors.password} /><Field inputRef={confirmationRef} label="Şifre tekrarı" name="passwordConfirmation" type="password" autoComplete="new-password" error={errors.passwordConfirmation} /></AuthForm>
}
function validateRegistration(body: Record<FieldName, string>): FieldErrors {
  const errors: FieldErrors = {}; const add = (key: FieldName, message: string) => { errors[key] = [...(errors[key] ?? []), message] }
  if (!body.username) add('username', 'Kullanıcı adı zorunludur.'); else if (body.username.length < 3 || body.username.length > 32) add('username', 'Kullanıcı adı 3-32 karakter olmalıdır.'); else if (!/^[\p{L}\p{N}._-]+$/u.test(body.username)) add('username', 'Kullanıcı adı yalnızca harf, rakam, nokta, alt çizgi ve kısa çizgi içerebilir.')
  if (!body.firstName) add('firstName', 'Ad zorunludur.'); else if (body.firstName.length > 80 || !/^[\p{L}\p{M} '-]+$/u.test(body.firstName)) add('firstName', 'Ad en fazla 80 karakter olmalı ve yalnızca adlarda kullanılan karakterleri içermelidir.')
  if (!body.lastName) add('lastName', 'Soyad zorunludur.'); else if (body.lastName.length > 80 || !/^[\p{L}\p{M} '-]+$/u.test(body.lastName)) add('lastName', 'Soyad en fazla 80 karakter olmalı ve yalnızca adlarda kullanılan karakterleri içermelidir.')
  const length = Array.from(body.password).length; if (!body.password) add('password', 'Şifre zorunludur.'); else { if (length < 8) add('password', 'Şifre en az 8 karakter olmalıdır.'); if (length > 128) add('password', 'Şifre en fazla 128 karakter olmalıdır.'); if (!/\p{Lu}/u.test(body.password)) add('password', 'Şifre en az bir büyük harf içermelidir.'); if (!/\p{Ll}/u.test(body.password)) add('password', 'Şifre en az bir küçük harf içermelidir.'); if (!/\p{Nd}/u.test(body.password)) add('password', 'Şifre en az bir rakam içermelidir.'); if (!/[\p{P}\p{S}]/u.test(body.password)) add('password', 'Şifre en az bir noktalama veya özel karakter içermelidir.') }
  if (!body.passwordConfirmation) add('passwordConfirmation', 'Şifre tekrarı zorunludur.'); else if (body.password !== body.passwordConfirmation) add('passwordConfirmation', 'Şifreler eşleşmiyor.')
  return errors
}
async function safeFieldErrors(response: Response): Promise<FieldErrors> { try { const value = await response.json() as { errors?: unknown }; if (!value.errors || typeof value.errors !== 'object' || Array.isArray(value.errors)) return {}; const result: FieldErrors = {}; for (const key of fieldOrder) { const messages = (value.errors as Record<string, unknown>)[key]; if (Array.isArray(messages)) { const safe = messages.filter((item): item is string => typeof item === 'string' && item.length <= 300); if (safe.length) result[key] = safe } } return result } catch { return {} } }
function AuthForm({ title, onSubmit, error, busy, children }: { title: string; onSubmit: (e: FormEvent<HTMLFormElement>) => void; error: string; busy: boolean; children: ReactNode }) { return <section className="page-card"><h1>{title}</h1>{error && <p className="error" role="alert" aria-live="assertive">{error}</p>}<form onSubmit={onSubmit} noValidate>{children}<button disabled={busy}>{busy ? 'Bekleyin…' : title}</button></form></section> }
function Field({ label, name, type = 'text', autoComplete, hint, error, inputRef }: { label: string; name: FieldName; type?: string; autoComplete: string; hint?: string; error?: string[]; inputRef?: RefObject<HTMLInputElement | null> }) { const [visible, setVisible] = useState(false); const isPassword = type === 'password'; const describedBy = [hint ? `${name}-hint` : '', error?.length ? `${name}-error` : ''].filter(Boolean).join(' ') || undefined; const toggleLabel = visible ? 'Şifreyi gizle' : 'Şifreyi göster'; return <div className="form-field"><label htmlFor={name}>{label}</label><div className={isPassword ? 'password-input' : undefined}><input ref={inputRef} id={name} name={name} type={isPassword && visible ? 'text' : type} autoComplete={autoComplete} required aria-required="true" aria-invalid={error?.length ? 'true' : undefined} aria-describedby={describedBy} />{isPassword && <button type="button" className="password-toggle" aria-label={toggleLabel} title={toggleLabel} aria-pressed={visible} onClick={() => setVisible(value => !value)}>{visible ? <EyeOffIcon /> : <EyeIcon />}</button>}</div>{hint && <small id={`${name}-hint`}>{hint}</small>}{error?.length && <ul id={`${name}-error`} className="field-errors">{error.map(message => <li key={message}>{message}</li>)}</ul>}</div> }
function EyeIcon() { return <svg aria-hidden="true" viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2"><path d="M2.5 12s3.5-6 9.5-6 9.5 6 9.5 6-3.5 6-9.5 6S2.5 12 2.5 12Z"/><circle cx="12" cy="12" r="2.5"/></svg> }
function EyeOffIcon() { return <svg aria-hidden="true" viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2"><path d="m3 3 18 18M10.6 6.2A10.5 10.5 0 0 1 12 6c6 0 9.5 6 9.5 6a17 17 0 0 1-2.1 2.8M6.2 6.2C3.8 8 2.5 12 2.5 12s3.5 6 9.5 6c1.4 0 2.7-.3 3.8-.8M9.9 9.9a3 3 0 0 0 4.2 4.2"/></svg> }
