import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/Auth'

export function AdminLayout() {
  const { user, logout } = useAuth(); const navigate = useNavigate()
  async function signOut() { await logout(); navigate('/login', { replace: true }) }
  return <div className="admin-shell">
    <header className="admin-header">
      <NavLink className="brand" to="/app">IFS AI-Web</NavLink>
      <nav aria-label="Yönetim">
        <NavLink end to="/admin">Genel bakış</NavLink>
        <NavLink to="/admin/users">Kullanıcılar</NavLink>
        <NavLink to="/admin/logs">AI kayıtları</NavLink>
        <NavLink to="/app">Özetleme alanı</NavLink>
      </nav>
      <div className="admin-account"><span><strong>{user?.firstName} {user?.lastName}</strong> · Admin</span><button className="secondary-button" onClick={() => void signOut()}>Çıkış yap</button></div>
    </header>
    <div className="admin-main"><Outlet /></div>
  </div>
}
