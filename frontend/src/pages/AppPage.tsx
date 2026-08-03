import { useCallback, useEffect, useRef, useState, type FormEvent, type RefObject } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/Auth'
import { deriveSummaryTitle, type SummaryLanguage } from '../summaryTitles'

type Summary = { id: string; summary: string; language: SummaryLanguage; createdAtUtc: string; expiresAtUtc: string }
type SummaryDetail = Summary & { inputText: string; promptVersion?: string }
type View = 'workspace' | 'library' | 'detail'
const maxLength = 12000
const pinStorageKey = 'ifs-aiweb:pinned-summary-ids'

export function AppPage() {
  const { user, logout, request } = useAuth(); const navigate = useNavigate()
  const [text, setText] = useState(''); const [language, setLanguage] = useState<SummaryLanguage>('Turkish'); const [busy, setBusy] = useState(false)
  const [selected, setSelected] = useState<SummaryDetail | null>(null); const [detailTarget, setDetailTarget] = useState<Summary | null>(null); const [view, setView] = useState<View>('workspace')
  const [detailLoading, setDetailLoading] = useState(false); const [detailError, setDetailError] = useState('')
  const [error, setError] = useState(''); const [recent, setRecent] = useState<Summary[]>([]); const [historyState, setHistoryState] = useState<'loading' | 'ready' | 'error'>('loading')
  const [narrow, setNarrow] = useState(isNarrowViewport); const [sidebarOpen, setSidebarOpen] = useState(() => !isNarrowViewport())
  const [pinnedIds, setPinnedIds] = useState<string[]>(readPinnedIds); const resultHeading = useRef<HTMLHeadingElement>(null); const sidebarToggle = useRef<HTMLButtonElement>(null)
  const detailCache = useRef(new Map<string, SummaryDetail>()); const pendingDetailId = useRef<string | null>(null); const detailTriggerKey = useRef<string | null>(null); const returnView = useRef<'workspace' | 'library'>('workspace')

  const loadRecent = useCallback(async () => { setHistoryState('loading'); try { const response = await request('/api/summaries/recent'); if (!response.ok) throw new Error(); setRecent(await response.json() as Summary[]); setHistoryState('ready') } catch { setHistoryState('error') } }, [request])
  useEffect(() => { let active = true; void request('/api/summaries/recent').then(async response => { if (!response.ok) throw new Error(); const data = await response.json() as Summary[]; if (active) { setRecent(data); setHistoryState('ready') } }).catch(() => { if (active) setHistoryState('error') }); return () => { active = false } }, [request])
  useEffect(() => { if (!sidebarOpen) return; const escape = (event: KeyboardEvent) => { if (event.key === 'Escape') { setSidebarOpen(false); sidebarToggle.current?.focus() } }; document.addEventListener('keydown', escape); return () => document.removeEventListener('keydown', escape) }, [sidebarOpen])
  useEffect(() => { if (!window.matchMedia) return; const media = window.matchMedia('(max-width: 760px)'); const update = () => setNarrow(media.matches); media.addEventListener?.('change', update); return () => media.removeEventListener?.('change', update) }, [])

  function newSummary() { setText(''); setLanguage('Turkish'); setSelected(null); setDetailTarget(null); setDetailError(''); setError(''); setView('workspace'); closeMobileSidebar() }
  async function openSummary(item: Summary, trigger?: HTMLButtonElement) {
    if (pendingDetailId.current === item.id) return
    if (view !== 'detail') returnView.current = view
    if (trigger) detailTriggerKey.current = trigger.dataset.detailTrigger ?? null
    setDetailTarget(item); setSelected(null); setDetailError(''); setView('detail'); closeMobileSidebar()
    const cached = detailCache.current.get(item.id)
    if (cached) { setSelected(cached); queueMicrotask(() => resultHeading.current?.focus()); return }
    pendingDetailId.current = item.id; setDetailLoading(true)
    try {
      const response = await request(`/api/summaries/${encodeURIComponent(item.id)}`)
      if (!response.ok) throw new Error()
      const detail = await response.json() as SummaryDetail; detailCache.current.set(item.id, detail); setSelected(detail)
      queueMicrotask(() => resultHeading.current?.focus())
    } catch { setDetailError('Özet ayrıntıları şu anda yüklenemedi. Lütfen yeniden deneyin.') }
    finally { pendingDetailId.current = null; setDetailLoading(false) }
  }
  function closeDetail() { const triggerKey = detailTriggerKey.current; setView(returnView.current); setSelected(null); setDetailTarget(null); setDetailError(''); queueMicrotask(() => { if (!triggerKey) return; [...document.querySelectorAll<HTMLButtonElement>('[data-detail-trigger]')].find(button => button.dataset.detailTrigger === triggerKey)?.focus() }) }
  function togglePin(id: string) { setPinnedIds(current => { const next = current.includes(id) ? current.filter(value => value !== id) : [...current, id]; localStorage.setItem(pinStorageKey, JSON.stringify(next)); return next }) }
  function closeMobileSidebar() { if (narrow) setSidebarOpen(false) }
  async function submit(event: FormEvent) {
    event.preventDefault(); if (busy || !text.trim() || text.length > maxLength) return; setBusy(true); setError('')
    try { const response = await request('/api/summaries', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ text, language }) }); if (!response.ok) throw new Error(response.status === 429 ? 'İstek sınırına ulaştınız. Lütfen bir dakika sonra yeniden deneyin.' : 'Özet şu anda oluşturulamadı. Lütfen yeniden deneyin.'); const data = await response.json() as Summary; const complete: SummaryDetail = { ...data, inputText: text }; detailCache.current.set(complete.id, complete); returnView.current = 'workspace'; setDetailTarget(data); setSelected(complete); setView('detail'); queueMicrotask(() => resultHeading.current?.focus()); void loadRecent() } catch (reason) { setError(reason instanceof Error ? reason.message : 'Özet oluşturulamadı.') } finally { setBusy(false) }
  }
  async function signOut() { await logout(); navigate('/login', { replace: true }) }

  const pinned = recent.filter(item => pinnedIds.includes(item.id)); const invalid = !text.trim() || text.length > maxLength
  return <div className={`product-shell ${sidebarOpen ? 'sidebar-open' : 'sidebar-collapsed'}`}>
    <button ref={sidebarToggle} className="sidebar-toggle icon-button" aria-label={sidebarOpen ? 'Kenar çubuğunu kapat' : 'Kenar çubuğunu aç'} aria-expanded={sidebarOpen} aria-controls="product-sidebar" onClick={() => setSidebarOpen(value => !value)}><MenuIcon /></button>
    {sidebarOpen && narrow && <button className="drawer-overlay" aria-label="Kenar çubuğunu kapat" onClick={() => { setSidebarOpen(false); sidebarToggle.current?.focus() }} />}
    <aside id="product-sidebar" className="product-sidebar" aria-label="Çalışma alanı menüsü">
      <div className="sidebar-brand"><span className="brand-mark" aria-hidden="true">IA</span><strong>IFS AI-Web</strong>{narrow && <button className="sidebar-close icon-button" aria-label="Kenar çubuğunu kapat" onClick={() => { setSidebarOpen(false); sidebarToggle.current?.focus() }}><CloseIcon /></button>}</div>
      <button className="new-summary-button" aria-label="Yeni özet" title="Yeni özet" onClick={newSummary}><PlusIcon /><span>Yeni özet</span></button>
      <nav className="workspace-nav" aria-label="Özetler">
        <button className={view === 'library' ? 'nav-action active' : 'nav-action'} aria-label="Kitaplık" title="Kitaplık" onClick={() => { setView('library'); setSelected(null); closeMobileSidebar() }}><LibraryIcon /><span>Kitaplık</span></button>
        {user?.role === 'Admin' && <Link className="nav-action admin-nav-link" to="/admin"><AdminIcon /><span>Admin paneli</span></Link>}
        <SidebarSection title="Sabitlenen özetler" items={pinned} empty="Henüz sabitlenen özet yok." onOpen={openSummary} />
        <SidebarSection title="Son özetler" items={recent} empty={historyState === 'loading' ? 'Yükleniyor…' : 'Henüz özet yok.'} onOpen={openSummary} />
      </nav>
      <div className="account-area"><div className="avatar" aria-hidden="true">{user?.firstName?.[0]}{user?.lastName?.[0]}</div><div className="account-copy"><strong>{user?.firstName} {user?.lastName}</strong><span>@{user?.username}</span></div><button className="logout-button icon-button" aria-label="Çıkış yap" title="Çıkış yap" onClick={() => void signOut()}><LogoutIcon /></button></div>
    </aside>
    <main className="workspace-main">
      {view === 'workspace' && <section className="composer-view" aria-labelledby="workspace-title"><div className="workspace-heading"><p className="section-kicker">Yeni özet</p><h1 id="workspace-title">Metninizi anlaşılır bir özete dönüştürün</h1><p>Kaynak metni ekleyin, çıktı dilini seçin ve önemli noktaları hızlıca görün.</p></div><form className="summary-composer" onSubmit={submit}><label htmlFor="summary-text">Kaynak metin</label><textarea id="summary-text" value={text} onChange={event => setText(event.target.value)} maxLength={maxLength + 1} rows={12} placeholder="Özetlemek istediğiniz metni buraya yapıştırın…" aria-describedby="character-count retention-note" /><div className="composer-meta"><span id="character-count" className={text.length > maxLength ? 'error-text' : ''}>{text.length.toLocaleString('tr-TR')} / 12.000</span><fieldset className="language-selector"><legend>Özet dili</legend><label><input type="radio" name="language" value="Turkish" checked={language === 'Turkish'} onChange={() => setLanguage('Turkish')} /> Türkçe</label><label><input type="radio" name="language" value="English" checked={language === 'English'} onChange={() => setLanguage('English')} /> İngilizce</label></fieldset></div><button className="summarize-button" disabled={busy || invalid}>{busy ? 'Özetleniyor…' : 'Özet oluştur'}</button></form><p id="retention-note" className="retention-notice">Gönderdiğiniz metin ve oluşturulan özet 30 gün saklanır. Otomatik silme görevi henüz uygulanmamıştır.</p><div aria-live="polite">{error && <p className="error" role="alert">{error}</p>}</div></section>}
      {view === 'library' && <LibraryView items={recent} pinnedIds={pinnedIds} historyState={historyState} onOpen={openSummary} onPin={togglePin} />}
      {view === 'detail' && <DetailView item={selected} target={detailTarget} loading={detailLoading} error={detailError} pinned={Boolean(selected && pinnedIds.includes(selected.id))} onPin={togglePin} headingRef={resultHeading} onBack={closeDetail} onNew={newSummary} onRetry={() => detailTarget && void openSummary(detailTarget)} />}
    </main>
  </div>
}

