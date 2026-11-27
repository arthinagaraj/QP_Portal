import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, download, session } from '../api'
import RichEditor from '../components/RichEditor'

const TABS = [
  { k: 'up', l: '1 · Upload data', p: 'setter.upload' },
  { k: 'reg', l: '2 · Registrations', p: 'faculty.approve' },
  { k: 'fw', l: '3 · QP framework', p: 'template.manage' },
  { k: 'ins', l: '4 · Instructions', p: 'instructions.manage' },
  { k: 'map', l: '5 · Suggest & map', p: 'setter.assign' },
  { k: 'an', l: '6 · Analysis', p: 'report.view' },
  { k: 'scr', l: '7 · Scrutiny', p: 'scrutiny.review' },
]

export default function Admin() {
  const visible = TABS.filter(t => session.can(t.p))
  const [tab, setTab] = useState(visible[0]?.k)
  return (
    <>
      <div className="tabs noprint">
        {visible.map(t => <button key={t.k} className={tab === t.k ? 'on' : 'g'} onClick={() => setTab(t.k)}>{t.l}</button>)}
      </div>
      {tab === 'up' && <UploadTab />}
      {tab === 'reg' && <RegistrationsTab />}
      {tab === 'fw' && <FrameworkTab />}
      {tab === 'ins' && <InstructionsTab />}
      {tab === 'map' && <MapTab />}
      {tab === 'an' && <AnalysisTab />}
      {tab === 'scr' && <ScrutinyTab />}
    </>
  )
}

function Sample({ h, r }: { h: string[]; r: string[] }) {
  return <div className="sc"><p className="mu" style={{ margin: '4px 0' }}>Excel format – column headings with one example row (not your data):</p><table><thead><tr>{h.map(x => <th key={x}>{x}</th>)}</tr></thead>
    <tbody><tr>{r.map((x, i) => <td key={i}>{x}</td>)}</tr></tbody></table></div>
}

