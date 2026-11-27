import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import Header from '../components/Header'

const empty = {
  name: '', email: '', mobile: '', dateOfBirth: '', designation: 'Assistant Professor', department: '',
  institution: '', setterType: 'Internal', experienceYears: '', qualification: '', areaOfInterest: ''
}

export default function Register() {
  const [f, setF] = useState<any>(empty)
  const [err, setErr] = useState('')
  const [done, setDone] = useState('')
  const set = (k: string) => (e: any) => setF({ ...f, [k]: e.target.value })

  async function submit(e: any) {
    e.preventDefault(); setErr('')
    try {
      const r = await api('POST', '/api/register', {
        ...f, experienceYears: f.experienceYears ? Number(f.experienceYears) : null
      })
      setDone(r.message)
    } catch (ex: any) { setErr(ex.message) }
  }

  return (
    <>
      <Header />
      <div className="home">
        <h2>Register as QP setter</h2>
        {done ? (
          <div className="card">
            <div className="msg ok">{done}</div>
            <Link to="/"><button className="sec">Back to home</button></Link>
          </div>
        ) : (
          <form className="card" onSubmit={submit}>
            <div className="grid">
              <label>Full name *<input value={f.name} onChange={set('name')} required /></label>
              <label>E-mail *<input type="email" value={f.email} onChange={set('email')} required /></label>
              <label>Mobile *<input value={f.mobile} onChange={set('mobile')} maxLength={10} required /></label>
              <label>Date of birth *<input type="date" value={f.dateOfBirth} onChange={set('dateOfBirth')} required /></label>
              <label>Designation
                <select value={f.designation} onChange={set('designation')}>
                  <option>Professor</option><option>Associate Professor</option><option>Assistant Professor</option>
                </select>
              </label>
              <label>Department<input value={f.department} onChange={set('department')} /></label>
              <label>Institution<input value={f.institution} onChange={set('institution')} /></label>
              <label>Internal / External *
                <select value={f.setterType} onChange={set('setterType')}>
                  <option>Internal</option><option>External</option>
                </select>
              </label>
              <label>Teaching experience (years)<input type="number" min={0} value={f.experienceYears} onChange={set('experienceYears')} /></label>
              <label>Highest qualification<input value={f.qualification} onChange={set('qualification')} /></label>
            </div>
            <label style={{ marginTop: 10 }}>Areas of interest (comma-separated subjects / topics)
              <textarea rows={2} value={f.areaOfInterest} onChange={set('areaOfInterest')} />
            </label>
            {err && <div className="msg err">{err}</div>}
            <div className="row" style={{ marginTop: 12 }}>
              <button className="ok">Submit registration</button>
              <Link to="/"><button type="button" className="sec">Cancel</button></Link>
            </div>
            <p className="muted">You will be able to log in after the exam cell approves your registration.</p>
          </form>
        )}
      </div>
    </>
  )
}