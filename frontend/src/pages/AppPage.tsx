import { useCallback, useEffect, useRef, useState, type FormEvent, type RefObject } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/Auth'
import { deriveSummaryTitle, type SummaryLanguage } from '../summaryTitles'

type Summary = { id: string; inputText?: string; summary: string; language: SummaryLanguage; createdAtUtc: string; expiresAtUtc: string }
type View = 'workspace' | 'library' | 'detail'
const maxLength = 12000
const pinStorageKey = 'ifs-aiweb:pinned-summary-ids'

export function AppPage() {
  const { user, logout, request } = useAuth(); const navigate = useNavigate()
  const [text, setText] = useState(''); const [language, setLanguage] = useState<SummaryLanguage>('Turkish'); const [busy, setBusy] = useState(false)
  const [result, setResult] = useState<Summary | null>(null); const [selected, setSelected] = useState<Summary | null>(null); const [view, setView] = useState<View>('workspace')
  const [error, setError] = useState(''); const [recent, setRecent] = useState<Summary[]>([]); const [historyState, setHistoryState] = useState<'loading' | 'ready' | 'error'>('loading')
  const [narrow, setNarrow] = useState(isNarrowViewport); const [sidebarOpen, setSidebarOpen] = useState(() => !isNarrowViewport())
  const [pinnedIds, setPinnedIds] = useState<string[]>(readPinnedIds); const resultHeading = useRef<HTMLHeadingElement>(null); const sidebarToggle = useRef<HTMLButtonElement>(null)

  const loadRecent = useCallback(async () => { setHistoryState('loading'); try { const response = await request('/api/summaries/recent'); if (!response.ok) throw new Error(); setRecent(await response.json() as Summary[]); setHistoryState('ready') } catch { setHistoryState('error') } }, [request])
  useEffect(() => { let active = true; void request('/api/summaries/recent').then(async response => { if (!response.ok) throw new Error(); const data = await response.json() as Summary[]; if (active) { setRecent(data); setHistoryState('ready') } }).catch(() => { if (active) setHistoryState('error') }); return () => { active = false } }, [request])
  useEffect(() => { if (!sidebarOpen) return; const escape = (event: KeyboardEvent) => { if (event.key === 'Escape') { setSidebarOpen(false); sidebarToggle.current?.focus() } }; document.addEventListener('keydown', escape); return () => document.removeEventListener('keydown', escape) }, [sidebarOpen])
  useEffect(() => { if (!window.matchMedia) return; const media = window.matchMedia('(max-width: 760px)'); const update = () => setNarrow(media.matches); media.addEventListener?.('change', update); return () => media.removeEventListener?.('change', update) }, [])

  function newSummary() { setText(''); setLanguage('Turkish'); setResult(null); setSelected(null); setError(''); setView('workspace'); closeMobileSidebar() }
  function openSummary(item: Summary) { setSelected(item); setView('detail'); closeMobileSidebar(); queueMicrotask(() => resultHeading.current?.focus()) }
  function togglePin(id: string) { setPinnedIds(current => { const next = current.includes(id) ? current.filter(value => value !== id) : [...current, id]; localStorage.setItem(pinStorageKey, JSON.stringify(next)); return next }) }
  function closeMobileSidebar() { if (narrow) setSidebarOpen(false) }
  async function submit(event: FormEvent) {
    event.preventDefault(); if (busy || !text.trim() || text.length > maxLength) return; setBusy(true); setError('')
    try { const response = await request('/api/summaries', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ text, language }) }); if (!response.ok) throw new Error(response.status === 429 ? 'İstek sınırına ulaştınız. Lütfen bir dakika sonra yeniden deneyin.' : 'Özet şu anda oluşturulamadı. Lütfen yeniden deneyin.'); const data = await response.json() as Summary; const complete = { ...data, inputText: text }; setResult(complete); setSelected(complete); setView('detail'); queueMicrotask(() => resultHeading.current?.focus()); void loadRecent() } catch (reason) { setError(reason instanceof Error ? reason.message : 'Özet oluşturulamadı.') } finally { setBusy(false) }
  }
  async function signOut() { await logout(); navigate('/login', { replace: true }) }

  const pinned = recent.filter(item => pinnedIds.includes(item.id)); const activeSummary = selected ?? result; const invalid = !text.trim() || text.length > maxLength
  return <div className={`product-shell ${sidebarOpen ? 'sidebar-open' : 'sidebar-collapsed'}`}>
    <button ref={sidebarToggle} className="sidebar-toggle icon-button" aria-label={sidebarOpen ? 'Kenar çubuğunu kapat' : 'Kenar çubuğunu aç'} aria-expanded={sidebarOpen} aria-controls="product-sidebar" onClick={() => setSidebarOpen(value => !value)}><MenuIcon /></button>
    {sidebarOpen && narrow && <button className="drawer-overlay" aria-label="Kenar çubuğunu kapat" onClick={() => { setSidebarOpen(false); sidebarToggle.current?.focus() }} />}
    <aside id="product-sidebar" className="product-sidebar" aria-label="Çalışma alanı menüsü">
      <div className="sidebar-brand"><span className="brand-mark" aria-hidden="true">IA</span><strong>IFS AI-Web</strong>{narrow && <button className="sidebar-close icon-button" aria-label="Kenar çubuğunu kapat" onClick={() => { setSidebarOpen(false); sidebarToggle.current?.focus() }}><CloseIcon /></button>}</div>
      <button className="new-summary-button" aria-label="Yeni özet" title="Yeni özet" onClick={newSummary}><PlusIcon /><span>Yeni özet</span></button>
      <nav className="workspace-nav" aria-label="Özetler">
        <button className={view === 'library' ? 'nav-action active' : 'nav-action'} aria-label="Kitaplık" title="Kitaplık" onClick={() => { setView('library'); setSelected(null); closeMobileSidebar() }}><LibraryIcon /><span>Kitaplık</span></button>
        <SidebarSection title="Sabitlenen özetler" items={pinned} empty="Henüz sabitlenen özet yok." onOpen={openSummary} />
        <SidebarSection title="Son özetler" items={recent} empty={historyState === 'loading' ? 'Yükleniyor…' : 'Henüz özet yok.'} onOpen={openSummary} />
      </nav>
      <div className="account-area"><div className="avatar" aria-hidden="true">{user?.firstName?.[0]}{user?.lastName?.[0]}</div><div className="account-copy"><strong>{user?.firstName} {user?.lastName}</strong><span>@{user?.username}</span></div><button className="logout-button icon-button" aria-label="Çıkış yap" title="Çıkış yap" onClick={() => void signOut()}><LogoutIcon /></button></div>
    </aside>
    <main className="workspace-main">
      {view === 'workspace' && <section className="composer-view" aria-labelledby="workspace-title"><div className="workspace-heading"><p className="section-kicker">Yeni özet</p><h1 id="workspace-title">Metninizi anlaşılır bir özete dönüştürün</h1><p>Kaynak metni ekleyin, çıktı dilini seçin ve önemli noktaları hızlıca görün.</p></div><form className="summary-composer" onSubmit={submit}><label htmlFor="summary-text">Kaynak metin</label><textarea id="summary-text" value={text} onChange={event => setText(event.target.value)} maxLength={maxLength + 1} rows={12} placeholder="Özetlemek istediğiniz metni buraya yapıştırın…" aria-describedby="character-count retention-note" /><div className="composer-meta"><span id="character-count" className={text.length > maxLength ? 'error-text' : ''}>{text.length.toLocaleString('tr-TR')} / 12.000</span><fieldset className="language-selector"><legend>Özet dili</legend><label><input type="radio" name="language" value="Turkish" checked={language === 'Turkish'} onChange={() => setLanguage('Turkish')} /> Türkçe</label><label><input type="radio" name="language" value="English" checked={language === 'English'} onChange={() => setLanguage('English')} /> İngilizce</label></fieldset></div><button className="summarize-button" disabled={busy || invalid}>{busy ? 'Özetleniyor…' : 'Özet oluştur'}</button></form><p id="retention-note" className="retention-notice">Gönderdiğiniz metin ve oluşturulan özet 30 gün saklanır. Otomatik silme görevi henüz uygulanmamıştır.</p><div aria-live="polite">{error && <p className="error" role="alert">{error}</p>}</div></section>}
      {view === 'library' && <LibraryView items={recent} pinnedIds={pinnedIds} historyState={historyState} onOpen={openSummary} onPin={togglePin} />}
      {view === 'detail' && activeSummary && <SummaryDetail item={activeSummary} pinned={pinnedIds.includes(activeSummary.id)} onPin={togglePin} headingRef={resultHeading} onNew={newSummary} />}
    </main>
  </div>
}