/* ---------------- 1. Upload ---------------- */
function UploadTab() {
  const [msg, setMsg] = useState<Record<string, any>>({})
  const [courses, setCourses] = useState<any[]>([])
  const [setters, setSetters] = useState<any[]>([])
  const [regs, setRegs] = useState<any[]>([])
  const [newReg, setNewReg] = useState('')
  const refresh = async () => {
    setRegs(await api('GET', '/api/regulations'))
    setCourses(await api('GET', '/api/courses'))
    if (session.can('setter.assign')) setSetters(await api('GET', '/api/setters'))
  }
  useEffect(() => { refresh() }, [])

  async function up(e: any, kind: 'setters' | 'courses') {
    const f = e.target.files?.[0]; if (!f) return
    const fd = new FormData(); fd.append('file', f)
    try {
      const r: any = await api('POST', `/api/uploads/${kind}`, fd)
      const text = kind === 'setters' ? `${r.added} added, ${r.updated} updated`
        : `${r.coursesAdded} courses added, ${r.coursesUpdated} updated, ${r.units} units`
      setMsg(m => ({ ...m, [kind]: { ok: r.errors.length === 0, text: text + (r.errors.length ? '\n' + r.errors.join('\n') : '') } }))
      refresh()
    } catch (ex: any) { setMsg(m => ({ ...m, [kind]: { ok: false, text: ex.message } })) }
    e.target.value = ''
  }
  const say = (k: string, ok: boolean, text: string) => setMsg(m => ({ ...m, [k]: { ok, text } }))
  async function addReg() {
    try { const r: any = await api('POST', '/api/regulations', { code: newReg }); setNewReg(''); say('regs', true, `Regulation ${r.code} added. Now create its QP framework (tab 3) and upload its courses.`); refresh() }
    catch (ex: any) { say('regs', false, ex.message) }
  }
  async function delReg(r: any) {
    if (!confirm(`Remove regulation ${r.code}?`)) return
    try { await api('DELETE', `/api/regulations/${r.id}`); say('regs', true, `${r.code} removed.`); refresh() } catch (ex: any) { say('regs', false, ex.message) }
  }
  async function delCourse(c: any) {
    if (!confirm(`Remove ${c.code} – ${c.title} and its unit syllabus?`)) return
    try { await api('DELETE', `/api/courses/${c.id}`); say('courses', true, `${c.code} removed.`); refresh() } catch (ex: any) { say('courses', false, ex.message) }
  }
  const Msg = ({ k }: { k: string }) => msg[k] ? <div className={`msg ${msg[k].ok ? 'ok' : 'err'}`}>{msg[k].text}</div> : null

  return (
    <>
      <div className="card"><h2>Upload data</h2>
        <p className="mu">Use the sample format exactly (same column names). Each file is validated row by row; rows with errors are listed and skipped.</p></div>
      <div className="card"><h3>Regulations</h3>
        <p className="mu">Add a regulation before uploading its courses or creating its QP framework. A regulation can be removed only while it has no courses and no framework.</p>
        <div className="flex">
          <input placeholder="e.g. R2025" value={newReg} onChange={e => setNewReg(e.target.value)} onKeyDown={e => e.key === 'Enter' && addReg()} style={{ width: 160 }} />
          <button onClick={addReg} disabled={!newReg.trim()}>+ Add regulation</button>
        </div>
        <Msg k="regs" />
        {regs.length > 0 && <div className="sc"><table><thead><tr><th>Regulation</th><th>Courses</th><th></th></tr></thead>
          <tbody>{regs.map(r => {
            const n = courses.filter(c => c.regulation === r.code).length
            return <tr key={r.id}><td><b>{r.code}</b></td><td>{n}</td>
              <td>{n === 0 && <button className="g sm" onClick={() => delReg(r)}>Remove</button>}</td></tr>
          })}</tbody></table></div>}
      </div>
      <div className="card"><h3>A. QP setter database</h3>
        <Sample h={['Name', 'Email', 'Mobile', 'DateOfBirth', 'Designation', 'Department', 'Institution', 'SetterType', 'ExperienceYears', 'Qualification', 'AreaOfInterest']}
                r={['Dr. R. Anitha', 'anitha.r@demo.edu', '9876500001', '1978-03-12', 'Professor', 'CSE', 'Karpagam College of Engineering', 'Internal', '22', 'Ph.D.', 'algorithms, graph theory']} />
        <p><input type="file" accept=".xlsx" onChange={e => up(e, 'setters')} style={{ width: 'auto' }} /> <span className="mu">{setters.length} setters in database</span></p>
        <Msg k="setters" />
        {setters.length > 0 && <details><summary>Show uploaded setters ({setters.length})</summary><div className="sc"><table>
          <thead><tr><th>Name</th><th>Email</th><th>Designation</th><th>Department</th><th>Type</th><th>Exp.</th><th>Area of interest</th></tr></thead>
          <tbody>{setters.map((s: any) => <tr key={s.id}><td>{s.name}</td><td>{s.email}</td><td>{s.designation}</td><td>{s.department}</td>
            <td>{s.setterType}</td><td>{s.experienceYears}</td><td>{s.areaOfInterest}</td></tr>)}</tbody></table></div></details>}
      </div>
      <div className="card"><h3>B. Course code, title, regulation with unit syllabus (one row per unit)</h3>
        <Sample h={['Regulation', 'CourseCode', 'CourseTitle', 'UnitNo', 'UnitTitle', 'CO', 'Topics']}
                r={['R2021', 'CS3401', 'Algorithms', '1', 'Introduction', 'Analyze time and space complexity', 'asymptotic notations, recurrence relations, ...']} />
        <p><input type="file" accept=".xlsx" onChange={e => up(e, 'courses')} style={{ width: 'auto' }} /> <span className="mu">{courses.length} courses loaded</span></p>
        <Msg k="courses" />
        {courses.length > 0 && <details open><summary>Show uploaded courses ({courses.length})</summary><div className="sc"><table><thead><tr><th>Code</th><th>Title</th><th>Regulation</th><th>Units</th><th></th></tr></thead>
          <tbody>{courses.map(c => <tr key={c.id}><td>{c.code}</td><td>{c.title}</td><td>{c.regulation}</td><td>{c.units}</td>
            <td><button className="g sm" onClick={() => delCourse(c)}>Remove</button></td></tr>)}</tbody></table></div></details>}
      </div>
    </>
  )
}

