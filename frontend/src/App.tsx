import { Link, Navigate, Route, Routes } from 'react-router-dom'
import { AdminRoute, AuthProvider, LoginPage, ProtectedRoute, PublicOnlyRoute, RegisterPage, useAuth } from './auth/Auth'
import { AppPage } from './pages/AppPage'
import { HomePage } from './pages/HomePage'
import { NotFoundPage } from './pages/NotFoundPage'
import { AdminLayout } from './admin/AdminLayout'
import { AdminOverviewPage } from './admin/AdminOverviewPage'
import { AdminUsersPage } from './admin/AdminUsersPage'
import { AdminLogsPage } from './admin/AdminLogsPage'
export default function App() { return <AuthProvider><AppChrome /></AuthProvider> }
function AppChrome() { const { user } = useAuth(); return <div className={user ? 'app-shell authenticated-shell' : 'app-shell'}>{!user && <header className="site-header"><Link className="brand" to="/">IFS AI-Web</Link><nav aria-label="Genel"><Link to="/login">Giriş</Link><Link to="/register">Kayıt</Link></nav></header>}<main><Routes><Route path="/" element={<HomePage />} /><Route element={<PublicOnlyRoute />}><Route path="/login" element={<LoginPage />} /><Route path="/register" element={<RegisterPage />} /></Route><Route element={<ProtectedRoute />}><Route path="/app" element={<AppPage />} /><Route element={<AdminRoute />}><Route path="/app/admin-check" element={<Navigate to="/admin" replace />} /><Route path="/admin" element={<AdminLayout />}><Route index element={<AdminOverviewPage />} /><Route path="users" element={<AdminUsersPage />} /><Route path="logs" element={<AdminLogsPage />} /></Route></Route></Route><Route path="/404" element={<NotFoundPage />} /><Route path="*" element={<Navigate to="/404" replace />} /></Routes></main></div> }
