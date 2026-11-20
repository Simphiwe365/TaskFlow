import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  AlertCircle,
  ArrowUpRight,
  CheckSquare2,
  CircleCheck,
  Clock3,
  FolderKanban,
} from 'lucide-react'
import StatCard from '../components/common/StatCard'
import api from '../services/api'
import { formatDate, getDateKey, getTodayDateKey } from '../utils/dateUtils'

const taskStatuses = [
  { key: 'ToDo', label: 'To Do' },
  { key: 'InProgress', label: 'In Progress' },
  { key: 'Review', label: 'Review' },
  { key: 'Done', label: 'Done' },
  { key: 'Blocked', label: 'Blocked' },
]

const taskPriorities = [
  { key: 'Low', label: 'Low' },
  { key: 'Medium', label: 'Medium' },
  { key: 'High', label: 'High' },
  { key: 'Urgent', label: 'Urgent' },
]

function getApiError(error, fallback) {
  const data = error?.response?.data

  if (typeof data?.message === 'string' && data.message.trim()) {
    return data.message
  }

  if (data?.errors) {
    const messages = Object.values(data.errors).flat().filter(Boolean)
    if (messages.length > 0) return messages.join(' ')
  }

  if (typeof data?.detail === 'string' && data.detail.trim()) {
    return data.detail
  }

  if (typeof data?.title === 'string' && data.title.trim()) {
    return data.title
  }

  if (error?.request) {
    return 'Unable to reach the TaskFlow API. Check that the backend is running.'
  }

  return fallback
}

function getEnumKey(value, options) {
  if (typeof value === 'number') {
    return options[value]?.key ?? null
  }

  if (typeof value !== 'string') return null

  const normalized = value.replaceAll(/[^a-z]/gi, '').toLowerCase()
  return options.find((option) => option.key.toLowerCase() === normalized)?.key ?? null
}

function getCreatedAtTime(task) {
  if (!task.createdAt) return null

  const timestamp = new Date(task.createdAt).getTime()
  return Number.isNaN(timestamp) ? null : timestamp
}

function getStatusLabel(status) {
  const key = getEnumKey(status, taskStatuses)
  return taskStatuses.find((option) => option.key === key)?.label ?? 'Unknown'
}

function getPriorityLabel(priority) {
  const key = getEnumKey(priority, taskPriorities)
  return taskPriorities.find((option) => option.key === key)?.label ?? 'Unknown'
}

function getStatusClass(status) {
  return getStatusLabel(status).toLowerCase().replaceAll(' ', '-')
}

function getPriorityClass(priority) {
  return getPriorityLabel(priority).toLowerCase()
}