/* ---------------- 2. Registrations ---------------- */
function RegistrationsTab() {
  const [status, setStatus] = useState('Pending')
  const [rows, setRows] = useState<any[]>([])
  const [msg, setMsg] = useState('')
  const load = async () => setRows(await api('GET', `/api/registrations?status=${status}`))
  useEffect(() => { load() }, [status])

  async function decide(r: any, ok: boolean) {
    try {
      if (ok) await api('POST', `/api/registrations/${r.id}/approve`)
      else {
        const reason = prompt(`Reason for rejecting ${r.name}`); if (!reason) return
        await api('POST', `/api/registrations/${r.id}/reject`, { reason })
      }
      setMsg(`${r.name}: ${ok ? 'approved - now in the setter database' : 'rejected'}`); load()
    } catch (ex: any) { setMsg(ex.message) }
  }

  return (
    <div className="card"><h2>Self-registered faculty</h2>
      <div className="flex">
        <select value={status} onChange={e => setStatus(e.target.value)} style={{ width: 'auto' }}>
          <option>Pending</option><option>Approved</option><option>Rejected</option>
        </select>
        <span className="mu">{rows.length} found</span>
      </div>
      {msg && <div className="msg ok">{msg}</div>}
      <div className="sc" style={{ marginTop: 8 }}><table>
        <thead><tr><th>Name</th><th>Designation / Dept</th><th>Institution</th><th>Type</th><th>Exp.</th><th>Area of interest</th><th>Contact</th>{status === 'Pending' && <th></th>}</tr></thead>
        <tbody>{rows.map(r => (
          <tr key={r.id}>
            <td><b>{r.name}</b><br /><span className="mu">{r.qualification}</span></td>
            <td>{r.designation}<br /><span className="mu">{r.department}</span></td>
            <td>{r.institution}</td><td>{r.setterType}</td><td>{r.experienceYears}</td><td>{r.areaOfInterest}</td>
            <td>{r.email}<br />{r.mobile}</td>
            {status === 'Pending' && <td><button className="okb" onClick={() => decide(r, true)}>Approve</button>{' '}
              <button className="badb" onClick={() => decide(r, false)}>Reject</button></td>}
          </tr>))}
          {rows.length === 0 && <tr><td colSpan={8} className="mu">None.</td></tr>}
        </tbody></table></div>
    </div>
  )
}

/* ---------------- 3. QP framework ---------------- */
const DEF_PARTS = [
  { partName: 'A', title: 'Short answer', distribution: 'PerUnit', questionCount: 2, marksEach: 2, isEitherOr: false, maxSubdivisions: 0 },
  { partName: 'B', title: 'Descriptive', distribution: 'PerUnit', questionCount: 1, marksEach: 13, isEitherOr: true, maxSubdivisions: 2 },
  { partName: 'C', title: 'Higher order / case', distribution: 'AnyUnit', questionCount: 1, marksEach: 15, isEitherOr: true, maxSubdivisions: 2 },
]