function SidebarSection({ title, items, empty, onOpen }: { title: string; items: Summary[]; empty: string; onOpen: (item: Summary, trigger?: HTMLButtonElement) => void }) { return <section className="sidebar-section"><h2>{title}</h2>{items.length === 0 ? <p>{empty}</p> : <ul>{items.map(item => <li key={item.id}><button title={titleFor(item)} data-detail-trigger={`${item.id}:sidebar:${title}`} onClick={event => void onOpen(item, event.currentTarget)}>{titleFor(item)}</button></li>)}</ul>}</section> }
function LibraryView({ items, pinnedIds, historyState, onOpen, onPin }: { items: Summary[]; pinnedIds: string[]; historyState: 'loading' | 'ready' | 'error'; onOpen: (item: Summary, trigger?: HTMLButtonElement) => void; onPin: (id: string) => void }) { return <section className="library-view" aria-labelledby="library-title"><div className="workspace-heading"><p className="section-kicker">Kitaplık</p><h1 id="library-title">Son özetleriniz</h1><p>API şu anda yalnız en yeni üç başarılı özetin kompakt listesini sunuyor.</p></div>{historyState === 'loading' && <p role="status">Özetler yükleniyor…</p>}{historyState === 'error' && <p className="error">Özetler şu anda yüklenemedi.</p>}{historyState === 'ready' && items.length === 0 && <div className="empty-state"><LibraryIcon /><h2>Kitaplığınız henüz boş</h2><p>İlk özetinizi oluşturduğunuzda burada görünecek.</p></div>}<div className="summary-grid">{items.map(item => <SummaryCard key={item.id} item={item} pinned={pinnedIds.includes(item.id)} onOpen={onOpen} onPin={onPin} />)}</div></section> }
function SummaryCard({ item, pinned, onOpen, onPin }: { item: Summary; pinned: boolean; onOpen: (item: Summary, trigger?: HTMLButtonElement) => void; onPin: (id: string) => void }) { return <article className="summary-card"><div className="card-top"><span className="language-badge">{item.language === 'Turkish' ? 'Türkçe' : 'English'}</span><button className="pin-button icon-button" aria-label={pinned ? 'Sabitlemeyi kaldır' : 'Özeti sabitle'} aria-pressed={pinned} onClick={() => onPin(item.id)}><PinIcon /></button></div><h2><button data-detail-trigger={`${item.id}:title`} onClick={event => void onOpen(item, event.currentTarget)}>{titleFor(item)}</button></h2><time dateTime={item.createdAtUtc}>{formatDate(item.createdAtUtc)}</time><p>{preview(item.summary)}</p><button className="text-button" data-detail-trigger={`${item.id}:open`} onClick={event => void onOpen(item, event.currentTarget)}>Tam özeti aç</button></article> }
function DetailView({ item, target, loading, error, pinned, onPin, headingRef, onBack, onNew, onRetry }: { item: SummaryDetail | null; target: Summary | null; loading: boolean; error: string; pinned: boolean; onPin: (id: string) => void; headingRef: RefObject<HTMLHeadingElement | null>; onBack: () => void; onNew: () => void; onRetry: () => void }) { if (loading) return <section className="summary-detail detail-state" aria-live="polite"><button className="text-button" onClick={onBack}>← Geri dön</button><p role="status">Özet ayrıntıları yükleniyor…</p></section>; if (error || !item) return <section className="summary-detail detail-state"><button className="text-button" onClick={onBack}>← Geri dön</button><p className="error" role="alert">{error || 'Özet ayrıntıları kullanılamıyor.'}</p>{target && <button className="text-button" onClick={onRetry}>Yeniden dene</button>}</section>; return <article className="summary-detail"><div className="detail-toolbar"><button className="text-button" onClick={onBack}>← Geri dön</button><button className="text-button" onClick={onNew}>Yeni özet</button><button className="pin-action" aria-pressed={pinned} onClick={() => onPin(item.id)}><PinIcon />{pinned ? 'Sabitlemeyi kaldır' : 'Sabitle'}</button></div><span className="language-badge">{item.language === 'Turkish' ? 'Türkçe' : 'English'}</span><h1 ref={headingRef} tabIndex={-1}>{titleFor(item)}</h1><time dateTime={item.createdAtUtc}>{formatDate(item.createdAtUtc)}</time>{item.promptVersion && <p className="template-version">Şablon sürümü: {item.promptVersion}</p>}<section className="detail-section" aria-labelledby="source-text-title"><h2 id="source-text-title">Kaynak metin</h2><div className="source-content preserve-lines">{item.inputText}</div></section><section className="detail-section" aria-labelledby="generated-summary-title"><h2 id="generated-summary-title">Oluşturulan özet</h2><div className="summary-content preserve-lines">{item.summary}</div></section></article> }
function titleFor(item: Summary | SummaryDetail) { return deriveSummaryTitle('inputText' in item ? item.inputText : undefined, item.summary, item.language) }
function preview(value: string) { const compact = value.replace(/\s+/g, ' ').trim(); return compact.length > 120 ? `${compact.slice(0, 117)}…` : compact }
function formatDate(value: string) { const date = new Date(value); return Number.isNaN(date.getTime()) ? 'Tarih belirtilmemiş' : new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium', timeStyle: 'short' }).format(date) }
function readPinnedIds() { try { const value = JSON.parse(localStorage.getItem(pinStorageKey) ?? '[]'); return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string').slice(0, 100) : [] } catch { return [] } }
function isNarrowViewport() { return typeof window !== 'undefined' && Boolean(window.matchMedia?.('(max-width: 760px)').matches) }

function MenuIcon() { return <svg aria-hidden="true" viewBox="0 0 24 24"><path d="M4 7h16M4 12h16M4 17h16" /></svg> }
function CloseIcon() { return <svg aria-hidden="true" viewBox="0 0 24 24"><path d="m6 6 12 12M18 6 6 18" /></svg> }
function PlusIcon() { return <svg aria-hidden="true" viewBox="0 0 24 24"><path d="M12 5v14M5 12h14" /></svg> }
function LibraryIcon() { return <svg aria-hidden="true" viewBox="0 0 24 24"><path d="M4 5.5A2.5 2.5 0 0 1 6.5 3H20v16H6.5A2.5 2.5 0 0 0 4 21.5zM4 5.5v16M8 7h8" /></svg> }
function PinIcon() { return <svg aria-hidden="true" viewBox="0 0 24 24"><path d="m14 4 6 6-3 1-4 4-1 5-2-2-4 4-1-1 4-4-2-2 5-1 4-4z" /></svg> }
function LogoutIcon() { return <svg aria-hidden="true" viewBox="0 0 24 24"><path d="M10 5H5v14h5M14 8l4 4-4 4M8 12h10" /></svg> }
function AdminIcon() { return <svg aria-hidden="true" viewBox="0 0 24 24"><path d="M12 3 4 6v5c0 5 3.4 8.5 8 10 4.6-1.5 8-5 8-10V6zM9 12l2 2 4-4" /></svg> }

export function AdminCheckPage() { return <section className="page-card"><h1>Yönetici yetkilendirmesi</h1><p>Yönetim deneyimi sonraki geliştirme aşamasında tamamlanacaktır.</p></section> }
