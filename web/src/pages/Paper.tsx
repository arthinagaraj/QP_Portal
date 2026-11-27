import { useEffect, useRef, useState } from 'react'
import { useParams } from 'react-router-dom'
import { api, download } from '../api'
import RichEditor from '../components/RichEditor'

const BTLS: Record<string, string> = { K1: 'Remember', K2: 'Understand', K3: 'Apply', K4: 'Analyze', K5: 'Evaluate', K6: 'Create' }
const SUBS = ['i', 'ii', 'iii', 'iv']
const keyOf = (sl: any) => `${sl.partName}-${sl.qNo}-${sl.option}`
const ekOf = (sl: any, sub = '') => sub ? `${keyOf(sl)}|${sub}` : keyOf(sl)
const label = (sl: any) => `Part ${sl.partName} – Q${sl.qNo}${sl.option ? ` (${sl.option})` : ''}`
const entryLabel = (sl: any, sub = '') => sub ? `${label(sl)} (${sub})` : label(sl)
const stem = (w: string) => w.toLowerCase().replace(/[^a-z0-9]/g, '').replace(/(ing|ed|s)$/, '').slice(0, 5)

const STOPW = new Set('what which when where with that this from about their there write explain describe discuss state define using given following into have been does also should would could between these those them than then your marks neat diagram suitable example briefly importance detail mechanism sequence'.split(' '))
const wordSet = (t: string) => new Set(((t || '').toLowerCase().match(/[a-z]{4,}/g) || []).filter(w => !STOPW.has(w)).map(w => w.slice(0, 6)))
const CHIP: Record<string, string> = { red: '✗ Syllabus mismatch / incorrect', yellow: "⚠ Check CO / Bloom's level / unit", blue: '≈ Duplicate / similar', green: '✓ Validated', none: 'not entered' }
const RANK = ['red', 'yellow', 'blue', 'none', 'green']

// Question-type variety: the action verb a question starts with (synonyms grouped)
const TYPE_VERBS = new Set('define list state name identify recall write mention label what why how explain describe discuss summarize summarise illustrate classify compare contrast differentiate distinguish interpret outline apply solve compute calculate demonstrate construct implement trace find simulate analyze analyse examine categorize investigate derive evaluate justify assess criticize recommend judge prove validate design develop formulate propose create compose devise give draw'.split(' '))
const SYN: Record<string, string> = { distinguish: 'differentiate / compare', compare: 'differentiate / compare', contrast: 'differentiate / compare', differentiate: 'differentiate / compare', analyse: 'analyze', summarise: 'summarize', what: 'define / what is', define: 'define / what is' }
const typeOf = (t: string) => { const w = ((t || '').toLowerCase().match(/[a-z]+/g) || []).find(x => TYPE_VERBS.has(x)); return w ? (SYN[w] || w) : '' }
const VARIETY_SHARE = 0.3   // warn when one question type exceeds 30% of a part (min. 2)

