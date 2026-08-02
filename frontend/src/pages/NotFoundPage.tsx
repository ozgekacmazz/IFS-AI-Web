import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <section className="page-card" aria-labelledby="not-found-title">
      <p className="eyebrow">404</p>
      <h1 id="not-found-title">Sayfa bulunamadı</h1>
      <p>Aradığınız sayfa mevcut değil.</p>
      <Link className="primary-link" to="/">
        Ana sayfaya dön
      </Link>
    </section>
  )
}