function SidebarSection({ title, items, empty, onOpen }: { title: string; items: Summary[]; empty: string; onOpen: (item: Summary) => void }) { return <section className="sidebar-section"><h2>{title}</h2>{items.length === 0 ? <p>{empty}</p> : <ul>{items.map(item => <li key={item.id}><button title={titleFor(item)} onClick={() => onOpen(item)}>{titleFor(item)}</button></li>)}</ul>}</section> }
function LibraryView({ items, pinnedIds, historyState, onOpen, onPin }: { items: Summary[]; pinnedIds: string[]; historyState: 'loading' | 'ready' | 'error'; onOpen: (item: Summary) => void; onPin: (id: string) => void }) { return <section className="library-view" aria-labelledby="library-title"><div className="workspace-heading"><p className="section-kicker">Kitaplık</p><h1 id="library-title">Son özetleriniz</h1><p>API şu anda yalnız en yeni üç başarılı özeti sunuyor.</p></div>{historyState === 'loading' && <p role="status">Özetler yükleniyor…</p>}{historyState === 'error' && <p className="error">Özetler şu anda yüklenemedi.</p>}{historyState === 'ready' && items.length === 0 && <div className="empty-state"><LibraryIcon /><h2>Kitaplığınız henüz boş</h2><p>İlk özetinizi oluşturduğunuzda burada görünecek.</p></div>}<div className="summary-grid">{items.map(item => <SummaryCard key={item.id} item={item} pinned={pinnedIds.includes(item.id)} onOpen={onOpen} onPin={onPin} />)}</div></section> }
function SummaryCard({ item, pinned, onOpen, onPin }: { item: Summary; pinned: boolean; onOpen: (item: Summary) => void; onPin: (id: string) => void }) { return <article className="summary-card"><div className="card-top"><span className="language-badge">{item.language === 'Turkish' ? 'Türkçe' : 'English'}</span><button className="pin-button icon-button" aria-label={pinned ? 'Sabitlemeyi kaldır' : 'Özeti sabitle'} aria-pressed={pinned} onClick={() => onPin(item.id)}><PinIcon /></button></div><h2><button onClick={() => onOpen(item)}>{titleFor(item)}</button></h2><time dateTime={item.createdAtUtc}>{formatDate(item.createdAtUtc)}</time><p>{preview(item.summary)}</p><button className="text-button" onClick={() => onOpen(item)}>Tam özeti aç</button></article> }
function SummaryDetail({ item, pinned, onPin, headingRef, onNew }: { item: Summary; pinned: boolean; onPin: (id: string) => void; headingRef: RefObject<HTMLHeadingElement | null>; onNew: () => void }) { return <article className="summary-detail"><div className="detail-toolbar"><button className="text-button" onClick={onNew}>← Yeni özete dön</button><button className="pin-action" aria-pressed={pinned} onClick={() => onPin(item.id)}><PinIcon />{pinned ? 'Sabitlemeyi kaldır' : 'Sabitle'}</button></div><span className="language-badge">{item.language === 'Turkish' ? 'Türkçe' : 'English'}</span><h1 ref={headingRef} tabIndex={-1}>{titleFor(item)}</h1><time dateTime={item.createdAtUtc}>{formatDate(item.createdAtUtc)}</time><div className="summary-content preserve-lines">{item.summary}</div></article> }
function titleFor(item: Summary) { return deriveSummaryTitle(item.inputText, item.summary, item.language) }
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

export function AdminCheckPage() { return <section className="page-card"><h1>Yönetici yetkilendirmesi</h1><p>Yönetim deneyimi sonraki geliştirme aşamasında tamamlanacaktır.</p></section> }
