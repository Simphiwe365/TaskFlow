import { NavLink, useNavigate } from 'react-router-dom'
import { LayoutDashboard, FolderKanban, CheckSquare2, Settings, LogOut, ListChecks } from 'lucide-react'
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
        <p className="nav-label">Workspace</p>
        {navigation.map(({ label, to, icon: Icon }) => (
          <NavLink
            key={to}
            to={to}
            className={({ isActive }) => `nav-item${isActive ? ' active' : ''}`}
          >
            <Icon size={19} strokeWidth={2} />
            <span>{label}</span>
          </NavLink>
        ))}

        <p className="nav-label nav-label-secondary">General</p>
        <button className="nav-item nav-button" type="button">
          <Settings size={19} strokeWidth={2} />
          <span>Settings</span>
        </button>
      </nav>

      <button className="logout-button" type="button" onClick={handleLogout}>
        <LogOut size={18} />
        <span>Logout</span>
      </button>
    </aside>
  )
}
