import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/Auth'

export function AppPage() {
  const { user, logout } = useAuth(); const navigate = useNavigate()
  return <section className="page-card"><p className="eyebrow">Phase 2</p><h1>Hoş geldiniz, {user?.firstName}</h1><p>Kullanıcı adı: {user?.username}<br />Rol: {user?.role}</p><p className="notice">Özetleme özelliği sonraki bir fazda uygulanacaktır.</p>{user?.role === 'Admin' && <Link to="/app/admin-check">Yönetici kontrolü</Link>}<button onClick={() => void logout().then(() => navigate('/login'))}>Çıkış yap</button></section>
}
export function AdminCheckPage() {
  const { request } = useAuth();
  return <section className="page-card"><h1>Yönetici yetkilendirmesi</h1><p>Bu rota hem istemci rol koruması hem de sunucudaki Admin politikasıyla korunur.</p><button onClick={() => void request('/api/auth/admin-check')}>Yetkilendirmeyi doğrula</button></section>
}
