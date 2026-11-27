export const COLLEGE = 'Karpagam College of Engineering, Coimbatore'

export type Session = { token: string; name: string; roles: string[]; permissions: string[] }
const KEY = 'qp_session'

export const session = {
  get(): Session | null {
    try { return JSON.parse(localStorage.getItem(KEY) || 'null') } catch { return null }
  },
  set(s: Session) { localStorage.setItem(KEY, JSON.stringify(s)) },
  clear() { localStorage.removeItem(KEY) },
  can(p: string) { return !!session.get()?.permissions?.includes(p) },
}

async function errorText(r: Response) {
  const t = await r.text()
  try {
    const j = JSON.parse(t)
    if (Array.isArray(j.errors)) return j.errors.join('\n')
    if (Array.isArray(j.problems)) return j.problems.join('\n')
    if (j.errors && typeof j.errors === 'object') return (Object.values(j.errors) as any[]).flat().join('\n')
    if (j.title) return j.title
    return t
  } catch {
    return t || `${r.status} ${r.statusText}`
  }
}

export async function api<T = any>(method: string, url: string, body?: any): Promise<T> {
  const s = session.get()
  const headers: Record<string, string> = {}
  if (s) headers.Authorization = 'Bearer ' + s.token
  let payload: any
  if (body instanceof FormData) payload = body
  else if (body !== undefined) { headers['Content-Type'] = 'application/json'; payload = JSON.stringify(body) }
  const r = await fetch(url, { method, headers, body: payload })
  if (!r.ok) {
    if (r.status === 403) throw new Error('You do not have permission for this action.')
    throw new Error(await errorText(r))
  }
  const ct = r.headers.get('content-type') || ''
  return (ct.includes('json') ? await r.json() : await r.text()) as T
}

export async function download(url: string, filename: string) {
  const s = session.get()
  const r = await fetch(url, { headers: s ? { Authorization: 'Bearer ' + s.token } : {} })
  if (!r.ok) throw new Error(await errorText(r))
  const blob = await r.blob()
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = filename
  a.click()
  setTimeout(() => URL.revokeObjectURL(a.href), 2000)
}