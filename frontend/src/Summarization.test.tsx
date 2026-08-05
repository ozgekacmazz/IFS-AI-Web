import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const user = { username: 'member', firstName: 'Test', lastName: 'User', role: 'User' }
const response = (body: unknown, status = 200, headers: Record<string, string> = {}) => Promise.resolve(new Response(status === 204 ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json', ...headers } }))
function renderApp(recent: unknown[] = []) { const fetchMock = vi.fn().mockImplementationOnce(() => response({ accessToken: 'opaque', user })).mockImplementationOnce(() => response(recent)); vi.stubGlobal('fetch', fetchMock); render(<MemoryRouter initialEntries={['/app']}><App /></MemoryRouter>); return fetchMock }
afterEach(() => { cleanup(); vi.useRealTimers(); vi.restoreAllMocks() })

describe('summarization experience', () => {
  it('renders authenticated controls, adaptive-length explanation, empty history and retention disclosure', async () => { renderApp(); expect(await screen.findByLabelText('Kaynak metin')).toBeVisible(); expect(screen.getByText('Özet uzunluğu metninize göre otomatik belirlenir.')).toBeVisible(); expect(screen.getByText(/30 gün boyunca güvenle saklanır/)).toBeVisible(); expect(await screen.findByText('Henüz özet yok.')).toBeVisible(); expect(screen.getByRole('button', { name: 'Özet oluştur' })).toBeDisabled() })
  it('renders all seven compact recent summaries returned by the API', async () => { const items = Array.from({ length: 7 }, (_, index) => ({ id: `recent-${index}`, summary: `Güvenli özet ${index + 1}`, language: 'Turkish', createdAtUtc: new Date(2026, 7, 3, 12, index).toISOString(), expiresAtUtc: new Date(2026, 8, 2, 12, index).toISOString() })); renderApp(items); fireEvent.click(await screen.findByRole('button', { name: 'Kitaplık' })); expect(await screen.findByText('En yeni 7 özetinizi burada görüntüleyebilirsiniz.')).toBeVisible(); expect(screen.getAllByRole('button', { name: 'Tam özeti aç' })).toHaveLength(7); expect(screen.queryByText(/Kaynak metin:/)).not.toBeInTheDocument() })
  it('updates count and blocks over-limit input', async () => { renderApp(); const area = await screen.findByLabelText('Kaynak metin'); fireEvent.change(area, { target: { value: 'abc' } }); expect(screen.getByText('3 / 12.000')).toBeVisible(); fireEvent.change(area, { target: { value: 'x'.repeat(12001) } }); expect(screen.getByRole('button', { name: 'Özet oluştur' })).toBeDisabled() })
  it.each([['12345', 'Metin en az bir harf içermelidir.'], ['!?.,---', 'Metin en az bir harf içermelidir.'], ['aaaaaaaa', 'Metin aynı karakterin uzun tekrarından oluşamaz.']])('blocks structurally invalid input without a request: %s', async (value, message) => { const fetchMock = renderApp(); const area = await screen.findByLabelText('Kaynak metin'); fireEvent.change(area, { target: { value } }); expect(screen.getByText(message)).toBeVisible(); expect(screen.getByRole('button', { name: 'Özet oluştur' })).toBeDisabled(); expect(fetchMock.mock.calls.some(call => String(call[0]).endsWith('/api/summaries') && (call[1] as RequestInit)?.method === 'POST')).toBe(false) })
  it.each(['Merhaba', 'erfdv'])('blocks one-word input without a request: %s', async value => { const fetchMock = renderApp(); const area = await screen.findByLabelText('Kaynak metin'); fireEvent.change(area, { target: { value } }); expect(screen.getByText('Özetlemek için en az iki kelimeden oluşan bir metin girin.')).toBeVisible(); expect(screen.getByRole('button', { name: 'Özet oluştur' })).toBeDisabled(); expect(fetchMock.mock.calls.some(call => String(call[0]).endsWith('/api/summaries') && (call[1] as RequestInit)?.method === 'POST')).toBe(false) })
  it.each([['Türkçe', 'Turkish', 'Toplantı ertelendi.'], ['İngilizce', 'English', 'Meeting postponed.']])('submits valid short %s content', async (label, value, source) => { const fetchMock = renderApp(); fetchMock.mockImplementationOnce(() => response({ id: '1', summary: 'Güvenli özet', language: value, createdAtUtc: new Date().toISOString(), expiresAtUtc: new Date().toISOString(), feedback: null })).mockImplementationOnce(() => response([])); const area = await screen.findByLabelText('Kaynak metin'); fireEvent.change(area, { target: { value: source } }); fireEvent.click(screen.getByLabelText(label)); fireEvent.click(screen.getByRole('button', { name: 'Özet oluştur' })); expect(await screen.findByText('Güvenli özet')).toBeVisible(); const post = fetchMock.mock.calls.find(call => String(call[0]).endsWith('/api/summaries') && (call[1] as RequestInit)?.method === 'POST'); expect(JSON.parse(String((post![1] as RequestInit).body))).toEqual({ text: source, language: value }) })
  it('prevents duplicate submission while loading', async () => { const fetchMock = renderApp(); let resolve!: (value: Response) => void; fetchMock.mockImplementationOnce(() => new Promise<Response>(done => { resolve = done })); const area = await screen.findByLabelText('Kaynak metin'); fireEvent.change(area, { target: { value: 'valid source' } }); const button = screen.getByRole('button', { name: 'Özet oluştur' }); fireEvent.click(button); expect(await screen.findByRole('button', { name: 'Özetleniyor…' })).toBeDisabled(); fireEvent.click(button); expect(fetchMock.mock.calls.filter(call => String(call[0]).endsWith('/api/summaries') && (call[1] as RequestInit)?.method === 'POST')).toHaveLength(1); resolve(await response({ id: '1', summary: 'done', language: 'Turkish', createdAtUtc: '', expiresAtUtc: '' })) })
  it('shows safe errors and keeps content usable', async () => { const fetchMock = renderApp(); fetchMock.mockImplementationOnce(() => response({ detail: 'raw provider detail' }, 502)); const area = await screen.findByLabelText('Kaynak metin'); fireEvent.change(area, { target: { value: 'private input' } }); fireEvent.click(screen.getByRole('button', { name: 'Özet oluştur' })); expect(await screen.findByRole('alert')).toHaveTextContent('Özetleme hizmetine şu anda ulaşılamıyor. Lütfen yeniden deneyin.'); expect(area).toHaveValue('private input'); expect(screen.queryByText(/raw provider detail/)).not.toBeInTheDocument(); expect(screen.queryByText(/saniye bekleyin/)).not.toBeInTheDocument(); expect(screen.getByRole('button', { name: 'Özet oluştur' })).toBeEnabled() })
  it.each([[400, { errors: { text: ['Metin en az bir harf içermelidir.'], unexpected: ['raw unsafe detail'] } }, 'Metin en az bir harf içermelidir.'], [413, { detail: 'raw body' }, 'Gönderilen metin isteği çok büyük.']])('maps HTTP %s safely and preserves input', async (status, body, expected) => { const fetchMock = renderApp(); fetchMock.mockImplementationOnce(() => response(body, status as number)); const area = await screen.findByLabelText('Kaynak metin'); fireEvent.change(area, { target: { value: 'private input' } }); fireEvent.click(screen.getByRole('button', { name: 'Özet oluştur' })); expect(await screen.findByRole('alert')).toHaveTextContent(expected as string); expect(area).toHaveValue('private input'); expect(screen.queryByText(/raw unsafe detail|raw body/)).not.toBeInTheDocument() })
  it('uses authoritative 429 remaining time, blocks duplicate submits and clears the countdown', async () => { const fetchMock = renderApp(); const area = await screen.findByLabelText('Kaynak metin'); vi.useFakeTimers(); fetchMock.mockImplementationOnce(() => response({ retryAfterSeconds: 9 }, 429, { 'Retry-After': '3' })); fireEvent.change(area, { target: { value: 'private input' } }); fireEvent.click(screen.getByRole('button', { name: 'Özet oluştur' })); await act(async () => {}); expect(screen.getByRole('alert')).toHaveTextContent('Dakikada en fazla 5 özet oluşturabilirsiniz.'); expect(screen.getByRole('alert')).toHaveTextContent('Yeniden deneyebilmek için 3 saniye bekleyin.'); expect(area).toHaveValue('private input'); const button = screen.getByRole('button', { name: 'Özet oluştur' }); expect(button).toBeDisabled(); fireEvent.click(button); expect(fetchMock.mock.calls.filter(call => String(call[0]).endsWith('/api/summaries') && (call[1] as RequestInit)?.method === 'POST')).toHaveLength(1); act(() => vi.advanceTimersByTime(1000)); expect(screen.getByRole('alert')).toHaveTextContent('2 saniye'); act(() => vi.advanceTimersByTime(2000)); expect(screen.queryByText(/Yeniden deneyebilmek/)).not.toBeInTheDocument(); expect(button).toBeEnabled() })
  it('handles insufficient content safely without clearing input or adding recent history', async () => { const fetchMock = renderApp(); fetchMock.mockImplementationOnce(() => response({ code: 'insufficient_content', detail: 'raw provider wording' }, 422)); const area = await screen.findByLabelText('Kaynak metin'); fireEvent.change(area, { target: { value: 'random words here' } }); fireEvent.click(screen.getByRole('button', { name: 'Özet oluştur' })); expect(await screen.findByRole('alert')).toHaveTextContent('Bu metinde özetlenebilecek yeterli ve anlamlı içerik bulunamadı. Lütfen daha açıklayıcı bir metin girin.'); expect(area).toHaveValue('random words here'); expect(screen.queryByText('raw provider wording')).not.toBeInTheDocument(); expect(await screen.findByText('Henüz özet yok.')).toBeVisible(); expect(fetchMock.mock.calls.filter(call => String(call[0]).endsWith('/api/summaries/recent'))).toHaveLength(1) })
  it('renders provider output as text, not HTML', async () => { renderApp([{ id: '1', summary: '<img src=x onerror=alert(1)>', language: 'English', createdAtUtc: new Date().toISOString(), expiresAtUtc: new Date().toISOString() }]); fireEvent.click(await screen.findByRole('button', { name: 'Kitaplık' })); expect(await screen.findByText('<img src=x onerror=alert(1)>')).toBeVisible(); expect(document.querySelector('img')).toBeNull() })
  it('renders PDF download button in summary detail view and triggers download request', async () => {
    const createObjectURLMock = vi.fn().mockReturnValue('blob:mock-url')
    const revokeObjectURLMock = vi.fn()
    vi.stubGlobal('URL', { ...globalThis.URL, createObjectURL: createObjectURLMock, revokeObjectURL: revokeObjectURLMock })
    const fetchMock = renderApp()
    fetchMock.mockImplementationOnce(() => response({ id: 'pdf-1', summary: 'Özet metin', language: 'Turkish', createdAtUtc: new Date().toISOString(), expiresAtUtc: new Date().toISOString() }))
      .mockImplementationOnce(() => response([]))
      .mockImplementationOnce(() => Promise.resolve(new Response(new Uint8Array([0x25, 0x50, 0x44, 0x46]), { status: 200, headers: { 'Content-Type': 'application/pdf' } })))
    const area = await screen.findByLabelText('Kaynak metin')
    fireEvent.change(area, { target: { value: 'Toplantı ertelendi.' } })
    fireEvent.click(screen.getByRole('button', { name: 'Özet oluştur' }))
    const downloadButton = await screen.findByRole('button', { name: 'PDF İndir' })
    expect(downloadButton).toBeEnabled()
    fireEvent.click(downloadButton)
    await act(async () => {})
    const pdfCall = fetchMock.mock.calls.find(call => String(call[0]).includes('/api/summaries/pdf-1/pdf'))
    expect(pdfCall).toBeTruthy()
    expect(createObjectURLMock).toHaveBeenCalled()
    expect(revokeObjectURLMock).toHaveBeenCalled()
  })
  it('shows error message when PDF download fails', async () => {
    const fetchMock = renderApp()
    fetchMock.mockImplementationOnce(() => response({ id: 'pdf-2', summary: 'Özet metin', language: 'Turkish', createdAtUtc: new Date().toISOString(), expiresAtUtc: new Date().toISOString() }))
      .mockImplementationOnce(() => response([]))
      .mockImplementationOnce(() => response({ detail: 'Not Found' }, 404))
    const area = await screen.findByLabelText('Kaynak metin')
    fireEvent.change(area, { target: { value: 'Sistem çalışıyor.' } })
    fireEvent.click(screen.getByRole('button', { name: 'Özet oluştur' }))
    const downloadButton = await screen.findByRole('button', { name: 'PDF İndir' })
    fireEvent.click(downloadButton)
    expect(await screen.findByRole('alert')).toHaveTextContent('Bu özetin PDF dosyası bulunamadı veya saklama süresi dolmuş.')
  })
  it('handles text-to-speech play and stop in summary detail view', async () => {
    const speakMock = vi.fn()
    const cancelMock = vi.fn()
    const lastUtterance = { current: null as { text: string; lang: string } | null }
    class MockSpeechSynthesisUtterance {
      text: string
      lang = ''
      onend?: () => void
      onerror?: () => void
      constructor(text: string) {
        this.text = text
        lastUtterance.current = this
      }
    }
    vi.stubGlobal('SpeechSynthesisUtterance', MockSpeechSynthesisUtterance)
    vi.stubGlobal('speechSynthesis', {
      speaking: false,
      speak: speakMock,
      cancel: cancelMock,
    })

    const fetchMock = renderApp()
    fetchMock.mockImplementationOnce(() => response({ id: 'tts-1', summary: 'Özet metin okuma testi', language: 'Turkish', createdAtUtc: new Date().toISOString(), expiresAtUtc: new Date().toISOString() }))
      .mockImplementationOnce(() => response([]))

    const area = await screen.findByLabelText('Kaynak metin')
    fireEvent.change(area, { target: { value: 'Test metni içeriği.' } })
    fireEvent.click(screen.getByRole('button', { name: 'Özet oluştur' }))

    const speakButton = await screen.findByRole('button', { name: /sesli dinle/i })
    expect(speakButton).toBeVisible()
    expect(speakButton).toHaveAttribute('aria-pressed', 'false')

    fireEvent.click(speakButton)
    expect(speakMock).toHaveBeenCalled()
    expect(lastUtterance.current?.lang).toBe('tr-TR')
    expect(lastUtterance.current?.text).toBe('Özet metin okuma testi')

    const stopButton = await screen.findByRole('button', { name: /durdur/i })
    expect(stopButton).toHaveAttribute('aria-pressed', 'true')

    fireEvent.click(stopButton)
    expect(cancelMock).toHaveBeenCalled()
    expect(await screen.findByRole('button', { name: /sesli dinle/i })).toHaveAttribute('aria-pressed', 'false')
  })
})
