import { useCallback, useEffect, useRef, useState, type FormEvent, type RefObject } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/Auth'
import { deriveSummaryTitle, type SummaryLanguage } from '../summaryTitles'

type SummaryFeedback = 'Useful' | 'NotUseful'
type Summary = { id: string; summary: string; language: SummaryLanguage; createdAtUtc: string; expiresAtUtc: string; feedback?: SummaryFeedback | null }
type SummaryDetail = Summary & { inputText: string; promptVersion?: string; feedbackUpdatedAtUtc?: string | null }
type View = 'workspace' | 'library' | 'detail'
const maxLength = 12000
const pinStorageKey = 'ifs-aiweb:pinned-summary-ids'

export function AppPage() {
  const { user, logout, request } = useAuth(); const navigate = useNavigate()
  const [text, setText] = useState(''); const [language, setLanguage] = useState<SummaryLanguage>('Turkish'); const [busy, setBusy] = useState(false)
  const [selected, setSelected] = useState<SummaryDetail | null>(null); const [detailTarget, setDetailTarget] = useState<Summary | null>(null); const [view, setView] = useState<View>('workspace')
  const [detailLoading, setDetailLoading] = useState(false); const [detailError, setDetailError] = useState('')
  const [feedbackPending, setFeedbackPending] = useState(false); const [feedbackError, setFeedbackError] = useState('')
  const [pdfPending, setPdfPending] = useState(false); const [pdfError, setPdfError] = useState('')
  const [error, setError] = useState(''); const [retryAfterSeconds, setRetryAfterSeconds] = useState(0); const [recent, setRecent] = useState<Summary[]>([]); const [historyState, setHistoryState] = useState<'loading' | 'ready' | 'error'>('loading')
  const [narrow, setNarrow] = useState(isNarrowViewport); const [sidebarOpen, setSidebarOpen] = useState(() => !isNarrowViewport())
  const [pinnedIds, setPinnedIds] = useState<string[]>(readPinnedIds); const resultHeading = useRef<HTMLHeadingElement>(null); const sidebarToggle = useRef<HTMLButtonElement>(null)
  const detailCache = useRef(new Map<string, SummaryDetail>()); const pendingDetailId = useRef<string | null>(null); const detailTriggerKey = useRef<string | null>(null); const returnView = useRef<'workspace' | 'library'>('workspace')

  const loadRecent = useCallback(async () => { setHistoryState('loading'); try { const response = await request('/api/summaries/recent'); if (!response.ok) throw new Error(); setRecent(await response.json() as Summary[]); setHistoryState('ready') } catch { setHistoryState('error') } }, [request])
  useEffect(() => { let active = true; void request('/api/summaries/recent').then(async response => { if (!response.ok) throw new Error(); const data = await response.json() as Summary[]; if (active) { setRecent(data); setHistoryState('ready') } }).catch(() => { if (active) setHistoryState('error') }); return () => { active = false } }, [request])
  useEffect(() => { if (!sidebarOpen) return; const escape = (event: KeyboardEvent) => { if (event.key === 'Escape') { setSidebarOpen(false); sidebarToggle.current?.focus() } }; document.addEventListener('keydown', escape); return () => document.removeEventListener('keydown', escape) }, [sidebarOpen])
  useEffect(() => { if (!window.matchMedia) return; const media = window.matchMedia('(max-width: 760px)'); const update = () => setNarrow(media.matches); media.addEventListener?.('change', update); return () => media.removeEventListener?.('change', update) }, [])
  useEffect(() => { if (retryAfterSeconds <= 0) return; const timer = window.setInterval(() => setRetryAfterSeconds(current => Math.max(0, current - 1)), 1000); return () => window.clearInterval(timer) }, [retryAfterSeconds])

  function newSummary() { setText(''); setLanguage('Turkish'); setSelected(null); setDetailTarget(null); setDetailError(''); setFeedbackError(''); setPdfError(''); setError(''); setView('workspace'); closeMobileSidebar() }
  async function openSummary(item: Summary, trigger?: HTMLButtonElement) {
    if (pendingDetailId.current === item.id) return
    if (view !== 'detail') returnView.current = view
    if (trigger) detailTriggerKey.current = trigger.dataset.detailTrigger ?? null
    setDetailTarget(item); setSelected(null); setDetailError(''); setFeedbackError(''); setPdfError(''); setView('detail'); closeMobileSidebar()
    const cached = detailCache.current.get(item.id)
    if (cached) { setSelected(cached); queueMicrotask(() => resultHeading.current?.focus()); return }
    pendingDetailId.current = item.id; setDetailLoading(true)
    try {
      const response = await request(`/api/summaries/${encodeURIComponent(item.id)}`)
      if (!response.ok) throw new Error()
      const detail = await response.json() as SummaryDetail; detailCache.current.set(item.id, detail); setSelected(detail)
      queueMicrotask(() => resultHeading.current?.focus())
    } catch { setDetailError('Bu özet bulunamadı veya saklama süresi dolmuş olabilir.') }
    finally { pendingDetailId.current = null; setDetailLoading(false) }
  }
  function closeDetail() { const triggerKey = detailTriggerKey.current; setView(returnView.current); setSelected(null); setDetailTarget(null); setDetailError(''); setPdfError(''); queueMicrotask(() => { if (!triggerKey) return; [...document.querySelectorAll<HTMLButtonElement>('[data-detail-trigger]')].find(button => button.dataset.detailTrigger === triggerKey)?.focus() }) }
  function togglePin(id: string) { setPinnedIds(current => { const next = current.includes(id) ? current.filter(value => value !== id) : [...current, id]; localStorage.setItem(pinStorageKey, JSON.stringify(next)); return next }) }
  async function submitFeedback(value: SummaryFeedback) {
    if (!selected || feedbackPending) return
    setFeedbackPending(true); setFeedbackError('')
    try {
      const response = await request(`/api/summaries/${encodeURIComponent(selected.id)}/feedback`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ value }) })
      if (!response.ok) throw new Error()
      const confirmed = await response.json() as { value: SummaryFeedback; updatedAtUtc: string }
      const updated = { ...selected, feedback: confirmed.value, feedbackUpdatedAtUtc: confirmed.updatedAtUtc }
      setSelected(current => current?.id === updated.id ? updated : current); setDetailTarget(current => current?.id === updated.id ? { ...current, feedback: confirmed.value } : current)
      detailCache.current.set(updated.id, updated); setRecent(current => current.map(item => item.id === updated.id ? { ...item, feedback: confirmed.value } : item))
    } catch { setFeedbackError('Değerlendirmeniz kaydedilemedi. Lütfen yeniden deneyin.') }
    finally { setFeedbackPending(false) }
  }
  async function downloadPdf(id: string) {
    if (pdfPending) return
    setPdfPending(true); setPdfError('')
    try {
      const response = await request(`/api/summaries/${encodeURIComponent(id)}/pdf`)
      if (!response.ok) {
        if (response.status === 404) {
          setPdfError('Bu özetin PDF dosyası bulunamadı veya saklama süresi dolmuş.')
        } else {
          setPdfError('PDF dosyası şu anda indirilemedi. Lütfen yeniden deneyin.')
        }
        return
      }
      const blob = await response.blob()
      const url = window.URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.setAttribute('download', `IFS-Summary-${id}.pdf`)
      document.body.appendChild(link)
      link.click()
      link.remove()
      window.URL.revokeObjectURL(url)
    } catch {
      setPdfError('PDF dosyası şu anda indirilemedi. Lütfen yeniden deneyin.')
    } finally {
      setPdfPending(false)
    }
  }
  function closeMobileSidebar() { if (narrow) setSidebarOpen(false) }
  async function submit(event: FormEvent) {
    event.preventDefault(); const validationError = validateSummaryText(text); if (busy || retryAfterSeconds > 0 || validationError) { if (validationError && text.length > 0 && retryAfterSeconds === 0) setError(validationError); return } setBusy(true); setError('')
    try { const response = await request('/api/summaries', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ text, language }) }); if (!response.ok) { const failure = await summaryRequestFailure(response); if (failure.retryAfterSeconds > 0) { setRetryAfterSeconds(failure.retryAfterSeconds); return } throw new Error(failure.message) } const data = await response.json() as Summary; const complete: SummaryDetail = { ...data, inputText: text }; detailCache.current.set(complete.id, complete); returnView.current = 'workspace'; setDetailTarget(data); setSelected(complete); setView('detail'); queueMicrotask(() => resultHeading.current?.focus()); void loadRecent() } catch (reason) { setError(reason instanceof Error ? reason.message : 'Özet oluşturulamadı.') } finally { setBusy(false) }
  }
  async function signOut() { await logout(); navigate('/login', { replace: true }) }

  const pinned = recent.filter(item => pinnedIds.includes(item.id)); const validationError = validateSummaryText(text); const invalid = Boolean(validationError); const inputDescription = text.length > 0 && validationError ? 'character-count input-validation adaptive-length-note retention-note' : 'character-count adaptive-length-note retention-note'
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
      {view === 'workspace' && <section className="composer-view" aria-labelledby="workspace-title"><div className="workspace-heading"><p className="section-kicker">Yeni özet</p><h1 id="workspace-title">Metninizi anlaşılır bir özete dönüştürün</h1><p>Kaynak metni ekleyin, çıktı dilini seçin ve önemli noktaları hızlıca görün.</p></div><form className="summary-composer" onSubmit={submit}><label htmlFor="summary-text">Kaynak metin</label><textarea id="summary-text" value={text} onChange={event => { setText(event.target.value); if (retryAfterSeconds === 0) setError('') }} maxLength={maxLength + 1} rows={12} placeholder="Özetlemek istediğiniz metni buraya yapıştırın…" aria-invalid={text.length > 0 && invalid ? 'true' : undefined} aria-describedby={inputDescription} />{text.length > 0 && validationError && <p id="input-validation" className="error-text">{validationError}</p>}<p id="adaptive-length-note" className="retention-notice">Özet uzunluğu metninize göre otomatik belirlenir.</p><div className="composer-meta"><span id="character-count" className={text.length > maxLength ? 'error-text' : ''}>{text.length.toLocaleString('tr-TR')} / 12.000</span><fieldset className="language-selector"><legend>Özet dili</legend><label><input type="radio" name="language" value="Turkish" checked={language === 'Turkish'} onChange={() => setLanguage('Turkish')} /> Türkçe</label><label><input type="radio" name="language" value="English" checked={language === 'English'} onChange={() => setLanguage('English')} /> İngilizce</label></fieldset></div><button className="summarize-button" disabled={busy || invalid || retryAfterSeconds > 0}>{busy ? 'Özetleniyor…' : 'Özet oluştur'}</button></form><p id="retention-note" className="retention-notice">Gönderdiğiniz metin ve oluşturulan özet 30 gün saklanır. Otomatik silme görevi henüz uygulanmamıştır.</p>{retryAfterSeconds > 0 && <div className="error" role="alert" aria-live="polite"><p>Dakikada en fazla 5 özet oluşturabilirsiniz.</p><p>Yeniden deneyebilmek için {retryAfterSeconds} saniye bekleyin.</p></div>}<div aria-live="polite">{error && <p className="error" role="alert">{error}</p>}</div></section>}
      {view === 'library' && <LibraryView items={recent} pinnedIds={pinnedIds} historyState={historyState} onOpen={openSummary} onPin={togglePin} />}
      {view === 'detail' && <DetailView item={selected} target={detailTarget} loading={detailLoading} error={detailError} feedbackPending={feedbackPending} feedbackError={feedbackError} pdfPending={pdfPending} pdfError={pdfError} pinned={Boolean(selected && pinnedIds.includes(selected.id))} onFeedback={value => void submitFeedback(value)} onDownloadPdf={id => void downloadPdf(id)} onPin={togglePin} headingRef={resultHeading} onBack={closeDetail} onNew={newSummary} onRetry={() => detailTarget && void openSummary(detailTarget)} />}
    </main>
  </div>
}