export default function Paper() {
  const { id } = useParams()
  const [s, setS] = useState<any>(null)
  const [res, setRes] = useState<Record<string, any>>({})
  const resRef = useRef<Record<string, any>>({})
  resRef.current = res
  const timers = useRef<Record<string, any>>({})
  const [step, setStep] = useState<any>('details')
  const [prof, setProf] = useState<any>({})
  const [instr, setInstr] = useState('')
  const [claim, setClaim] = useState<any>(null)
  const [err, setErr] = useState('')
  const [msg, setMsg] = useState<any>(null)
  const [sug, setSug] = useState<Record<string, any>>({})

  async function load() {
    try {
      const st: any = await api('GET', `/api/setter/assignments/${id}/structure`)
      setS(st)
      const r: any = {}
      const fromQ = (q: any) => ({ html: q.bodyHtml, text: q.plainText, unitNo: q.unitNo, marks: q.marks, verdict: q.syllabusVerdict, clarity: q.clarity, btl: q.btl, btlSource: q.btlSource, notes: q.notes, score: q.matchScore, matched: [] })
      st.slots.forEach((sl: any) => {
        const subs = sl.subs || []
        if (subs.length) {
          r[keyOf(sl)] = { unitNo: subs[0].unitNo ?? sl.unitNo ?? st.units.length, split: subs.map((x: any) => x.subLabel) }
          subs.forEach((q: any) => { r[ekOf(sl, q.subLabel)] = fromQ(q) })
        } else {
          r[keyOf(sl)] = sl.question ? { ...fromQ(sl.question), split: [] } : { unitNo: sl.unitNo ?? st.units.length, split: [] }
        }
      })
      setRes(r)
      const p: any = await api('GET', '/api/setter/profile')
      setProf({ ...p, confirmAccountNumber: p.accountNumber || '' })
      setInstr((await api('GET', '/api/instructions') as any).html)
      if (st.status === 'Submitted') { setStep('review'); setClaim(await api('GET', `/api/setter/assignments/${id}/claim`)) }
    } catch (ex: any) { setErr(ex.message) }
  }
  useEffect(() => { load() }, [id])

  if (err) return <div className="card"><div className="msg err">{err}</div></div>
  if (!s) return <div className="card">Loading…</div>

  /* ---------- entries: a slot is one whole question or 2-4 sub-divisions ---------- */
  const splitOf = (sl: any): string[] => res[keyOf(sl)]?.split || []
  const entriesOf = (sl: any): string[] => splitOf(sl).length ? splitOf(sl) : ['']
  const allEntries = () => s.slots.flatMap((sl: any) => entriesOf(sl).map((sub: string) => ({ sl, sub })))
  const entryOk = (sl: any, sub: string) => { const r = res[ekOf(sl, sub)]; return !!r && r.verdict === 'In' && r.clarity === 'Complete' && !r.saving }
  const subSum = (sl: any) => splitOf(sl).reduce((a: number, sub: string) => a + Number(res[ekOf(sl, sub)]?.marks || 0), 0)
  function repetition() {
    const out: { part: string; type: string; labels: string[]; limit: number }[] = []
    const parts = [...new Set(s.slots.map((x: any) => x.partName))] as string[]
    parts.forEach(p => {
      const slotsP = s.slots.filter((x: any) => x.partName === p)
      const n = new Set(slotsP.map((x: any) => x.qNo)).size
      const limit = Math.max(2, Math.ceil(n * VARIETY_SHARE))
      const byType: Record<string, string[]> = {}
      slotsP.forEach((sl: any) => entriesOf(sl).forEach(sub => {
        const ty = typeOf(res[ekOf(sl, sub)]?.text); if (ty) (byType[ty] = byType[ty] || []).push(entryLabel(sl, sub))
      }))
      Object.entries(byType).forEach(([type, labels]) => { if (labels.length > limit) out.push({ part: p, type, labels, limit }) })
    })
    return out
  }
  const accepted = (sl: any) => {
    const sp = splitOf(sl)
    if (sp.length) return sp.length >= 2 && sp.every(sub => entryOk(sl, sub)) && subSum(sl) === Number(sl.marks)
    return entryOk(sl, '')
  }

  const submitted = s.status === 'Submitted'
  const anySlots = s.slots.filter((x: any) => x.unitNo == null)
  const pages: any[] = submitted ? ['review']
    : ['details', ...s.units.map((u: any) => u.unitNo), ...(anySlots.length ? ['any'] : []), 'review']
  const slotsOf = (p: any) => p === 'any' ? anySlots : s.slots.filter((x: any) => x.unitNo === p)
  const pageDone = (p: any) => p === 'details' ? s.profileComplete : p === 'review' ? submitted : slotsOf(p).every(accepted)
  const idx = pages.indexOf(step)
  const pageName = (p: any) => p === 'details' ? 'Details' : p === 'review' ? 'Review' : p === 'any' ? 'Any unit' : `Unit ${p}`

  /* ---------- saving + AI check ---------- */
  async function save(sl: any, sub = '', btl?: string) {
    const ek = ekOf(sl, sub); const cur = resRef.current[ek] || {}; const slot = resRef.current[keyOf(sl)] || {}
    if (!(cur.text || '').trim() && !/<img/i.test(cur.html || '')) {
      setRes(r => ({ ...r, [ek]: { ...r[ek], verdict: undefined, clarity: undefined, notes: [], saving: false } })); return
    }
    setRes(r => ({ ...r, [ek]: { ...r[ek], saving: true } }))
    try {
      const out: any = await api('PUT', `/api/setter/assignments/${id}/questions`, {
        partName: sl.partName, qNo: sl.qNo, option: sl.option, unitNo: sl.unitNo ?? slot.unitNo,
        bodyHtml: cur.html || '', plainText: cur.text || '', btl: btl ?? (cur.btlManual ? cur.btl : null),
        subLabel: sub, marks: sub ? Number(cur.marks || 0) : null
      })
      setRes(r => ({ ...r, [ek]: { ...r[ek], saving: false, error: '', verdict: out.syllabusVerdict, clarity: out.clarity, btl: out.btl, aiBtl: out.aiBtl, btlSource: out.btlSource, better: out.betterUnit, notes: out.notes, score: out.matchScore, matched: out.matched } }))
    } catch (ex: any) { setRes(r => ({ ...r, [ek]: { ...r[ek], saving: false, error: ex.message } })) }
  }
  function update(sl: any, patch: any, delay = 800, btl?: string, sub = '') {
    const ek = ekOf(sl, sub)
    resRef.current = { ...resRef.current, [ek]: { ...resRef.current[ek], ...patch } }
    setRes(r => ({ ...r, [ek]: { ...r[ek], ...patch } }))
    clearTimeout(timers.current[ek])
    timers.current[ek] = setTimeout(() => save(sl, sub, btl), delay)
  }
  function setPatch(patch: Record<string, any>) {
    resRef.current = { ...resRef.current, ...patch }
    setRes(r => ({ ...r, ...patch }))
  }
  function changeUnit(sl: any, unitNo: number) {
    const k = keyOf(sl)
    setPatch({ [k]: { ...resRef.current[k], unitNo } })
    entriesOf(sl).forEach(sub => save(sl, sub))
  }

  /* ---------- sub-division controls ---------- */
  function setSplit(sl: any, on: boolean) {
    const k = keyOf(sl); const whole = resRef.current[k] || {}
    if (on) {
      const m = Number(sl.marks); const m1 = Math.ceil(m / 2)
      setPatch({
        [k]: { unitNo: whole.unitNo, split: ['i', 'ii'] },
        [ekOf(sl, 'i')]: { html: whole.html, text: whole.text, marks: m1 },
        [ekOf(sl, 'ii')]: { marks: m - m1 },
      })
      if ((whole.text || '').trim()) save(sl, 'i')
    } else {
      if (!confirm('Remove the sub-divisions? Sub-division (i) will be kept as the whole question.')) return
      const first = resRef.current[ekOf(sl, 'i')] || {}
      setPatch({ [k]: { unitNo: whole.unitNo, split: [], html: first.html, text: first.text } })
      if ((first.text || '').trim()) save(sl, '')
      else splitOf(sl).forEach(sub => del(sl, sub))
    }
  }
  function addSub(sl: any) {
    const k = keyOf(sl); const sp = splitOf(sl); const next = SUBS[sp.length]
    setPatch({ [k]: { ...resRef.current[k], split: [...sp, next] }, [ekOf(sl, next)]: { marks: '' } })
  }
  function removeSub(sl: any, sub: string) {
    const k = keyOf(sl)
    setPatch({ [k]: { ...resRef.current[k], split: splitOf(sl).filter(x => x !== sub) }, [ekOf(sl, sub)]: {} })
    del(sl, sub)
  }
  function del(sl: any, sub: string) {
    api('DELETE', `/api/setter/assignments/${id}/questions?partName=${encodeURIComponent(sl.partName)}&qNo=${sl.qNo}&option=${sl.option}&subLabel=${sub}`).catch(() => { })
  }

  /* ---------- 💡 question suggestions for the unit (LLM or syllabus templates) ---------- */
  const esc = (x: string) => x.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
  function tooClose(text: string) {
    const a = wordSet(text)
    return allEntries().some((o: any) => {
      const b = wordSet(res[ekOf(o.sl, o.sub)]?.text); if (b.size < 3 || a.size < 3) return false
      let i = 0; a.forEach(w => { if (b.has(w)) i++ })
      return i / Math.min(a.size, b.size) >= 0.75
    })
  }
  async function suggest(sl: any, sub = '') {
    const ek = ekOf(sl, sub)
    const unitNo = sl.unitNo ?? res[keyOf(sl)]?.unitNo ?? 1
    const marks = sub ? Number(res[ek]?.marks || sl.marks) : Number(sl.marks)
    setSug(x => ({ ...x, [ek]: { loading: true } }))
    try {
      const r: any = await api('POST', `/api/setter/assignments/${id}/suggest`, { unitNo, marks, count: 6 })
      setSug(x => ({ ...x, [ek]: { ...r, items: r.items.filter((q: any) => !tooClose(q.text)), unitNo } }))
    } catch (ex: any) { setSug(x => ({ ...x, [ek]: { err: ex.message } })) }
  }
  function useSuggestion(sl: any, sub: string, q: any) {
    const ek = ekOf(sl, sub); const cur = res[ek] || {}
    if ((cur.text || '').trim() && !confirm('Replace the question you have typed with this suggestion?')) return
    update(sl, { html: esc(q.text), text: q.text, v: (cur.v || 0) + 1 }, 0, undefined, sub)
    setSug(x => ({ ...x, [ek]: undefined }))
  }
  function suggestBox(sl: any, sub = '') {
    const ek = ekOf(sl, sub); const g = sug[ek]
    return (
      <div className="sug">
        <button className="g sm" disabled={g?.loading} onClick={() => suggest(sl, sub)}>
          {g?.loading ? 'Thinking…' : g?.items ? '💡 More suggestions' : '💡 Suggest questions'}</button>
        {g?.items && <button className="g sm" onClick={() => setSug(x => ({ ...x, [ek]: undefined }))}>Hide</button>}
        {g?.err && <span className="bad" style={{ fontSize: 12 }}> {g.err}</span>}
        {g?.items && (
          <div className="sug-list">
            <div className="mu" style={{ fontSize: 12 }}>Suggestions from Unit {g.unitNo} syllabus · {g.source}{g.note ? ' · ' + g.note : ''} – edit after inserting; the AI check runs again.</div>
            {g.items.length === 0 && <div className="mu">No new suggestions (all were close to questions already in the paper). Try again.</div>}
            {g.items.map((q: any, i: number) => (
              <div key={i} className="sug-item">
                <span className="chip">{q.btl}</span> <span>{q.text}</span>
                <button className="sm" onClick={() => useSuggestion(sl, sub, q)}>Use</button>
              </div>))}
          </div>)}
      </div>)
  }

  /* ---------- colour-coded AI validation: red / yellow / blue / green ---------- */
  function similarTo(sl: any, sub: string): string[] {
    const a = wordSet(res[ekOf(sl, sub)]?.text)
    if (a.size < 3) return []
    return allEntries().filter((o: any) => ekOf(o.sl, o.sub) !== ekOf(sl, sub)).filter((o: any) => {
      const b = wordSet(res[ekOf(o.sl, o.sub)]?.text)
      if (b.size < 3) return false
      let i = 0; a.forEach(w => { if (b.has(w)) i++ })
      const jaccard = i / (a.size + b.size - i)          // overall word overlap
      const contained = i / Math.min(a.size, b.size)     // shorter question's keywords found inside the other
      return jaccard >= 0.5 || (contained >= 0.75 && i >= 3)
    }).map((o: any) => entryLabel(o.sl, o.sub))
  }

  function qStatus(sl: any, sub = ''): { c: string; msgs: { c: string; t: string }[] } {
    const r = res[ekOf(sl, sub)] || {}
    if (!r.verdict) return { c: 'none', msgs: [] }
    const msgs: { c: string; t: string }[] = []
    if (r.verdict !== 'In' || r.clarity !== 'Complete')
      (r.notes || []).filter((n: string) => !n.includes('fits Unit')).forEach((t: string) => msgs.push({ c: 'red', t }))
    const btlMismatch = r.aiBtl ? r.btl !== r.aiBtl : r.btlSource === 'Setter'
    if (btlMismatch) msgs.push({ c: 'yellow', t: `Bloom's level ${r.btl} differs from the AI suggestion${r.aiBtl ? ' (' + r.aiBtl + ' · ' + BTLS[r.aiBtl] + ')' : ''} – please confirm` })
    const betterNote = (r.notes || []).find((n: string) => n.includes('fits Unit'))
    if (r.better) msgs.push({ c: 'yellow', t: `Unit / CO mapping: this question fits Unit ${r.better} (CO${r.better}) better than this unit` })
    else if (betterNote) msgs.push({ c: 'yellow', t: 'Unit / CO mapping: ' + betterNote })
    const rp = repetition().find(w => w.labels.includes(entryLabel(sl, sub)))
    if (rp) msgs.push({ c: 'yellow', t: `Question type "${rp.type}" is used ${rp.labels.length} times in Part ${rp.part} (limit ${rp.limit}) – vary the question types` })
    const sim = similarTo(sl, sub)
    if (sim.length) msgs.push({ c: 'blue', t: `Duplicate / similar to ${sim.join(', ')}` })
    const c = msgs.some(m => m.c === 'red') ? 'red' : msgs.some(m => m.c === 'yellow') ? 'yellow' : msgs.some(m => m.c === 'blue') ? 'blue' : 'green'
    if (c === 'green') msgs.push({ c: 'green', t: "Validated: within syllabus, complete, CO and Bloom's level consistent" })
    return { c, msgs }
  }

  function slotColor(sl: any) {
    const cols = entriesOf(sl).map(sub => qStatus(sl, sub).c)
    if (splitOf(sl).length && cols.every(c => c !== 'none') && subSum(sl) !== Number(sl.marks)) cols.push('red')
    return RANK.find(c => cols.includes(c)) || 'none'
  }

  function badge(sl: any, sub = '') {
    const r = res[ekOf(sl, sub)] || {}
    if (r.saving) return <span className="mu">AI agent checking…</span>
    if (r.error) return <span className="chip red">{r.error}</span>
    const st = qStatus(sl, sub)
    if (st.c === 'none') return <span className="chip none">not entered</span>
    return (
      <>
        <span className={`chip ${st.c}`}>{CHIP[st.c]}</span>
        <span className="mu">Syllabus match {Math.round((r.score || 0) * 100)}% · BTL </span>
        <select value={r.btl} style={{ width: 'auto', padding: '1px 4px' }}
                onChange={e => update(sl, { btl: e.target.value, btlManual: true }, 0, e.target.value, sub)}>
          {Object.entries(BTLS).map(([k, v]) => <option key={k} value={k}>{k} · {v}</option>)}
        </select>
        {st.msgs.map((m, i) => <div key={i} className={`st-msg ${m.c}`}>{m.t}</div>)}
      </>
    )
  }

  /* ---------- pieces ---------- */
  const prog = (
    <div className="prog noprint">
      {pages.map((p, i) => (
        <span key={String(p)} className={i === idx ? 'a' : pageDone(p) ? 'd' : ''}
              onClick={() => { if (i < idx || pageDone(pages[i - 1] ?? 'details')) setStep(p) }}>{pageName(p)}</span>))}
    </div>
  )
  const header = (
    <div className="card noprint"><b>{s.course.code} – {s.course.title}</b> <span className="mu">· {s.examSession} · deadline {s.dueDate} · {s.template.name} ({s.template.maxMarks} marks, {s.template.duration})</span></div>
  )
  const nav = (
    <p className="noprint" style={{ marginTop: 12 }}>
      <button className="g" disabled={idx <= 0} onClick={() => setStep(pages[idx - 1])}>← Previous</button>{' '}
      {step !== 'review' && <button disabled={!pageDone(step)} onClick={() => setStep(pages[idx + 1])}>
        {pages[idx + 1] === 'review' ? 'Finish → Review' : 'Next →'}</button>}{' '}
      {step !== 'review' && !pageDone(step) && <span className="mu">
        {step === 'details' ? 'Save your details to continue' : 'Every question on this page must be validated (and sub-division marks must add up) to continue'}</span>}
    </p>
  )

  const reps = repetition()
  const repBanner = reps.length > 0 && (
    <div className="msg" style={{ background: '#fffbea', borderColor: '#e0a800', color: '#8a5a00' }}>
      <b>⚠ Question variety</b>
      {reps.map(w => <div key={w.part + w.type}>Part {w.part}: {w.labels.length} "{w.type}" questions ({w.labels.join(', ')}). Keep it to {w.limit} or fewer and use other question types.</div>)}
    </div>)

  function highlight(topics: string, matched: string[]) {
    const keys = new Set((matched || []).map(stem))
    return topics.split(/(\s+|,)/).map((w, i) => keys.size && keys.has(stem(w)) && w.trim().length > 2 ? <mark key={i}>{w}</mark> : w)
  }

  function slotBlock(sl: any) {
    const k = keyOf(sl); const r = res[k] || {}; const sp = splitOf(sl)
    return (
      <div key={k}>
        {sl.option === 'b' && <div className="or-sep">(OR)</div>}
        <div className={`st ${slotColor(sl)}`}>
          <b>{label(sl)}</b> <span className="mu">({sl.marks} marks · {sl.partTitle})</span>
          {step === 'any' && (
            <select value={r.unitNo} style={{ width: 'auto', marginLeft: 8 }} onChange={e => changeUnit(sl, Number(e.target.value))}>
              {s.units.map((u: any) => <option key={u.unitNo} value={u.unitNo}>From Unit {u.unitNo}</option>)}
            </select>)}
          {sl.maxSub >= 2 && (
            <label style={{ display: 'inline-flex', alignItems: 'center', gap: 4, marginLeft: 12, fontSize: 12 }}>
              <input type="checkbox" style={{ width: 'auto' }} checked={sp.length > 0} onChange={e => setSplit(sl, e.target.checked)} />
              Split into sub-divisions (i), (ii)…
            </label>)}

          {sp.length === 0 ? (
            <>
              <div style={{ margin: '4px 0' }}>{badge(sl)}</div>
              {suggestBox(sl)}
              <RichEditor key={`${k}-${r.v || 0}`} id={`${id}-${k}-${r.v || 0}`} html={r.html || ''} onChange={(html, text) => update(sl, { html, text })} />
            </>
          ) : (
            <>
              {sp.map(sub => {
                const e = res[ekOf(sl, sub)] || {}
                return (
                  <div key={sub} style={{ marginTop: 10, paddingLeft: 10, borderLeft: '2px dashed var(--bd)' }}>
                    <div className="flex">
                      <b>({sub})</b>
                      <span className="mu">Marks</span>
                      <input type="number" min={1} max={Number(sl.marks) - 1} value={e.marks ?? ''} style={{ width: 70 }}
                             onChange={ev => update(sl, { marks: ev.target.value }, 600, undefined, sub)} />
                      {sub === sp[sp.length - 1] && sp.length > 2 && <button className="g sm" onClick={() => removeSub(sl, sub)}>Remove ({sub})</button>}
                    </div>
                    <div style={{ margin: '4px 0' }}>{badge(sl, sub)}</div>
                    {suggestBox(sl, sub)}
                    <RichEditor key={`${ekOf(sl, sub)}-${e.v || 0}`} id={`${id}-${ekOf(sl, sub)}-${e.v || 0}`} html={e.html || ''} onChange={(html, text) => update(sl, { html, text }, 800, undefined, sub)} />
                  </div>)
              })}
              <div className="flex" style={{ marginTop: 6 }}>
                <span className={subSum(sl) === Number(sl.marks) ? 'ok' : 'bad'} style={{ fontSize: 12, fontWeight: 600 }}>
                  Sub-division total: {subSum(sl)} / {sl.marks} marks</span>
                {sp.length < sl.maxSub && <button className="g sm" onClick={() => addSub(sl)}>+ Add ({SUBS[sp.length]})</button>}
              </div>
            </>
          )}
        </div>
      </div>
    )
  }

  /* ---------- step: details ---------- */
  if (step === 'details') {
    const f = (k: string, l: string, extra: any = {}) =>
      <label>{l}<input value={prof[k] ?? ''} onChange={e => setProf({ ...prof, [k]: e.target.value })} {...extra} /></label>
    async function saveProfile() {
      try {
        await api('PUT', '/api/setter/profile', prof)
        setS({ ...s, profileComplete: true }); setMsg(null); setStep(pages[1])
      } catch (ex: any) { setMsg({ ok: false, text: ex.message }) }
    }
    return (
      <>
        {prog}{header}
        {instr && <div className="card"><h2>Instructions to the QP setter</h2><div dangerouslySetInnerHTML={{ __html: instr }} /></div>}
        <div className="card"><h2>Personal &amp; bank details</h2>
          <div className="cols">{f('name', 'Full name')}{f('mobile', 'Mobile', { maxLength: 10 })}{f('designation', 'Designation')}{f('department', 'Department')}{f('institution', 'Institution')}{f('pan', 'PAN', { maxLength: 10, style: { textTransform: 'uppercase' } })}</div>
          <div style={{ marginTop: 8 }}>{f('address', 'Address (with PIN code)')}</div>
          <h3>Bank account</h3>
          <div className="cols">{f('accountHolder', 'Account holder name')}{f('accountNumber', 'Account number', { inputMode: 'numeric' })}{f('confirmAccountNumber', 'Re-enter account number', { inputMode: 'numeric' })}{f('bankName', 'Bank name')}{f('branch', 'Branch')}{f('ifsc', 'IFSC', { maxLength: 11, style: { textTransform: 'uppercase' } })}</div>
          {msg && <div className={`msg ${msg.ok ? 'ok' : 'err'}`}>{msg.text}</div>}
          <p><button onClick={saveProfile}>Save &amp; continue</button></p>
        </div>
      </>
    )
  }

  /* ---------- step: unit page or any-unit page ---------- */
  if (step !== 'review') {
    const list = slotsOf(step)
    const unit = step === 'any' ? null : s.units.find((u: any) => u.unitNo === step)
    const matchedAll = list.flatMap((sl: any) => entriesOf(sl).flatMap(sub => res[ekOf(sl, sub)]?.matched || []))
    return (
      <>
        {prog}{header}
        <div className="split">
          <div className="card">
            <h2>{unit ? `Unit ${unit.unitNo} (CO${unit.unitNo}) – questions` : 'Questions from any unit'}</h2>
            <div className="legend"><span className="chip red">Red · syllabus mismatch / incorrect</span><span className="chip yellow">Yellow · CO, Bloom's level or unit</span><span className="chip blue">Blue · duplicate / similar</span><span className="chip green">Green · validated</span></div>
            {repBanner}
            {list.map(slotBlock)}
            {nav}
          </div>
          <div className="card syl">
            {unit ? (
              <>
                <h2>Syllabus – Unit {unit.unitNo}: {unit.title}</h2>
                <p><b>CO{unit.unitNo}:</b> {unit.co}</p>
                <p>{highlight(unit.topics, matchedAll)}</p>
              </>
            ) : s.units.map((u: any) => (
              <div key={u.unitNo} style={{ marginBottom: 10 }}><b>Unit {u.unitNo}: {u.title}</b><br /><span className="mu">CO{u.unitNo}: {u.co}</span><br />{u.topics}</div>))}
            <p className="mu">The AI agent checks each question (and each sub-division) against this unit's syllabus topics: more than 50% deviation is rejected. It also checks the question is complete and fills the BTL level automatically; you can change it. Sub-division marks must add up to the question's marks.</p>
          </div>
        </div>
      </>
    )
  }

  /* ---------- step: review ---------- */
  const total = s.slots.length
  const done = s.slots.filter(accepted).length
  const entries = allEntries()
  const spread = Object.keys(BTLS).map(k => `${k}: ${entries.filter((e: any) => res[ekOf(e.sl, e.sub)]?.btl === k).length}`).join(' · ')
  async function submit() {
    if (!confirm('Submit the question paper? It will be locked and cannot be edited afterwards.')) return
    try {
      await api('POST', `/api/setter/assignments/${id}/submit`)
      setMsg({ ok: true, text: 'Question paper submitted to the Controller of Examinations.' })
      await load()
    } catch (ex: any) { setMsg({ ok: false, text: ex.message }) }
  }
  const dlPdf = () => download(`/api/setter/assignments/${id}/claim.pdf`, `Claim_${s.course.code}.pdf`).catch(e => setMsg({ ok: false, text: e.message }))

  return (
    <>
      {prog}{header}
      <div className="card noprint"><h2>Final check · {s.course.code} – {s.course.title}</h2>
        <div className="cols">
          <div><span className={`big ${done === total ? 'ok' : 'bad'}`}>{done}/{total}</span><br />questions accepted</div>
          <div><b>BTL spread</b><br />{spread}</div>
          <div><b>AI validation</b><br />{['red', 'yellow', 'blue', 'green'].map(c => <span key={c} className={`chip ${c}`}>{entries.filter((e: any) => qStatus(e.sl, e.sub).c === c).length}</span>)}</div>
          <div><b>Status</b><br /><span className={`b ${submitted ? 'ok' : 'warn'}`}>{s.status}</span></div>
        </div>
        {repBanner}
        <p className="mu">For confidentiality, this page shows only counts. The question paper cannot be printed or downloaded by the setter.</p>
        {!submitted && <p><button className="okb" disabled={done !== total} onClick={submit}>Submit question paper</button></p>}
        {msg && <div className={`msg ${msg.ok ? 'ok' : 'err'}`}>{msg.text}</div>}
      </div>
      {claim && (
        <div className="card" id="claim"><h2>Remuneration claim</h2>
          <div className="cols">
            <div><b>{claim.setter.name}</b><br />{claim.setter.designation}, {claim.setter.department}<br />{claim.setter.institution}<br />PAN {claim.bank.pan}<br />{claim.setter.mobile}</div>
            <div>{claim.bank.accountHolder}<br />{claim.bank.bankName}, {claim.bank.branch}<br />A/c {claim.bank.accountNumber}<br />IFSC {claim.bank.ifsc}</div>
            <div>Course: {claim.course}<br />Session: {claim.examSession}<br />Setter type: {claim.setter.setterType}</div>
          </div>
          <table style={{ marginTop: 10, maxWidth: 520 }}><tbody>
            <tr><td>Question paper setting fee</td><td style={{ textAlign: 'right' }}>₹{claim.claim.qpFee}</td></tr>
            <tr><td>Travel / other allowance</td><td style={{ textAlign: 'right' }}>₹{claim.claim.travelAllowance}</td></tr>
            <tr><td><b>Gross</b></td><td style={{ textAlign: 'right' }}><b>₹{claim.claim.gross}</b></td></tr>
            <tr><td>Less: TDS @ {claim.claim.tdsPercent}%</td><td style={{ textAlign: 'right' }}>₹{claim.claim.tds}</td></tr>
            <tr><td><b>Net payable</b></td><td style={{ textAlign: 'right' }}><b>₹{claim.claim.net}</b></td></tr>
          </tbody></table>
          <p className="mu">Claim form only – no question content.</p>
          <p className="noprint"><button className="or" onClick={dlPdf}>⬇ Download claim PDF</button>{' '}
            <button className="g" onClick={() => window.print()}>🖨 Print claim</button></p>
        </div>
      )}
      {nav}
    </>
  )
}
