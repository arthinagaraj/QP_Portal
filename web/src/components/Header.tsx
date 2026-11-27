import { useNavigate } from 'react-router-dom'
import { session, COLLEGE } from '../api'

export default function Header() {
  const s = session.get()
  const nav = useNavigate()
  return (
    <header className="top">
      <img src="/kce-logo.png" alt={COLLEGE} style={{ height: 56, maxWidth: '55%', objectFit: 'contain' }} />
      <div style={{ borderLeft: '2px solid var(--bd)', paddingLeft: 14 }}>
        <h1>QP Setting &amp; Collection Portal</h1>
        <div className="sub">Office of the Controller of Examinations, Coimbatore</div>
      </div>
      {s && (
        <div className="user">
          <b>{s.name}</b><br />
          <span className="muted">{s.roles.join(', ')}</span>{' '}
          <button className="sm sec" onClick={() => { session.clear(); nav('/') }}>Logout</button>
        </div>
      )}
    </header>
  )
}