function SidebarSection({ title, items, empty, onOpen }: { title: string; items: Summary[]; empty: string; onOpen: (item: Summary, trigger?: HTMLButtonElement) => void }) { return <section className="sidebar-section"><h2>{title}</h2>{items.length === 0 ? <p>{empty}</p> : <ul>{items.map(item => <li key={item.id}><button title={titleFor(item)} data-detail-trigger={`${item.id}:sidebar:${title}`} onClick={event => void onOpen(item, event.currentTarget)}>{titleFor(item)}</button></li>)}</ul>}</section> }
function LibraryView({ items, pinnedIds, historyState, onOpen, onPin }: { items: Summary[]; pinnedIds: string[]; historyState: 'loading' | 'ready' | 'error'; onOpen: (item: Summary, trigger?: HTMLButtonElement) => void; onPin: (id: string) => void }) { return <section className="library-view" aria-labelledby="library-title"><div className="workspace-heading"><p className="section-kicker">Kitaplık</p><h1 id="library-title">Son özetleriniz</h1><p>En yeni 7 özetinizi burada görüntüleyebilirsiniz.</p></div>{historyState === 'loading' && <p role="status">Özetler yükleniyor…</p>}{historyState === 'error' && <p className="error">Özetler şu anda yüklenemedi.</p>}{historyState === 'ready' && items.length === 0 && <div className="empty-state"><LibraryIcon /><h2>Kitaplığınız henüz boş</h2><p>İlk özetinizi oluşturduğunuzda burada görünecek.</p></div>}<div className="summary-grid">{items.map(item => <SummaryCard key={item.id} item={item} pinned={pinnedIds.includes(item.id)} onOpen={onOpen} onPin={onPin} />)}</div></section> }
function SummaryCard({ item, pinned, onOpen, onPin }: { item: Summary; pinned: boolean; onOpen: (item: Summary, trigger?: HTMLButtonElement) => void; onPin: (id: string) => void }) { return <article className="summary-card"><div className="card-top"><span className="language-badge">{item.language === 'Turkish' ? 'Türkçe' : 'English'}</span><button className="pin-button icon-button" aria-label={pinned ? 'Sabitlemeyi kaldır' : 'Özeti sabitle'} aria-pressed={pinned} onClick={() => onPin(item.id)}><PinIcon /></button></div><h2><button data-detail-trigger={`${item.id}:title`} onClick={event => void onOpen(item, event.currentTarget)}>{titleFor(item)}</button></h2><time dateTime={item.createdAtUtc}>{formatDate(item.createdAtUtc)}</time><p>{preview(item.summary)}</p>{item.feedback && <p className="feedback-status">{feedbackStatus(item.feedback)}</p>}<button className="text-button" data-detail-trigger={`${item.id}:open`} onClick={event => void onOpen(item, event.currentTarget)}>Tam özeti aç</button></article> }
function DetailView({ item, target, loading, error, feedbackPending, feedbackError, pdfPending, pdfError, pinned, onFeedback, onDownloadPdf, onPin, headingRef, onBack, onNew, onRetry }: { item: SummaryDetail | null; target: Summary | null; loading: boolean; error: string; feedbackPending: boolean; feedbackError: string; pdfPending: boolean; pdfError: string; pinned: boolean; onFeedback: (value: SummaryFeedback) => void; onDownloadPdf: (id: string) => void; onPin: (id: string) => void; headingRef: RefObject<HTMLHeadingElement | null>; onBack: () => void; onNew: () => void; onRetry: () => void }) { if (loading) return <section className="summary-detail detail-state" aria-live="polite"><button className="text-button" onClick={onBack}>← Geri dön</button><p role="status">Özet ayrıntıları yükleniyor…</p></section>; if (error || !item) return <section className="summary-detail detail-state"><button className="text-button" onClick={onBack}>← Geri dön</button><p className="error" role="alert">{error || 'Özet ayrıntıları kullanılamıyor.'}</p>{target && <button className="text-button" onClick={onRetry}>Yeniden dene</button>}</section>; return <article className="summary-detail"><div className="detail-toolbar"><button className="text-button" onClick={onBack}>← Geri dön</button><button className="text-button" onClick={onNew}>Yeni özet</button><button type="button" className="text-button pdf-download-action" disabled={pdfPending} onClick={() => onDownloadPdf(item.id)}><DownloadIcon /><span>{pdfPending ? 'İndiriliyor…' : 'PDF İndir'}</span></button><button className="pin-action" aria-pressed={pinned} onClick={() => onPin(item.id)}><PinIcon />{pinned ? 'Sabitlemeyi kaldır' : 'Sabitle'}</button></div>{pdfError && <p className="error" role="alert">{pdfError}</p>}<span className="language-badge">{item.language === 'Turkish' ? 'Türkçe' : 'English'}</span><h1 ref={headingRef} tabIndex={-1}>{titleFor(item)}</h1><time dateTime={item.createdAtUtc}>{formatDate(item.createdAtUtc)}</time>{item.promptVersion && <p className="template-version">Şablon sürümü: {item.promptVersion}</p>}<section className="detail-section" aria-labelledby="source-text-title"><h2 id="source-text-title">Kaynak metin</h2><div className="source-content preserve-lines">{item.inputText}</div></section><section className="detail-section" aria-labelledby="generated-summary-title"><h2 id="generated-summary-title">Oluşturulan özet</h2><div className="summary-content preserve-lines">{item.summary}</div></section><section className="feedback-panel" aria-labelledby="feedback-title"><h2 id="feedback-title">Bu özet faydalı mı?</h2><div className="feedback-actions"><button type="button" aria-pressed={item.feedback === 'Useful'} disabled={feedbackPending} onClick={() => onFeedback('Useful')}>Faydalı</button><button type="button" aria-pressed={item.feedback === 'NotUseful'} disabled={feedbackPending} onClick={() => onFeedback('NotUseful')}>Faydalı değil</button></div>{item.feedback && <p className="feedback-confirmation" role="status">{feedbackStatus(item.feedback)}</p>}{feedbackError && <p className="error" role="alert">{feedbackError}</p>}</section></article> }
function feedbackStatus(value: SummaryFeedback) { return value === 'Useful' ? 'Faydalı olarak değerlendirildi' : 'Faydalı değil olarak değerlendirildi' }
function titleFor(item: Summary | SummaryDetail) { return deriveSummaryTitle('inputText' in item ? item.inputText : undefined, item.summary, item.language) }
function preview(value: string) { const compact = value.replace(/\s+/g, ' ').trim(); return compact.length > 120 ? `${compact.slice(0, 117)}…` : compact }
function formatDate(value: string) { const date = new Date(value); return Number.isNaN(date.getTime()) ? 'Tarih belirtilmemiş' : new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium', timeStyle: 'short' }).format(date) }
function validateSummaryText(value: string) { if (!value.trim()) return 'Özetlenecek metni girin.'; if (value.length > maxLength) return 'Metin en fazla 12.000 karakter olabilir.'; if (!/\p{L}/u.test(value)) return 'Metin en az bir harf içermelidir.'; const normalized = [...value].filter(character => /[\p{L}\p{N}]/u.test(character)); if (normalized.length >= 8 && normalized.every(character => character === normalized[0])) return 'Metin aynı karakterin uzun tekrarından oluşamaz.'; const words = value.match(/[\p{L}\p{N}]*\p{L}[\p{L}\p{N}]*/gu) ?? []; if (words.length < 2) return 'Özetlemek için en az iki kelimeden oluşan bir metin girin.'; return '' }
async function summaryRequestFailure(response: Response): Promise<{ message: string; retryAfterSeconds: number }> { if (response.status === 429) { const headerSeconds = positiveSeconds(response.headers.get('Retry-After')); let bodySeconds = 0; try { const body = await response.json() as { retryAfterSeconds?: unknown }; bodySeconds = typeof body.retryAfterSeconds === 'number' ? positiveSeconds(String(body.retryAfterSeconds)) : 0 } catch { /* Header or safe fallback below. */ } return { message: '', retryAfterSeconds: headerSeconds || bodySeconds || 60 } } if (response.status === 413) return { message: 'Gönderilen metin isteği çok büyük. Metni kısaltıp yeniden deneyin.', retryAfterSeconds: 0 }; if (response.status === 422) return { message: 'Bu metinde özetlenebilecek yeterli ve anlamlı içerik bulunamadı. Lütfen daha açıklayıcı bir metin girin.', retryAfterSeconds: 0 }; if (response.status === 502) return { message: 'Özetleme hizmetine şu anda ulaşılamıyor. Lütfen yeniden deneyin.', retryAfterSeconds: 0 }; if (response.status === 400) { try { const body = await response.json() as { errors?: unknown }; if (body.errors && typeof body.errors === 'object' && !Array.isArray(body.errors)) for (const key of ['text', 'language']) { const messages = (body.errors as Record<string, unknown>)[key]; if (Array.isArray(messages)) { const safe = messages.find((message): message is string => typeof message === 'string' && message.length <= 300); if (safe) return { message: safe, retryAfterSeconds: 0 } } } } catch { /* Safe fallback below. */ } return { message: 'Metni ve özet dilini kontrol edip yeniden deneyin.', retryAfterSeconds: 0 } } return { message: 'Özet şu anda oluşturulamadı. Lütfen yeniden deneyin.', retryAfterSeconds: 0 } }
function positiveSeconds(value: string | null) { if (!value || !/^\d+$/.test(value)) return 0; const seconds = Number(value); return Number.isSafeInteger(seconds) && seconds > 0 && seconds <= 3600 ? seconds : 0 }
function readPinnedIds() { try { const value = JSON.parse(localStorage.getItem(pinStorageKey) ?? '[]'); return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string').slice(0, 100) : [] } catch { return [] } }
function isNarrowViewport() { return typeof window !== 'undefined' && Boolean(window.matchMedia?.('(max-width: 760px)').matches) }

function MenuIcon() { return <svg aria-hidden="true" viewBox="0 0 24 24"><path d="M4 7h16M4 12h16M4 17h16" /></svg> }
function CloseIcon() { return <svg aria-hidden="true" viewBox="0 0 24 24"><path d="m6 6 12 12M18 6 6 18" /></svg> }
function PlusIcon() { return <svg aria-hidden="true" viewBox="0 0 24 24"><path d="M12 5v14M5 12h14" /></svg> }
function LibraryIcon() { return <svg aria-hidden="true" viewBox="0 0 24 24"><path d="M4 5.5A2.5 2.5 0 0 1 6.5 3H20v16H6.5A2.5 2.5 0 0 0 4 21.5zM4 5.5v16M8 7h8" /></svg> }
function PinIcon() { return <svg aria-hidden="true" viewBox="0 0 24 24"><path d="m14 4 6 6-3 1-4 4-1 5-2-2-4 4-1-1 4-4-2-2 5-1 4-4z" /></svg> }
function LogoutIcon() { return <svg aria-hidden="true" viewBox="0 0 24 24"><path d="M10 5H5v14h5M14 8l4 4-4 4M8 12h10" /></svg> }
function AdminIcon() { return <svg aria-hidden="true" viewBox="0 0 24 24"><path d="M12 3 4 6v5c0 5 3.4 8.5 8 10 4.6-1.5 8-5 8-10V6zM9 12l2 2 4-4" /></svg> }
function DownloadIcon() { return <svg aria-hidden="true" viewBox="0 0 24 24"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3" /></svg> }

export function AdminCheckPage() { return <section className="page-card"><h1>Yönetici yetkilendirmesi</h1><p>Yönetim deneyimi sonraki geliştirme aşamasında tamamlanacaktır.</p></section> }
