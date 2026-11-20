import { NavLink, useNavigate } from 'react-router-dom'
import { LayoutDashboard, FolderKanban, CheckSquare2, LogOut, ListChecks } from 'lucide-react'
import { useAuth } from '../../context/useAuth'

const navigation = [
  { label: 'Dashboard', to: '/dashboard', icon: LayoutDashboard },
  { label: 'Projects', to: '/projects', icon: FolderKanban },
  { label: 'My Tasks', to: '/tasks', icon: CheckSquare2 },
]

export default function Sidebar() {
  const { logout } = useAuth()
  const navigate = useNavigate()

  const handleLogout = () => {
    logout()
    navigate('/login', { replace: true })
  }

  return (
    <aside className="sidebar">
      <NavLink className="brand" to="/dashboard" aria-label="TaskFlow dashboard">
        <span className="brand-mark"><ListChecks size={20} /></span>
        <span>TaskFlow</span>
      </NavLink>

      <nav className="sidebar-nav" aria-label="Primary navigation">
        {navigation.map(({ label, to, icon: Icon }) => (
          <NavLink
            key={to}
            to={to}
            className={({ isActive }) => `nav-item${isActive ? ' active' : ''}`}
            aria-label={label}
            title={label}
          >
            <Icon size={19} strokeWidth={2} aria-hidden="true" />
            <span>{label}</span>
          </NavLink>
        ))}
      </nav>

      <button className="logout-button" type="button" onClick={handleLogout} aria-label="Logout" title="Logout">
        <LogOut size={18} aria-hidden="true" />
        <span>Logout</span>
      </button>
    </aside>
  )
}