function FrameworkTab() {
  const [regs, setRegs] = useState<any[]>([])
  const [list, setList] = useState<any[]>([])
  const [msg, setMsg] = useState<any>(null)
  const blank = (regId: any) => ({ id: null, regulationId: regId, examType: 'End Semester', name: '', duration: '3 Hours', maxMarks: 100, instructions: 'Answer ALL questions', parts: DEF_PARTS })
  const [f, setF] = useState<any>(blank(''))

  const load = async () => {
    const r = await api('GET', '/api/regulations'); setRegs(r)
    setList(await api('GET', '/api/templates'))
    setF((x: any) => x.regulationId ? x : { ...x, regulationId: r[0]?.id ?? '' })
  }
  useEffect(() => { load() }, [])

  const partTotal = (p: any) => (p.distribution === 'PerUnit' ? Number(p.questionCount) * 5 : Number(p.questionCount)) * Number(p.marksEach)
  const total = f.parts.reduce((a: number, p: any) => a + partTotal(p), 0)
  const setPart = (i: number, k: string, v: any) => setF({ ...f, parts: f.parts.map((p: any, j: number) => j === i ? { ...p, [k]: v } : p) })
  const addPart = () => setF({ ...f, parts: [...f.parts, { partName: String.fromCharCode(65 + f.parts.length), title: '', distribution: 'PerUnit', questionCount: 1, marksEach: 10, isEitherOr: false, maxSubdivisions: 0 }] })
  const removePart = (i: number) => setF({ ...f, parts: f.parts.filter((_: any, j: number) => j !== i) })

  async function save() {
    const reg = regs.find(r => r.id == f.regulationId)
    const body = {
      regulationId: Number(f.regulationId), examType: f.examType, name: f.name || `${reg?.code} ${f.examType}`,
      duration: f.duration, maxMarks: Number(f.maxMarks), instructions: f.instructions,
      parts: f.parts.map((p: any) => ({ ...p, questionCount: Number(p.questionCount), marksEach: Number(p.marksEach), maxSubdivisions: Number(p.maxSubdivisions || 0) }))
    }
    try {
      const t: any = f.id ? await api('PUT', `/api/templates/${f.id}`, body) : await api('POST', '/api/templates', body)
      setF({ ...f, id: t.id }); setMsg({ ok: true, text: `Saved as draft (version ${t.version}). Total ${t.total} / ${t.maxMarks}.` }); load()
    } catch (ex: any) { setMsg({ ok: false, text: ex.message }) }
  }
  async function publish(id: number) {
    try { await api('POST', `/api/templates/${id}/publish`); setMsg({ ok: true, text: 'Published. New setter assignments for this regulation will use it.' }); load() }
    catch (ex: any) { setMsg({ ok: false, text: ex.message }) }
  }
  const edit = (t: any) => setF({
    id: t.id, regulationId: t.regulationId, examType: t.examType, name: t.name, duration: t.duration, maxMarks: t.maxMarks,
    instructions: t.instructions || '', parts: t.parts.map((p: any) => ({ partName: p.partName, title: p.title, distribution: p.distribution, questionCount: p.questionCount, marksEach: p.marksEach, isEitherOr: p.isEitherOr, maxSubdivisions: p.maxSubdivisions ?? 0 }))
  })
  const copyAsNew = (t: any) => { edit(t); setF((x: any) => ({ ...x, id: null })); setMsg({ ok: true, text: 'Copied into the form as a new draft version. Change it, save, then publish.' }) }

  return (
    <>
      <div className="card"><h2>{f.id ? 'Edit draft framework' : 'New QP framework'}</h2>
        <p className="mu">Each regulation has its own framework (e.g. Part A/B or Part A/B/C). "Per unit" counts are for each of the 5 units; "Any unit" counts are totals for the paper. "Sub-divisions" lets setters split a question into (i), (ii)… with their own marks.</p>
        <div className="cols">
          <label>Regulation<select value={f.regulationId} onChange={e => setF({ ...f, regulationId: e.target.value })}>
            {regs.map(r => <option key={r.id} value={r.id}>{r.code}</option>)}</select></label>
          <label>Exam type<input value={f.examType} onChange={e => setF({ ...f, examType: e.target.value })} /></label>
          <label>Name<input value={f.name} placeholder="e.g. R2021 UG Theory" onChange={e => setF({ ...f, name: e.target.value })} /></label>
          <label>Duration<input value={f.duration} onChange={e => setF({ ...f, duration: e.target.value })} /></label>
          <label>Maximum marks<input type="number" value={f.maxMarks} onChange={e => setF({ ...f, maxMarks: e.target.value })} /></label>
        </div>
        <label style={{ marginTop: 8 }}>General instructions<input value={f.instructions} onChange={e => setF({ ...f, instructions: e.target.value })} /></label>
        <div className="sc" style={{ marginTop: 10 }}><table>
          <thead><tr><th>Part</th><th>Title</th><th>Distribution</th><th>Questions</th><th>Marks each</th><th>Either / Or (a)(b)</th><th>Sub-divisions</th><th>Part total</th><th></th></tr></thead>
          <tbody>{f.parts.map((p: any, i: number) => (
            <tr key={i}>
              <td style={{ width: 60 }}><input value={p.partName} onChange={e => setPart(i, 'partName', e.target.value)} /></td>
              <td><input value={p.title} onChange={e => setPart(i, 'title', e.target.value)} /></td>
              <td><select value={p.distribution} onChange={e => setPart(i, 'distribution', e.target.value)}>
                <option value="PerUnit">Per unit (× 5 units)</option><option value="AnyUnit">Any unit (total)</option></select></td>
              <td style={{ width: 90 }}><input type="number" min={1} value={p.questionCount} onChange={e => setPart(i, 'questionCount', e.target.value)} /></td>
              <td style={{ width: 90 }}><input type="number" min={1} value={p.marksEach} onChange={e => setPart(i, 'marksEach', e.target.value)} /></td>
              <td style={{ textAlign: 'center' }}><input type="checkbox" style={{ width: 'auto' }} checked={p.isEitherOr} onChange={e => setPart(i, 'isEitherOr', e.target.checked)} /></td>
              <td style={{ width: 150 }}><select value={p.maxSubdivisions ?? 0} onChange={e => setPart(i, 'maxSubdivisions', Number(e.target.value))}>
                <option value={0}>Not allowed</option><option value={2}>Up to 2: (i), (ii)</option><option value={3}>Up to 3: (i)–(iii)</option><option value={4}>Up to 4: (i)–(iv)</option></select></td>
              <td>{partTotal(p)}</td>
              <td><button className="g" onClick={() => removePart(i)}>✕</button></td>
            </tr>))}</tbody></table></div>
        <div className="flex" style={{ marginTop: 8 }}>
          <button className="g" onClick={addPart}>+ Add part</button>
          <b className={total == f.maxMarks ? 'ok' : 'bad'}>Total: {total} / {f.maxMarks}</b>
          <button onClick={save}>Save draft</button>
          {f.id && <button className="g" onClick={() => setF(blank(regs[0]?.id ?? ''))}>New framework</button>}
        </div>
        {msg && <div className={`msg ${msg.ok ? 'ok' : 'err'}`}>{msg.text}</div>}
      </div>
      <div className="card"><h2>Frameworks by regulation</h2>
        <div className="sc"><table><thead><tr><th>Regulation</th><th>Name</th><th>Parts</th><th>Total</th><th>Status</th><th></th></tr></thead>
          <tbody>{list.map(t => (
            <tr key={t.id}>
              <td>{t.regulation}<br /><span className="mu">{t.examType}</span></td><td>{t.name} <span className="mu">v{t.version}</span></td>
              <td>{t.parts.map((p: any) => <div key={p.partName}>Part {p.partName}: {p.totalQuestions} × {p.marksEach}{p.isEitherOr ? ' (either/or)' : ''} {p.distribution === 'PerUnit' ? '· per unit' : '· any unit'}{p.maxSubdivisions >= 2 ? ` · up to ${p.maxSubdivisions} sub-divisions` : ''}</div>)}</td>
              <td>{t.total}</td>
              <td><span className={`b ${t.status === 'Published' ? 'ok' : t.status === 'Draft' ? 'warn' : ''}`}>{t.status}</span></td>
              <td>{t.status === 'Draft'
                ? <><button className="g" onClick={() => edit(t)}>Edit</button> <button className="okb" onClick={() => publish(t.id)}>Publish</button></>
                : <button className="g sm" onClick={() => copyAsNew(t)}>Copy as new version</button>}</td>
            </tr>))}
            {list.length === 0 && <tr><td colSpan={6} className="mu">No frameworks yet.</td></tr>}
          </tbody></table></div>
      </div>
    </>
  )
}

