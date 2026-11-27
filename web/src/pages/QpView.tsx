import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import DOMPurify from 'dompurify'
import 'katex/dist/katex.min.css'
import { api, download } from '../api'

// Exam cell view of a submitted question paper: Word (editable) download and PDF (print) download
export default function QpView() {
  const { id } = useParams()
  const nav = useNavigate()
  const [d, setD] = useState<any>(null)
  const [err, setErr] = useState('')
  useEffect(() => { api('GET', `/api/papers/${id}`).then(setD).catch((e: any) => setErr(e.message)) }, [id])

  if (err) return <div className="card"><div className="msg err">{err}</div><button className="g" onClick={() => nav(-1)}>← Back</button></div>
  if (!d) return <div className="card">Loading…</div>

  const safe = (h: string) => ({ __html: DOMPurify.sanitize(h || '') })
  const word = () => download(`/api/papers/${id}/docx`, `QP_${d.course.code}.docx`).catch((e: any) => setErr(e.message))
  const c = { textAlign: 'center' as const }

  return (
    <>
      <div className="card noprint flex">
        <button className="g" onClick={() => nav(-1)}>← Back</button>
        <button className="or" onClick={word}>⬇ Download Word (editable)</button>
        <button onClick={() => window.print()}>⬇ Download PDF / Print</button>
        <span className="mu">For PDF choose "Save as PDF" as the printer. Setter: {d.setter} ({d.setterType}) · submitted {d.submittedAt ? new Date(d.submittedAt).toLocaleString('en-IN') : ''}</span>
      </div>
      <div className="card qp">
        <div style={c}>
          <img src="/kce-logo.png" alt="" style={{ height: 60, maxWidth: '100%' }} />
          <div style={{ fontWeight: 700, fontSize: 16, marginTop: 6 }}>{d.college}</div>
          <div><b>{d.examLine}</b></div>
          <div><b>{d.course.code} – {d.course.title}</b></div>
          <div>(Regulation {d.regulation})</div>
        </div>
        <div className="flex" style={{ justifyContent: 'space-between', margin: '8px 0' }}>
          <b>Time: {d.duration}</b><b>Maximum Marks: {Number(d.maxMarks)}</b>
        </div>
        {d.instructions && <p style={{ ...c, fontStyle: 'italic' }}>{d.instructions}</p>}
        {d.parts.map((p: any) => (
          <div key={p.heading} style={{ marginTop: 14 }}>
            <div style={{ ...c, fontWeight: 700, marginBottom: 6 }}>{p.heading}</div>
            <table>
              <thead><tr><th style={{ width: '9%' }}>Q.No.</th><th>Question</th><th style={{ width: '8%' }}>Marks</th><th style={{ width: '7%' }}>CO</th><th style={{ width: '7%' }}>BTL</th></tr></thead>
              <tbody>{p.rows.flatMap((r: any) => [
                ...(r.option === 'b' ? [<tr key={r.qLabel + '-or'}><td colSpan={5} style={{ ...c, fontWeight: 700 }}>(OR)</td></tr>] : []),
                ...(r.entries.length === 0
                  ? [<tr key={r.qLabel}><td style={c}>{r.qLabel}</td><td><i className="mu">not entered</i></td><td /><td /><td /></tr>]
                  : r.entries.map((e: any, i: number) => (
                    <tr key={r.qLabel + '-' + e.subLabel}>
                      <td style={c}>{i === 0 ? r.qLabel : ''}</td>
                      <td>{e.subLabel && <b>({e.subLabel}) </b>}<div className="qp-q" dangerouslySetInnerHTML={safe(e.html)} /></td>
                      <td style={c}>{Number(e.marks)}</td><td style={c}>{e.co}</td><td style={c}>{e.btl}</td>
                    </tr>)))
              ])}</tbody>
            </table>
          </div>))}
      </div>
    </>
  )
}
