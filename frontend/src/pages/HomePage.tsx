import { Link } from 'react-router-dom'
export function HomePage() { return <section className="page-card"><p className="eyebrow">Phase 2</p><h1>Güvenli oturum temeli hazır</h1><p>PostgreSQL destekli kayıt, giriş ve rol yetkilendirmesi kullanıma hazırdır.</p><p className="notice">Özetleme özelliği henüz uygulanmadı.</p><Link className="primary-link" to="/login">Giriş yap</Link></section> }