/* ---------------- 4. Instructions ---------------- */
function InstructionsTab() {
  const [html, setHtml] = useState<string | null>(null)
  const [cur, setCur] = useState('')
  const [msg, setMsg] = useState('')
  useEffect(() => { api('GET', '/api/instructions').then((r: any) => { setHtml(r.html); setCur(r.html) }) }, [])
  async function save() {
    try { await api('PUT', '/api/instructions', { html: cur }); setMsg('Saved. These instructions are sent with every setter invitation and shown on the setter\'s details page.') }
    catch (ex: any) { setMsg(ex.message) }
  }
  return (
    <div className="card"><h2>Common instructions for all QP setters</h2>
      {html === null ? 'Loading…' : <RichEditor id="instructions" html={html} onChange={h => setCur(h)} />}
      <p><button onClick={save}>Save instructions</button></p>
      {msg && <div className="msg ok">{msg}</div>}
    </div>
  )
}

/* ---------------- 5. Suggest & map ---------------- */
function MapTab() {
  const [courses, setCourses] = useState<any[]>([])
  const [setters, setSetters] = useState<any[]>([])
  const [maps, setMaps] = useState<any[]>([])
  const [sug, setSug] = useState<Record<number, any[]>>({})
  const [pick, setPick] = useState<Record<number, any>>({})
  const [examSession, setExamSession] = useState('Nov/Dec 2026')
  const [msg, setMsg] = useState<any>(null)
  const defDue = new Date(Date.now() + 7 * 864e5).toISOString().slice(0, 10)

  async function load() {
    const c: any[] = await api('GET', '/api/courses'); setCourses(c)
    setSetters((await api('GET', '/api/setters')).filter((x: any) => x.registrationStatus === 'Approved'))
    setMaps(await api('GET', '/api/assignments'))
    const all: any = {}
    await Promise.all(c.map(async x => { all[x.id] = await api('GET', `/api/suggestions?courseId=${x.id}`) }))
    setSug(all)
  }
  useEffect(() => { load() }, [])

  const mapped = (c: any) => maps.find(m => m.course.startsWith(c.code + ' - ') && m.examSession === examSession && m.status !== 'Cancelled')
  const val = (c: any) => ({ setterId: pick[c.id]?.setterId ?? sug[c.id]?.[0]?.setterId ?? '', due: pick[c.id]?.due ?? defDue })
  const setVal = (c: any, k: string, v: any) => setPick(p => ({ ...p, [c.id]: { ...val(c), [k]: v } }))

  async function assign(c: any) {
    const v = val(c)
    try {
      const r: any = await api('POST', '/api/assignments', { courseId: c.id, setterId: Number(v.setterId), examSession, dueDate: v.due })
      setMsg({ ok: r.emailSent, text: `${c.code} mapped. Invitation ${r.emailSent ? 'e-mailed with common instructions' : 'NOT e-mailed: ' + r.emailError}.\nLink: ${r.link}` })
      load()
    } catch (ex: any) { setMsg({ ok: false, text: ex.message }) }
  }
  async function resend(m: any) {
    try { const r: any = await api('POST', `/api/assignments/${m.id}/resend`); setMsg({ ok: r.emailSent, text: r.emailSent ? 'Link re-sent.' : r.emailError }) }
    catch (ex: any) { setMsg({ ok: false, text: ex.message }) }
  }

  return (
    <>
      <div className="card"><h2>Suggested QP setters and subject mapping</h2>
        <p className="mu">Score out of 100 = area-of-interest match with the syllabus (60) + designation (20) + experience (20), minus 5 for each paper the setter is already working on.</p>
        <label style={{ maxWidth: 260 }}>Exam session<input value={examSession} onChange={e => setExamSession(e.target.value)} /></label>
        {msg && <div className={`msg ${msg.ok ? 'ok' : 'err'}`}>{msg.text}</div>}
        <div className="sc" style={{ marginTop: 8 }}><table>
          <thead><tr><th>Course</th><th>Top suggestions</th><th>Setter</th><th>Deadline</th><th></th></tr></thead>
          <tbody>{courses.map(c => {
            const m = mapped(c); const v = val(c)
            return (
              <tr key={c.id}>
                <td><b>{c.code}</b><br />{c.title}<br /><span className="mu">{c.regulation}</span></td>
                <td>{(sug[c.id] || []).slice(0, 3).map(t => (
                  <div key={t.setterId} style={{ marginBottom: 4 }}>{t.name} <span className="b ok">{t.score}</span>
                    <br /><span className="mu">{t.designation}, {t.experienceYears} yrs, {t.setterType}{t.matchedTopics?.length ? ' · matches: ' + t.matchedTopics.join(', ') : ''}</span></div>))}</td>
                <td>{m ? <><b>{m.setter}</b><br /><span className="mu">{m.setterType}</span></> :
                  <select value={v.setterId} onChange={e => setVal(c, 'setterId', e.target.value)}>
                    {setters.map(s => <option key={s.id} value={s.id}>{s.name} ({s.setterType})</option>)}</select>}</td>
                <td>{m ? m.dueDate : <input type="date" value={v.due} onChange={e => setVal(c, 'due', e.target.value)} />}</td>
                <td>{m ? <><span className="b ok">{m.status}</span><br /><button className="g sm" onClick={() => resend(m)}>Resend link</button></>
                  : <button onClick={() => assign(c)}>Assign &amp; send link</button>}</td>
              </tr>)
          })}</tbody></table></div>
      </div>
    </>
  )
}

