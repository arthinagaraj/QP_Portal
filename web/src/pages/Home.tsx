import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api, session } from '../api'
import Header from '../components/Header'

export default function Home() {
  const nav = useNavigate()
  const [loginId, setLoginId] = useState('')
  const [password, setPassword] = useState('')
  const [show, setShow] = useState(false)
  const [err, setErr] = useState('')

  async function login(e: any) {
    e.preventDefault(); setErr('')
    try {
      const r = await api('POST', '/api/auth/login', { loginId, password })
      session.set(r)
      nav(r.permissions.includes('paper.edit') ? '/app/my' : '/app')
    } catch (ex: any) { setErr(ex.message) }
  }

  return (
    <>
      <Header />
      <div className="home" style={{ maxWidth: 460 }}>
        <h2>Question Paper Setting &amp; Collection Portal</h2>
        <form className="card" onSubmit={login}>
          <h3>Login</h3>
          <label>E-mail or mobile number
            <input value={loginId} onChange={e => setLoginId(e.target.value)} autoFocus required />
          </label>
          <label style={{ marginTop: 10 }}>Password
            <div className="row" style={{ flexWrap: 'nowrap' }}>
              <input type={show ? 'text' : 'password'} value={password}
                     onChange={e => setPassword(e.target.value)} required />
              <button type="button" className="sm sec" onClick={() => setShow(!show)}>{show ? 'Hide' : 'Show'}</button>
            </div>
          </label>
          <p className="muted">QP setters: use your date of birth as the password, e.g. 15061985.</p>
          {err && <div className="msg err">{err}</div>}
          <button style={{ width: '100%', marginTop: 6 }}>Login</button>
        </form>
        <div className="card" style={{ textAlign: 'center' }}>
          <p className="muted" style={{ marginTop: 0 }}>New faculty member? Register to be considered as a question paper setter.</p>
          <Link to="/register"><button type="button" className="ok">Register as QP setter</button></Link>
        </div>
      </div>
    </>
  )
}