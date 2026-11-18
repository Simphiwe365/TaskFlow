import { FolderKanban, CheckSquare2, CircleCheck, Clock3, ArrowUpRight } from 'lucide-react'
import StatCard from '../components/common/StatCard'

const projects = [
  { name: 'Website Redesign', description: 'Refresh the marketing experience', due: 'Dec 18', progress: 72, tone: 'blue' },
  { name: 'Mobile Application', description: 'Customer account management app', due: 'Jan 12', progress: 48, tone: 'violet' },
  { name: 'TaskFlow Platform', description: 'Internal project management system', due: 'Jan 28', progress: 86, tone: 'green' },
]

const tasks = [
  { title: 'Review homepage wireframes', project: 'Website Redesign', status: 'In Review', priority: 'High', due: 'Today' },
  { title: 'Set up authentication flow', project: 'TaskFlow Platform', status: 'In Progress', priority: 'Medium', due: 'Tomorrow' },
  { title: 'Create app navigation', project: 'Mobile Application', status: 'To Do', priority: 'Low', due: 'Dec 20' },
]

export default function Dashboard() {
  return (
    <div className="page dashboard-page">
      <section className="welcome-row">
        <div>
          <p className="eyebrow">Monday, December 16</p>
          <h2>Welcome back, Alex</h2>
          <p className="welcome-copy">Here is what is happening across your projects today.</p>
        </div>
        <button className="primary-button" type="button">+ Create project</button>
      </section>

      <section className="stats-grid" aria-label="Project statistics">
        <StatCard icon={FolderKanban} label="Total Projects" value="12" change="+2 this month" />
        <StatCard icon={CheckSquare2} label="Total Tasks" value="48" change="+8 this week" tone="violet" />
        <StatCard icon={CircleCheck} label="Completed Tasks" value="31" change="65% completion" tone="green" />
        <StatCard icon={Clock3} label="Pending Tasks" value="17" change="4 due soon" tone="amber" />
      </section>

      <section className="dashboard-grid">
        <article className="panel recent-projects">
          <div className="panel-header">
            <div>
              <p className="eyebrow">Overview</p>
              <h3>Recent Projects</h3>
            </div>
            <button className="text-button" type="button">View all <ArrowUpRight size={15} /></button>
          </div>
          <div className="project-list">
            {projects.map((project) => (
              <div className="project-row" key={project.name}>
                <div className={`project-avatar ${project.tone}`}>{project.name.charAt(0)}</div>
                <div className="project-row-copy">
                  <div className="project-row-heading">
                    <strong>{project.name}</strong>
                    <span>{project.progress}%</span>
                  </div>
                  <p>{project.description}</p>
                  <div className="progress-track"><span style={{ width: `${project.progress}%` }} /></div>
                </div>
                <div className="project-due">
                  <span>Due date</span>
                  <strong>{project.due}</strong>
                </div>
              </div>
            ))}
          </div>
        </article>

        <article className="panel recent-tasks">
          <div className="panel-header">
            <div>
              <p className="eyebrow">Priority queue</p>
              <h3>Recent Tasks</h3>
            </div>
            <button className="text-button" type="button">View all <ArrowUpRight size={15} /></button>
          </div>
          <div className="task-list">
            {tasks.map((task) => (
              <div className="task-row" key={task.title}>
                <button className="task-check" type="button" aria-label={`Mark ${task.title} complete`} />
                <div className="task-row-copy">
                  <strong>{task.title}</strong>
                  <span>{task.project}</span>
                </div>
                <span className={`status-badge status-${task.status.toLowerCase().replaceAll(' ', '-')}`}>{task.status}</span>
              </div>
            ))}
          </div>
        </article>
      </section>
    </div>
  )
}