/* ---------------- 6. Analysis ---------------- */
function AnalysisTab() {
  const nav = useNavigate()
  const [d, setD] = useState<any>(null)
  const [msg, setMsg] = useState('')
  const canScr = session.can('scrutiny.assign')
  const [reviewers, setReviewers] = useState<any[]>([])
  const [scr, setScr] = useState<any[]>([])
  const [pick, setPick] = useState<Record<number, string>>({})
  const loadScr = () => api('GET', '/api/scrutiny').then(setScr).catch(() => { })
  useEffect(() => {
    api('GET', '/api/reports/status').then(setD).catch((e: any) => setMsg(e.message))
    if (canScr) { api('GET', '/api/scrutiny/reviewers').then(setReviewers).catch(() => { }); loadScr() }
  }, [])
  async function sendScr(assignmentId: number) {
    const reviewerId = Number(pick[assignmentId] || reviewers[0]?.id)
    try { await api('POST', '/api/scrutiny', { assignmentId, reviewerId }); setMsg(''); loadScr() } catch (e: any) { setMsg(e.message) }
  }
  const SCR: Record<string, string> = { Pending: 'warn', Approved: 'ok', ChangesRequested: 'bad' }
  const SCRT: Record<string, string> = { Pending: 'With reviewer', Approved: 'Approved', ChangesRequested: 'Changes requested' }
  if (!d) return <div className="card">{msg || 'Loading…'}</div>

  const s = d.summary
  const pct = s.mapped ? Math.round(s.qpReceived / s.mapped * 100) : 0
  const today = new Date(new Date().toDateString()).getTime()
  const daysLeft = (due: string) => Math.ceil((new Date(due).getTime() - today) / 864e5)
  const canExport = session.can('claim.export')
  const canQp = session.can('paper.download')
  const dl = (url: string, name: string) => download(url, name).catch(e => setMsg(e.message))

  return (
    <>
      <div className="card"><h2>QP and claim receipt status</h2>
        <div className="cols">
          <div><span className="big ok">{s.qpReceived}</span><br />QP received</div>
          <div><span className="big bad">{s.qpNotReceived}</span><br />QP not received</div>
          <div><span className="big">{pct}%</span><br />Complete</div>
          <div><span className="big warn">{s.overdue}</span><br />Overdue</div>
          <div><span className="big">{s.claimsReceived}</span><br />Claims received</div>
          <div><span className="big">₹{Number(s.totalNetPayable).toLocaleString('en-IN')}</span><br />Net payable</div>
        </div>
        {canExport && <p className="noprint"><button className="or" onClick={() => dl('/api/reports/bank-excel', 'QP_Claims_Bank_Settlement.xlsx')}>⬇ Download bank settlement Excel</button></p>}
        {msg && <div className="msg err">{msg}</div>}
      </div>
      <div className="card sc"><table>
        <thead><tr><th>Course</th><th>Setter</th><th>Deadline</th><th>QP status</th><th>Claim</th>{canScr && <th>Scrutiny</th>}{(canExport || canQp) && <th>Downloads</th>}</tr></thead>
        <tbody>{d.rows.map((r: any) => {
          const dd = daysLeft(r.dueDate)
          return (
            <tr key={r.id}>
              <td>{r.course}<br /><span className="mu">{r.regulation} · {r.examSession}</span></td>
              <td>{r.setter}<br /><span className="mu">{r.setterType} · {r.department} · {r.mobile}</span></td>
              <td>{r.dueDate}</td>
              <td>{r.qpReceived ? <span className="b ok">Received</span>
                : <span className={`b ${dd < 0 ? 'bad' : 'warn'}`}>{dd < 0 ? `Overdue by ${-dd} d` : `Pending · ${dd} d left`}</span>}
                {!r.inviteSent && <span className="b bad">link not sent</span>}</td>
              <td>{r.claimReceived ? <>₹{r.net}</> : <span className="mu">not received</span>}</td>
              {canScr && <td style={{ minWidth: 220 }}>
                {scr.filter(x => x.assignmentId === r.id).map(x => (
                  <div key={x.id} style={{ marginBottom: 4 }}>
                    <span className={`b ${SCR[x.status]}`}>{SCRT[x.status]}</span> <span className="mu">{x.reviewer}</span>
                    {x.remarks && <div className="mu" style={{ fontSize: 12 }}>“{x.remarks}”</div>}
                  </div>))}
                {r.qpReceived ? (reviewers.length ? <div className="flex noprint">
                  <select value={pick[r.id] ?? ''} onChange={e => setPick({ ...pick, [r.id]: e.target.value })} style={{ width: 'auto' }}>
                    {reviewers.map(v => <option key={v.id} value={v.id}>{v.name} ({v.role}{v.department ? ' · ' + v.department : ''})</option>)}
                  </select>
                  <button className="g sm" onClick={() => sendScr(r.id)}>Send for scrutiny</button></div>
                  : <span className="mu">no DCoE / HoD users</span>) : <span className="mu">after QP is received</span>}
              </td>}
              {(canExport || canQp) && <td>
                {canQp && r.qpReceived && <><button className="g sm" onClick={() => nav(`/app/qp/${r.id}`)}>View QP / PDF</button>{' '}
                  <button className="g sm" onClick={() => dl(`/api/papers/${r.id}/docx`, `QP_${r.course.split(' ')[0]}.docx`)}>QP Word</button>{' '}</>}
                {canExport && r.claimReceived && <button className="g sm" onClick={() => dl(`/api/reports/claims/${r.id}/pdf`, `Claim_${r.course.split(' ')[0]}.pdf`)}>Claim PDF</button>}
              </td>}
            </tr>)
        })}</tbody></table>
        {d.notMappedSubjects.length > 0 && <p className="mu">Subjects without a setter: {d.notMappedSubjects.join('; ')}</p>}
      </div>
    </>
  )
}

