import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, session } from '../api'
import Header from '../components/Header'

export default function SetterLink() {
  const nav = useNavigate()
  const token = new URLSearchParams(window.location.search).get('token') || ''
  const [loginId, setLoginId] = useState('')
  const [dob, setDob] = useState('')
  const [err, setErr] = useState('')

  async function login(e: any) {
    e.preventDefault(); setErr('')
    try {
            const r = await api('POST', '/api/auth/login', { loginId, password: dob })
      session.set(r)
      if (token) {
        const a = await api('GET', `/api/setter/assignments/by-token/${token}`)
                nav(`/app/paper/${a.id}`)
      } else nav('/app/my')
    } catch (ex: any) { setErr(ex.message) }
  }

  return (
    <>
      <Header />
      <div className="home" style={{ maxWidth: 480 }}>
        <form className="card" onSubmit={login}>
          <h3>QP setter login</h3>
          <p className="muted">You received this link from the Controller of Examinations. Verify your identity to continue.</p>
          <label>Mobile number or e-mail<input value={loginId} onChange={e => setLoginId(e.target.value)} required /></label>
          <label style={{ marginTop: 8 }}>Date of birth<input type="date" value={dob} onChange={e => setDob(e.target.value)} required /></label>
          {err && <div className="msg err">{err}</div>}
          <div style={{ marginTop: 12 }}><button>Continue</button></div>
        </form>
      </div>
    </>
  )
}