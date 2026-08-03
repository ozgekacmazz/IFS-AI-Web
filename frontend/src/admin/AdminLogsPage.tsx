import { useEffect, useState, type FormEvent } from 'react'
import { adminApi, type AdminLog, type LogFilters, type Page } from './adminApi'
import { useAuth } from '../auth/Auth'
import { AdminState, PageHeading } from './AdminOverviewPage'
import { Pagination } from './AdminUsersPage'

type FilterDraft = Omit<LogFilters, 'page' | 'pageSize'>
const emptyFilters: FilterDraft = { status: '', language: '', user: '', fromUtc: '', toUtc: '' }

export function AdminLogsPage() {
  const { request } = useAuth(); const [page, setPage] = useState(1); const [draft, setDraft] = useState<FilterDraft>(emptyFilters); const [filters, setFilters] = useState<FilterDraft>(emptyFilters)
  const [data, setData] = useState<Page<AdminLog> | null>(null); const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading'); const [reload, setReload] = useState(0)
  useEffect(() => { const controller = new AbortController(); void (async () => { await Promise.resolve(); setState('loading'); try { setData(await adminApi.logs(request, { ...filters, page, pageSize: 20 }, controller.signal)); setState('ready') } catch (error) { if ((error as Error).name !== 'AbortError') setState('error') } })(); return () => controller.abort() }, [request, filters, page, reload])
  function submit(event: FormEvent) { event.preventDefault(); setPage(1); setFilters(draft) }
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1
  return <section className="admin-page" aria-labelledby="admin-logs-title">
    <PageHeading kicker="Mahremiyet odaklı" title="AI işlem kayıtları" id="admin-logs-title" text="Yalnızca sunucu tarafından güvenli biçimde kısaltılmış önizlemeler ve operasyonel üst veriler gösterilir." />
    <section className="admin-panel"><form className="filter-bar log-filters" aria-label="Kayıt filtreleri" onSubmit={submit}>
      <p id="log-date-help" className="filter-help">Tarihler UTC olarak değerlendirilir. Başlangıç anı aralığa dahildir, bitiş anı dahil değildir.</p>
      <label htmlFor="log-user">Kullanıcı<input id="log-user" value={draft.user} onChange={event => setDraft(value => ({ ...value, user: event.target.value }))} /></label>
      <label htmlFor="log-status">Durum<select id="log-status" value={draft.status} onChange={event => setDraft(value => ({ ...value, status: event.target.value as FilterDraft['status'] }))}><option value="">Tümü</option><option value="Succeeded">Başarılı</option><option value="Failed">Başarısız</option></select></label>
      <label htmlFor="log-language">Dil<select id="log-language" value={draft.language} onChange={event => setDraft(value => ({ ...value, language: event.target.value as FilterDraft['language'] }))}><option value="">Tümü</option><option value="Turkish">Türkçe</option><option value="English">İngilizce</option></select></label>
      <label htmlFor="log-from">Başlangıç tarihi<input id="log-from" type="datetime-local" aria-describedby="log-date-help" value={localValue(draft.fromUtc)} onChange={event => setDraft(value => ({ ...value, fromUtc: utcValue(event.target.value) }))} /></label>
      <label htmlFor="log-to">Bitiş tarihi<input id="log-to" type="datetime-local" aria-describedby="log-date-help" value={localValue(draft.toUtc)} onChange={event => setDraft(value => ({ ...value, toUtc: utcValue(event.target.value) }))} /></label>
      <button>Filtrele</button>
    </form></section>
    {state === 'loading' && <AdminState title="AI kayıtları yükleniyor…" />}{state === 'error' && <AdminState title="AI kayıtları yüklenemedi." error onRetry={() => setReload(value => value + 1)} />}
    {state === 'ready' && data?.items.length === 0 && <p className="empty-message">Filtrelere uygun işlem kaydı bulunamadı.</p>}
    {state === 'ready' && data && data.items.length > 0 && <div className="log-list">{data.items.map(item => <LogCard key={item.id} item={item} />)}</div>}
    <Pagination page={page} pages={pages} total={data?.total ?? 0} onPage={setPage} />
  </section>
}

function LogCard({ item }: { item: AdminLog }) { const succeeded = item.status === 'Succeeded'; return <article className="log-card"><header><div><StatusLabel succeeded={succeeded} /><strong>@{item.username}</strong></div><time dateTime={item.createdAtUtc}>{formatDate(item.createdAtUtc)}</time></header><dl className="log-meta"><div><dt>Dil</dt><dd>{item.language === 'Turkish' ? 'Türkçe' : 'İngilizce'}</dd></div><div><dt>Kullanılan AI modeli</dt><dd>{item.provider} / {item.model}</dd></div><div><dt>Sürüm</dt><dd>{item.promptVersion}</dd></div><div><dt>Süre</dt><dd>{item.durationMilliseconds.toLocaleString('tr-TR')} ms</dd></div><div><dt>Metin uzunluğu</dt><dd>{item.inputCharacterCount.toLocaleString('tr-TR')} giriş · {item.outputCharacterCount.toLocaleString('tr-TR')} çıkış</dd></div>{item.failureCategory && <div><dt>Hata kategorisi</dt><dd>{item.failureCategory}</dd></div>}</dl>{succeeded && item.inputPreview !== null && item.summaryPreview !== null ? <div className="preview-grid"><section><h3>Kısa giriş</h3><p>{item.inputPreview}</p></section><section><h3>Kısa özet</h3><p>{item.summaryPreview}</p></section></div> : succeeded ? <p className="privacy-note">Bu kaydın içerik önizlemesi saklama süresi nedeniyle gösterilmiyor.</p> : <p className="privacy-note">{item.privacyExplanation ?? 'Başarısız işlem için kaynak veya özet önizlemesi bulunmuyor.'}</p>}</article> }
function StatusLabel({ succeeded }: { succeeded: boolean }) { return <span className={`status-badge ${succeeded ? 'status-success' : 'status-failure'}`}><span aria-hidden="true">{succeeded ? '✓' : '!'}</span>{succeeded ? 'Başarılı' : 'Başarısız'}</span> }
function formatDate(value: string) { return new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) }
function utcValue(value: string) { return value ? `${value}:00Z` : '' }
function localValue(value?: string) { return value?.endsWith(':00Z') ? value.slice(0, -4) : value ?? '' }
