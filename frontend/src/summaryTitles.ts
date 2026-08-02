export type SummaryLanguage = 'Turkish' | 'English'

const trStop = new Set(['ve', 'veya', 'ile', 'için', 'bir', 'bu', 'şu', 'olan', 'olarak', 'hakkında'])
const enStop = new Set(['and', 'or', 'with', 'for', 'the', 'a', 'an', 'this', 'that', 'about', 'of'])

export function deriveSummaryTitle(source: string | undefined, summary: string, language: SummaryLanguage) {
  const fallback = language === 'Turkish' ? 'Yeni Özet' : 'New Summary'
  const candidate = `${source ?? ''} ${summary}`.replace(/<[^>]*>/g, ' ').replace(/[\r\n]+/g, ' ').trim()
  if (!candidate) return fallback
  const stop = language === 'Turkish' ? trStop : enStop
  const words = candidate.match(/[\p{L}\p{N}]+/gu)?.filter(word => word.length > 2 && !stop.has(word.toLocaleLowerCase(language === 'Turkish' ? 'tr-TR' : 'en-US'))) ?? []
  if (words.length < 2) return fallback
  const topic = [...new Set(words.map(word => titleCase(word, language)))].slice(0, 2).join(' ')
  if (!topic || topic.length > 52) return fallback
  return `${topic} ${language === 'Turkish' ? 'Özeti' : 'Summary'}`
}

function titleCase(value: string, language: SummaryLanguage) {
  const locale = language === 'Turkish' ? 'tr-TR' : 'en-US'
  return value.slice(0, 1).toLocaleUpperCase(locale) + value.slice(1).toLocaleLowerCase(locale)
}