/* ---------------- 7. Scrutiny (DCoE / HoD) ---------------- */
function ScrutinyTab() {
  const nav = useNavigate()
  const [rows, setRows] = useState<any[] | null>(null)
  const [msg, setMsg] = useState('')
  useEffect(() => { api('GET', '/api/scrutiny/mine').then(setRows).catch((e: any) => setMsg(e.message)) }, [])
  const SCR: Record<string, string> = { Pending: 'warn', Approved: 'ok', ChangesRequested: 'bad' }
  const SCRT: Record<string, string> = { Pending: 'To review', Approved: 'Approved', ChangesRequested: 'Changes requested' }
  return (
    <div className="card"><h2>Question papers sent to me for scrutiny</h2>
      <p className="mu">Read the paper, then approve it or return the changes needed to the CoE. The setter's identity is hidden and papers cannot be downloaded.</p>
      {msg && <div className="msg err">{msg}</div>}
      {rows && rows.length === 0 && <p className="mu">Nothing assigned to you yet.</p>}
      {rows && rows.length > 0 && <div className="sc"><table>
        <thead><tr><th>Course</th><th>Sent on</th><th>Status</th><th></th></tr></thead>
        <tbody>{rows.map(r => <tr key={r.id}>
          <td><b>{r.code}</b> {r.title}<br /><span className="mu">{r.examSession}</span></td>
          <td>{new Date(r.assignedAt).toLocaleDateString('en-IN')}</td>
          <td><span className={`b ${SCR[r.status]}`}>{SCRT[r.status]}</span>{r.remarks && <div className="mu" style={{ fontSize: 12 }}>“{r.remarks}”</div>}</td>
          <td><button className="sm" onClick={() => nav(`/app/scrutiny/${r.id}`)}>{r.status === 'Pending' ? 'Open & review' : 'Open'}</button></td>
        </tr>)}</tbody></table></div>}
    </div>)
}
