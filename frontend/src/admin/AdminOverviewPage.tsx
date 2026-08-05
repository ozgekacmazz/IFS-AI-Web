import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { adminApi, type PromptInfo, type SevenDayStatistics } from './adminApi'
import { useAuth } from '../auth/Auth'

export function AdminOverviewPage() {
  const { request } = useAuth(); const [statistics, setStatistics] = useState<SevenDayStatistics | null>(null); const [prompt, setPrompt] = useState<PromptInfo | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading')
  const load = useCallback(async (signal?: AbortSignal) => { await Promise.resolve(); setState('loading'); try { const [nextStatistics, nextPrompt] = await Promise.all([adminApi.statistics(request, signal), adminApi.promptInfo(request, signal)]); setStatistics(nextStatistics); setPrompt(nextPrompt); setState('ready') } catch (error) { if ((error as Error).name !== 'AbortError') setState('error') } }, [request])
  useEffect(() => {
    let active = true
    Promise.all([adminApi.statistics(request), adminApi.promptInfo(request)])
      .then(([nextStatistics, nextPrompt]) => {
        if (active) {
          setStatistics(nextStatistics)
          setPrompt(nextPrompt)
          setState('ready')
        }
      })
      .catch(error => {
        if (active && (error as Error).name !== 'AbortError') setState('error')
      })
    return () => {
      active = false
    }
  }, [request])
  const totals = useMemo(() => statistics?.days.reduce((value, day) => ({ total: value.total + day.total, succeeded: value.succeeded + day.succeeded, failed: value.failed + day.failed, duration: value.duration + day.averageDurationMilliseconds * day.total }), { total: 0, succeeded: 0, failed: 0, duration: 0 }), [statistics])
  if (state === 'loading') return <AdminState title="Yönetim özeti yükleniyor…" />
  if (state === 'error' || !statistics || !prompt) return <AdminState title="Yönetim özeti yüklenemedi." error onRetry={() => void load()} />
  const rate = totals?.total ? Math.round((totals.succeeded / totals.total) * 10000) / 100 : 0
  const average = totals?.total ? Math.round(totals.duration / totals.total) : 0
  const provider = statistics.providers[0]
  const feedback = statistics.feedback
  const hasFeedback = Boolean(feedback && (feedback.useful + feedback.notUseful > 0))

  return <section className="admin-page" aria-labelledby="admin-overview-title">
    <PageHeading kicker="Admin paneli" title="Genel bakış" id="admin-overview-title" text="Son 7 günlük kullanımı ve etkin özetleme sürümünü güvenli biçimde izleyin." />
    <div className="admin-shortcuts"><Link to="/admin/users">Kullanıcıları yönet</Link><Link to="/admin/logs">AI kayıtlarını incele</Link></div>
    <section aria-labelledby="statistics-title"><h2 id="statistics-title">Son 7 günlük kullanım</h2><p className="muted">Veriler günlük olarak gösterilir.</p>
      <div className="metric-grid">
        <Metric label="Toplam işlem" value={totals?.total ?? 0} /><Metric label="Başarılı" value={totals?.succeeded ?? 0} /><Metric label="Başarısız" value={totals?.failed ?? 0} />
        <Metric label="Başarı oranı" value={`%${rate.toLocaleString('tr-TR')}`} /><Metric label="Ortalama süre" value={`${average.toLocaleString('tr-TR')} ms`} /><Metric label="Aktif kullanıcı" value={statistics.activeUsers} />
        <Metric label="Kullanıcı Memnuniyeti" value={hasFeedback ? `👍 %${Math.round(feedback!.satisfactionRate)}` : 'Henüz değerlendirme yok'} subtext={hasFeedback ? `(${feedback!.useful} Faydalı / ${feedback!.notUseful} Faydalı Değil)` : undefined} />
      </div>
      {provider && <p className="provider-summary"><strong>En çok kullanılan AI modeli:</strong> {provider.provider} / {provider.model} ({provider.total} işlem)</p>}
      <SevenDayChart days={statistics.days} />
    </section>
    <section className="prompt-card" aria-labelledby="prompt-title"><div><p className="section-kicker">Salt okunur</p><h2 id="prompt-title">Özetleme bilgisi</h2></div><dl><div><dt>Sürüm</dt><dd>{prompt.version}</dd></div><div><dt>Amaç</dt><dd>{prompt.purpose}</dd></div><div><dt>Diller</dt><dd>{prompt.supportedLanguages.join(', ')}</dd></div><div><dt>Düzenlenebilir</dt><dd>{prompt.editable ? 'Evet' : 'Hayır — yalnızca bilgi amaçlıdır'}</dd></div></dl></section>
  </section>
}

function SevenDayChart({ days }: { days: SevenDayStatistics['days'] }) { const max = Math.max(1, ...days.map(day => day.total)); return <div className="usage-chart" aria-label="Günlük toplam işlem grafiği"><ul>{days.map(day => <li key={day.dateUtc}><span className="chart-value">{day.total}</span><span className="chart-bar" aria-hidden="true" style={{ height: `${Math.max(4, day.total / max * 100)}%` }} /><time dateTime={day.dateUtc}>{new Intl.DateTimeFormat('tr-TR', { day: '2-digit', month: 'short', timeZone: 'UTC' }).format(new Date(`${day.dateUtc}T00:00:00Z`))}</time></li>)}</ul><table className="sr-table"><caption>Günlük işlem sayıları</caption><tbody>{days.map(day => <tr key={day.dateUtc}><th>{day.dateUtc} UTC</th><td>{day.total}</td></tr>)}</tbody></table></div> }
function Metric({ label, value, subtext }: { label: string; value: string | number; subtext?: string }) { return <article className="metric-card"><span>{label}</span><strong>{value}</strong>{subtext && <small className="metric-subtext">{subtext}</small>}</article> }
export function PageHeading({ kicker, title, text, id }: { kicker: string; title: string; text: string; id: string }) { return <header className="admin-page-heading"><p className="section-kicker">{kicker}</p><h1 id={id}>{title}</h1><p>{text}</p></header> }
export function AdminState({ title, error = false, onRetry }: { title: string; error?: boolean; onRetry?: () => void }) { return <section className="admin-state"><p className={error ? 'error' : undefined} role={error ? 'alert' : 'status'}>{title}</p>{onRetry && <button className="secondary-button" onClick={onRetry}>Yeniden dene</button>}</section> }
