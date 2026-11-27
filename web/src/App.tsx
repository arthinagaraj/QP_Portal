import { BrowserRouter, Routes, Route, Navigate, Outlet } from 'react-router-dom'
import { session } from './api'
import Header from './components/Header'
import Home from './pages/Home'
import Register from './pages/Register'
import SetterLink from './pages/SetterLink'
import Admin from './pages/Admin'
import MyAssignments from './pages/MyAssignments'
import Paper from './pages/Paper'
import QpView from './pages/QpView'

function Layout() {
  if (!session.get()) return <Navigate to="/" replace />
  return (<><Header /><main className="page"><Outlet /></main></>)
}

function Start() {
  return <Navigate to={session.can('paper.edit') ? '/app/my' : '/app/admin'} replace />
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/register" element={<Register />} />
        <Route path="/setter" element={<SetterLink />} />
        <Route path="/app" element={<Layout />}>
          <Route index element={<Start />} />
          <Route path="admin" element={<Admin />} />
          <Route path="my" element={<MyAssignments />} />
          <Route path="paper/:id" element={<Paper />} />
          <Route path="qp/:id" element={<QpView />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