export default function Dashboard() {
  const [projects, setProjects] = useState([])
  const [projectTaskResults, setProjectTaskResults] = useState([])
  const [failedProjectCount, setFailedProjectCount] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let ignore = false

    const loadDashboard = async () => {
      setLoading(true)
      setError('')
      setFailedProjectCount(0)

      try {
        const projectsResponse = await api.get('/projects')
        const ownedProjects = Array.isArray(projectsResponse.data) ? projectsResponse.data : []

        const taskResults = await Promise.all(ownedProjects.map(async (project) => {
          try {
            const response = await api.get(`/projects/${project.id}/tasks`)
            return {
              project,
              tasks: Array.isArray(response.data) ? response.data : [],
              failed: false,
            }
          } catch {
            return { project, tasks: [], failed: true }
          }
        }))

        if (!ignore) {
          setProjects(ownedProjects)
          setProjectTaskResults(taskResults)
          setFailedProjectCount(taskResults.filter((result) => result.failed).length)
        }
      } catch (requestError) {
        if (!ignore) {
          setError(getApiError(requestError, 'Could not load dashboard data.'))
          setProjects([])
          setProjectTaskResults([])
        }
      } finally {
        if (!ignore) setLoading(false)
      }
    }

    loadDashboard()

    return () => {
      ignore = true
    }
  }, [reloadKey])

  const tasks = useMemo(() => {
    const taskIds = new Set()
    const uniqueTasks = []

    projectTaskResults.forEach(({ project, tasks: projectTasks }) => {
      projectTasks.forEach((task) => {
        if (!task || typeof task !== 'object') return

        if (task.id !== null && task.id !== undefined) {
          const taskId = String(task.id)
          if (taskIds.has(taskId)) return
          taskIds.add(taskId)
        }

        uniqueTasks.push({
          ...task,
          projectId: task.projectId ?? project.id,
          projectName: project.name ?? 'Project unavailable',
        })
      })
    })

    return uniqueTasks
  }, [projectTaskResults])

  const recentTasks = useMemo(() => [...tasks].sort((first, second) => {
    const firstCreatedAt = getCreatedAtTime(first)
    const secondCreatedAt = getCreatedAtTime(second)

    if (firstCreatedAt === null) return secondCreatedAt === null ? 0 : 1
    if (secondCreatedAt === null) return -1
    return secondCreatedAt - firstCreatedAt
  }).slice(0, 5), [tasks])

  const taskCounts = useMemo(() => {
    const statusCounts = Object.fromEntries(taskStatuses.map(({ key }) => [key, 0]))
    const priorityCounts = Object.fromEntries(taskPriorities.map(({ key }) => [key, 0]))
    const todayKey = getTodayDateKey()
    let completed = 0
    let overdue = 0
    const upcoming = []

    tasks.forEach((task) => {
      const statusKey = getEnumKey(task.status, taskStatuses)
      const priorityKey = getEnumKey(task.priority, taskPriorities)
      const dueDateKey = getDateKey(task.dueDate)

      if (statusKey) statusCounts[statusKey] += 1
      if (priorityKey) priorityCounts[priorityKey] += 1

      if (statusKey === 'Done') {
        completed += 1
        return
      }

      if (dueDateKey && dueDateKey < todayKey) {
        overdue += 1
      } else if (dueDateKey && dueDateKey >= todayKey) {
        upcoming.push({ ...task, dueDateKey })
      }
    })

    upcoming.sort((first, second) => first.dueDateKey.localeCompare(second.dueDateKey))

    return {
      completed,
      incomplete: tasks.length - completed,
      overdue,
      status: statusCounts,
      priority: priorityCounts,
      upcoming,
    }
  }, [tasks])

  const todayLabel = new Intl.DateTimeFormat('en', {
    weekday: 'long',
    month: 'long',
    day: 'numeric',
  }).format(new Date())
  const todayKey = getTodayDateKey()

  if (loading) {
    return <div className="page"><div className="api-state" role="status" aria-live="polite">Loading dashboard...</div></div>
  }

  if (error) {
    return (
      <div className="page">
        <div className="api-state api-error" role="alert">
          <p>{error}</p>
          <button className="secondary-button" type="button" onClick={() => setReloadKey((current) => current + 1)}>Try again</button>
        </div>
      </div>
    )
  }

  return (
    <div className="page dashboard-page">
      <section className="welcome-row">
        <div>
          <p className="eyebrow">{todayLabel}</p>
          <h2>Welcome back</h2>
          <p className="welcome-copy">Here is what is happening across your projects today.</p>
        </div>
        <Link className="primary-button" to="/projects"><FolderKanban size={17} /> Manage projects</Link>
      </section>

      <section className="stats-grid" aria-label="Project statistics">
        <StatCard icon={FolderKanban} label="Total Projects" value={projects.length} />
        <StatCard icon={CheckSquare2} label="Total Tasks" value={tasks.length} tone="violet" />
        <StatCard icon={CircleCheck} label="Completed Tasks" value={taskCounts.completed} tone="green" />
        <StatCard icon={Clock3} label="Incomplete Tasks" value={taskCounts.incomplete} tone="amber" />
        <StatCard icon={AlertCircle} label="Overdue Tasks" value={taskCounts.overdue} tone="amber" />
      </section>

      {failedProjectCount > 0 && (
        <div className="api-state api-warning" role="status">
          <span>Task lists could not be loaded for {failedProjectCount} of {projects.length} projects. Task totals include only successfully loaded projects.</span>
          <button className="text-button" type="button" onClick={() => setReloadKey((current) => current + 1)}>Retry task loading</button>
        </div>
      )}

      {projects.length === 0 ? (
        <section className="api-state api-empty dashboard-empty">
          <div>
            <h3>No projects yet</h3>
            <p>Create a project to start organizing tasks and deadlines.</p>
            <Link className="primary-button" to="/projects">Create your first project</Link>
          </div>
        </section>
      ) : (
        <>
          <section className="dashboard-grid">
            <article className="panel recent-projects">
              <div className="panel-header">
                <div>
                  <p className="eyebrow">Workspace</p>
                  <h3>Recent Projects</h3>
                </div>
                <Link className="text-button" to="/projects">View all <ArrowUpRight size={15} /></Link>
              </div>
              <div className="project-list">
                {projects.slice(0, 5).map((project) => (
                  <div className="project-row" key={project.id}>
                    <div className={`project-avatar ${['blue', 'violet', 'green', 'amber'][project.id % 4]}`} aria-hidden="true">
                      {project.name?.charAt(0)?.toUpperCase() ?? 'P'}
                    </div>
                    <div className="project-row-copy">
                      <div className="project-row-heading">
                        <strong>{project.name}</strong>
                        <span>{project.taskCount ?? 0} tasks</span>
                      </div>
                      <p>{project.description || 'No description provided.'}</p>
                    </div>
                    <div className="project-due">
                      <span>Due date</span>
                      <strong>{formatDate(project.dueDate) ?? 'No due date'}</strong>
                    </div>
                  </div>
                ))}
              </div>
            </article>

            <article className="panel recent-tasks">
              <div className="panel-header">
                <div>
                  <p className="eyebrow">Latest created</p>
                  <h3>Recent Tasks</h3>
                </div>
                <Link className="text-button" to="/tasks">View all <ArrowUpRight size={15} /></Link>
              </div>
              <div className="task-list">
                {recentTasks.length === 0 ? (
                  <p className="list-empty">No tasks in your projects yet.</p>
                ) : recentTasks.map((task) => (
                  <div className="task-row" key={task.id}>
                    <div className="task-row-copy">
                      <strong>{task.title}</strong>
                      <span>
                        {task.projectName} · Due {formatDate(task.dueDate) ?? 'No due date'}
                        {getDateKey(task.dueDate) && getDateKey(task.dueDate) < todayKey && getEnumKey(task.status, taskStatuses) !== 'Done' && (
                          <span className="overdue-label">Overdue</span>
                        )}
                      </span>
                    </div>
                    <div className="task-badges">
                      <span className={`status-badge status-${getStatusClass(task.status)}`}>{getStatusLabel(task.status)}</span>
                      <span className={`priority-badge priority-${getPriorityClass(task.priority)}`}>{getPriorityLabel(task.priority)}</span>
                    </div>
                  </div>
                ))}
              </div>
            </article>
          </section>

          <section className="dashboard-detail-grid">
            <article className="panel">
              <div className="panel-header">
                <div>
                  <p className="eyebrow">Task breakdown</p>
                  <h3>Tasks by Status</h3>
                </div>
              </div>
              <div className="summary-list">
                {taskStatuses.map(({ key, label }) => {
                  const count = taskCounts.status[key]
                  const percentage = tasks.length === 0 ? 0 : (count / tasks.length) * 100

                  return (
                    <div className="summary-row" key={key}>
                      <span>{label}</span>
                      <div className="summary-track" role="progressbar" aria-label={`${label} tasks`} aria-valuemin="0" aria-valuemax="100" aria-valuenow={Math.round(percentage)}>
                        <span style={{ width: `${percentage}%` }} />
                      </div>
                      <strong>{count}</strong>
                    </div>
                  )
                })}
              </div>
            </article>

            <article className="panel">
              <div className="panel-header">
                <div>
                  <p className="eyebrow">Task breakdown</p>
                  <h3>Tasks by Priority</h3>
                </div>
              </div>
              <div className="summary-list">
                {taskPriorities.map(({ key, label }) => {
                  const count = taskCounts.priority[key]
                  const percentage = tasks.length === 0 ? 0 : (count / tasks.length) * 100

                  return (
                    <div className="summary-row" key={key}>
                      <span>{label}</span>
                      <div className="summary-track" role="progressbar" aria-label={`${label} priority`} aria-valuemin="0" aria-valuemax="100" aria-valuenow={Math.round(percentage)}>
                        <span className={`priority-track-${key.toLowerCase()}`} style={{ width: `${percentage}%` }} />
                      </div>
                      <strong>{count}</strong>
                    </div>
                  )
                })}
              </div>
            </article>

            <article className="panel upcoming-deadlines">
              <div className="panel-header">
                <div>
                  <p className="eyebrow">Next due</p>
                  <h3>Upcoming Deadlines</h3>
                </div>
                <Link className="text-button" to="/tasks">View all <ArrowUpRight size={15} /></Link>
              </div>
              <div className="deadline-list">
                {taskCounts.upcoming.length === 0 ? (
                  <p className="list-empty">No upcoming deadlines.</p>
                ) : taskCounts.upcoming.map((task) => (
                  <div className="deadline-row" key={task.id}>
                    <div className="deadline-copy">
                      <strong>{task.title}</strong>
                      <span>{task.projectName} · {getStatusLabel(task.status)}</span>
                    </div>
                    <span className="deadline-date">{task.dueDateKey === todayKey ? 'Today' : formatDate(task.dueDate)}</span>
                  </div>
                ))}
              </div>
            </article>
          </section>
        </>
      )}
    </div>
  )
}
