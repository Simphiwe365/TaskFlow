import { Outlet, useLocation } from 'react-router-dom'
import Sidebar from './Sidebar'
import Topbar from './Topbar'

const pageTitles = {
  '/dashboard': 'Dashboard',
  '/projects': 'Projects',
  '/tasks': 'My Tasks',
}

export default function MainLayout() {
  const location = useLocation()
  const title = pageTitles[location.pathname] ?? 'TaskFlow'

  return (
    <div className="app-shell">
      <Sidebar />
      <div className="main-column">
        <Topbar title={title} />
        <main className="main-content">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
