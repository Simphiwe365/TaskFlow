import { Bell, Search } from 'lucide-react'

export default function Topbar({ title }) {
  return (
    <header className="topbar">
      <h1 className="topbar-title">{title}</h1>

      <div className="topbar-actions">
        <label className="search-box" aria-label="Search">
          <Search size={18} />
          <input type="search" placeholder="Search..." />
        </label>
        <button className="icon-button" type="button" aria-label="Notifications">
          <Bell size={19} />
          <span className="notification-dot" />
        </button>
        <div className="profile">
          <div className="avatar">AM</div>
          <div className="profile-copy">
            <strong>Alex Morgan</strong>
            <span>Project Manager</span>
          </div>
        </div>
      </div>
    </header>
  )
}
