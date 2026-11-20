export default function StatCard({ icon: Icon, label, value, change, tone = 'blue' }) {
  return (
    <article className="stat-card">
      <div className={`stat-icon ${tone}`}>
        <Icon size={20} aria-hidden="true" />
      </div>
      <div className="stat-copy">
        <span>{label}</span>
        <strong>{value}</strong>
      </div>
      {change && <span className="stat-change">{change}</span>}
    </article>
  )
}
