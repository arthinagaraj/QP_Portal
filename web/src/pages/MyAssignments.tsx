import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../api'

export default function MyAssignments() {
  const nav = useNavigate()
  const [rows, setRows] = useState<any[] | null>(null)
  useEffect(() => { api('GET', '/api/setter/assignments').then(setRows) }, [])
  if (!rows) return <div className="card">Loading…</div>
  return (
    <div className="card"><h2>My question paper assignments</h2>
      {rows.length === 0 && <p className="mu">No assignments yet.</p>}
      <div className="sc"><table><thead><tr><th>Course</th><th>Session</th><th>Deadline</th><th>Status</th><th></th></tr></thead>
        <tbody>{rows.map(r => (
          <tr key={r.id}><td><b>{r.code}</b> – {r.title}</td><td>{r.examSession}</td><td>{r.dueDate}</td>
            <td><span className={`b ${r.status === 'Submitted' ? 'ok' : 'warn'}`}>{r.status}</span></td>
            <td><button onClick={() => nav(`/app/paper/${r.id}`)}>{r.status === 'Submitted' ? 'View claim' : 'Open'}</button></td></tr>))}
        </tbody></table></div>
    </div>
  )
}