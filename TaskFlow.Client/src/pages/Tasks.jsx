import { useEffect, useMemo, useState } from 'react'
import { Plus, Filter, MoreHorizontal, Pencil, Trash2, X, CalendarDays } from 'lucide-react'
import api from '../services/api'

const statusOptions = [
	{ value: 0, label: 'To Do' },
	{ value: 1, label: 'In Progress' },
	{ value: 2, label: 'Review' },
	{ value: 3, label: 'Done' },
	{ value: 4, label: 'Blocked' },
]

const priorityOptions = [
	{ value: 0, label: 'Low' },
	{ value: 1, label: 'Medium' },
	{ value: 2, label: 'High' },
	{ value: 3, label: 'Urgent' },
]

const emptyForm = {
	title: '',
	description: '',
	status: 0,
	priority: 1,
	dueDate: '',
	assigneeId: '',
}

function getApiError(error, fallback) {
	const data = error?.response?.data

	if (typeof data?.message === 'string' && data.message.trim()) {
		return data.message
	}

	if (data?.errors) {
		const messages = Object.values(data.errors).flat().filter(Boolean)
		if (messages.length > 0) return messages.join(' ')
	}

	if (error?.request) {
		return 'Unable to reach the TaskFlow API. Check that the backend is running.'
	}

	return fallback
}

function formatDueDate(value) {
	if (!value) return 'No due date'

	const date = new Date(value)
	if (Number.isNaN(date.getTime())) return 'No due date'

	return new Intl.DateTimeFormat('en', {
		day: 'numeric',
		month: 'short',
		year: 'numeric',
	}).format(date)
}

function toInputDate(value) {
	if (!value) return ''
	const date = new Date(value)
	if (Number.isNaN(date.getTime())) return ''
	return date.toISOString().slice(0, 10)
}

function statusLabel(status) {
	if (typeof status === 'number') {
		return statusOptions.find((option) => option.value === status)?.label ?? 'Unknown'
	}
	return status ?? 'Unknown'
}

function priorityLabel(priority) {
	if (typeof priority === 'number') {
		return priorityOptions.find((option) => option.value === priority)?.label ?? 'Unknown'
	}
	return priority ?? 'Unknown'
}

export default function Tasks() {
	const [projects, setProjects] = useState([])
	const [selectedProjectId, setSelectedProjectId] = useState(null)
	const [tasks, setTasks] = useState([])
	const [loading, setLoading] = useState(true)
	const [mutating, setMutating] = useState(false)
	const [error, setError] = useState('')
	const [formError, setFormError] = useState('')
	const [formOpen, setFormOpen] = useState(false)
	const [editingTask, setEditingTask] = useState(null)
	const [form, setForm] = useState(emptyForm)
	const [openMenuId, setOpenMenuId] = useState(null)

	const selectedProject = useMemo(
		() => projects.find((project) => project.id === selectedProjectId) ?? null,
		[projects, selectedProjectId],
	)

	useEffect(() => {
		let ignore = false

		const loadProjects = async () => {
			setLoading(true)
			setError('')

			try {
				const response = await api.get('/projects')
				if (!ignore) {
					const ownedProjects = Array.isArray(response.data) ? response.data : []
					setProjects(ownedProjects)
					setSelectedProjectId((current) => {
						if (current && ownedProjects.some((project) => project.id === current)) return current
						return ownedProjects[0]?.id ?? null
					})
				}
			} catch (requestError) {
				if (!ignore) {
					setError(getApiError(requestError, 'Could not load projects.'))
					setProjects([])
					setSelectedProjectId(null)
				}
			} finally {
				if (!ignore) setLoading(false)
			}
		}

		loadProjects()
		return () => {
			ignore = true
		}
	}, [])

	useEffect(() => {
		let ignore = false

		if (!selectedProjectId) {
			return () => {
				ignore = true
			}
		}

		const loadTasks = async () => {
			setLoading(true)
			setError('')

			try {
				const response = await api.get(`/projects/${selectedProjectId}/tasks`)
				if (!ignore) {
					setTasks(Array.isArray(response.data) ? response.data : [])
				}
			} catch (requestError) {
				if (!ignore) {
					setError(getApiError(requestError, 'Could not load tasks.'))
					setTasks([])
				}
			} finally {
				if (!ignore) setLoading(false)
			}
		}

		loadTasks()
		return () => {
			ignore = true
		}
	}, [selectedProjectId])

	const fetchTasks = async () => {
		if (!selectedProjectId) return

		setLoading(true)
		setError('')

		try {
			const response = await api.get(`/projects/${selectedProjectId}/tasks`)
			setTasks(Array.isArray(response.data) ? response.data : [])
		} catch (requestError) {
			setError(getApiError(requestError, 'Could not load tasks.'))
			setTasks([])
		} finally {
			setLoading(false)
		}
	}

	const openCreate = () => {
		if (!selectedProjectId) return
		setEditingTask(null)
		setForm(emptyForm)
		setFormError('')
		setFormOpen(true)
	}

	const openEdit = (task) => {
		setEditingTask(task)
		setForm({
			title: task.title ?? '',
			description: task.description ?? '',
			status: typeof task.status === 'number' ? task.status : statusOptions.find((option) => option.label === task.status)?.value ?? 0,
			priority: typeof task.priority === 'number' ? task.priority : priorityOptions.find((option) => option.label === task.priority)?.value ?? 1,
			dueDate: toInputDate(task.dueDate),
			assigneeId: task.assigneeId?.toString() ?? '',
		})
		setFormError('')
		setOpenMenuId(null)
		setFormOpen(true)
	}

	const closeForm = () => {
		if (mutating) return
		setFormOpen(false)
		setEditingTask(null)
		setForm(emptyForm)
		setFormError('')
	}

	const handleSubmit = async (event) => {
		event.preventDefault()
		if (mutating || !selectedProjectId) return

		setFormError('')

		if (!form.title.trim()) {
			setFormError('Task title is required.')
			return
		}

		if (form.title.trim().length > 150) {
			setFormError('Task title cannot exceed 150 characters.')
			return
		}

		if ((form.description ?? '').trim().length > 2000) {
			setFormError('Task description cannot exceed 2000 characters.')
			return
		}

		const assigneeId = form.assigneeId.trim() ? Number(form.assigneeId) : null
		if (assigneeId !== null && (!Number.isInteger(assigneeId) || assigneeId <= 0)) {
			setFormError('Assignee ID must be a positive integer.')
			return
		}

		const payload = {
			title: form.title.trim(),
			description: form.description.trim() || null,
			status: Number(form.status),
			priority: Number(form.priority),
			dueDate: form.dueDate ? new Date(`${form.dueDate}T00:00:00Z`).toISOString() : null,
			assigneeId,
		}

		setMutating(true)

		try {
			if (editingTask) {
				await api.put(`/projects/${selectedProjectId}/tasks/${editingTask.id}`, payload)
			} else {
				await api.post(`/projects/${selectedProjectId}/tasks`, payload)
			}

			setFormOpen(false)
			setEditingTask(null)
			setForm(emptyForm)
			setFormError('')
			await fetchTasks()
		} catch (requestError) {
			setFormError(getApiError(requestError, 'Could not save the task.'))
		} finally {
			setMutating(false)
		}
	}

	const handleDelete = async (task) => {
		if (mutating || !selectedProjectId) return

		const confirmed = window.confirm(`Delete "${task.title}"?`)
		if (!confirmed) return

		setMutating(true)
		setError('')

		try {
			await api.delete(`/projects/${selectedProjectId}/tasks/${task.id}`)
			setOpenMenuId(null)
			await fetchTasks()
		} catch (requestError) {
			setError(getApiError(requestError, 'Could not delete the task.'))
		} finally {
			setMutating(false)
		}
	}

	return (
		<div className="page">
			<section className="page-heading-row">
				<div>
					<p className="eyebrow">Assignments</p>
					<h2>My Tasks</h2>
				</div>
				<div className="heading-actions">
					<label className="task-project-select">
						<span>Project</span>
						<select value={selectedProjectId ?? ''} onChange={(event) => setSelectedProjectId(Number(event.target.value))} disabled={projects.length === 0}>
							{projects.length === 0 && <option value="">No projects</option>}
							{projects.map((project) => (
								<option key={project.id} value={project.id}>{project.name}</option>
							))}
						</select>
					</label>
					<button className="secondary-button" type="button" disabled><Filter size={17} /> Filter</button>
					<button className="primary-button" type="button" onClick={openCreate} disabled={!selectedProjectId}><Plus size={18} /> Add Task</button>
				</div>
			</section>

			{error && <div className="api-state api-error" role="alert">{error}</div>}

			{loading ? (
				<div className="api-state">Loading tasks...</div>
			) : !selectedProject ? (
				<div className="api-state api-empty">
					<h3>No projects available</h3>
					<p>Create a project before adding tasks.</p>
				</div>
			) : tasks.length === 0 ? (
				<div className="api-state api-empty">
					<h3>No tasks in {selectedProject.name}</h3>
					<p>Add a task to start tracking work.</p>
					<button className="primary-button" type="button" onClick={openCreate}><Plus size={18} /> Add Task</button>
				</div>
			) : (
				<section className="table-card">
					<div className="task-table table-header" role="row">
						<span>Task</span><span>Project</span><span>Priority</span><span>Status</span><span>Due date</span><span>Assignee</span><span />
					</div>
					{tasks.map((task) => {
						const status = statusLabel(task.status)
						const priority = priorityLabel(task.priority)
						const assigneeName = task.assignee?.username ?? (task.assigneeId ? `User ${task.assigneeId}` : 'Unassigned')

						return (
							<div className="task-table task-row-table" role="row" key={task.id}>
								<div className="table-task-name">
									<button className="task-check" type="button" aria-label={`Mark ${task.title} complete`} />
									<strong>{task.title}</strong>
								</div>
								<span className="muted-text">{selectedProject.name}</span>
								<span><span className={`priority-badge priority-${priority.toLowerCase()}`}>{priority}</span></span>
								<span><span className={`status-badge status-${status.toLowerCase().replaceAll(' ', '-')}`}>{status}</span></span>
								<span className="muted-text"><span className="task-due"><CalendarDays size={14} /> {formatDueDate(task.dueDate)}</span></span>
								<span className="table-avatar" title={assigneeName}>{task.assignee?.username?.slice(0, 2).toUpperCase() ?? (task.assigneeId ? `U${task.assigneeId}` : '—')}</span>
								<div className="task-menu-wrap">
									<button className="icon-button plain" type="button" aria-label={`More actions for ${task.title}`} onClick={() => setOpenMenuId(openMenuId === task.id ? null : task.id)}><MoreHorizontal size={18} /></button>
									{openMenuId === task.id && (
										<div className="task-menu">
											<button type="button" onClick={() => openEdit(task)}><Pencil size={15} /> Edit</button>
											<button className="delete-action" type="button" onClick={() => handleDelete(task)}><Trash2 size={15} /> Delete</button>
										</div>
									)}
								</div>
							</div>
						)
					})}
				</section>
			)}

			{formOpen && (
				<div className="modal-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && closeForm()}>
					<section className="project-modal" role="dialog" aria-modal="true" aria-labelledby="task-form-title">
						<div className="modal-header">
							<div>
								<p className="eyebrow">Task details</p>
								<h3 id="task-form-title">{editingTask ? 'Edit task' : 'Create task'}</h3>
							</div>
							<button className="icon-button plain" type="button" onClick={closeForm} aria-label="Close task form"><X size={20} /></button>
						</div>

						<form className="project-form" onSubmit={handleSubmit}>
							{formError && <div className="api-state api-error" role="alert">{formError}</div>}
							<label>
								<span>Title</span>
								<input type="text" value={form.title} onChange={(event) => setForm((current) => ({ ...current, title: event.target.value }))} maxLength={151} required />
							</label>
							<label>
								<span>Description</span>
								<textarea value={form.description} onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))} maxLength={2001} rows={4} />
							</label>
							<div className="task-form-grid">
								<label>
									<span>Status</span>
									<select value={form.status} onChange={(event) => setForm((current) => ({ ...current, status: Number(event.target.value) }))}>
										{statusOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
									</select>
								</label>
								<label>
									<span>Priority</span>
									<select value={form.priority} onChange={(event) => setForm((current) => ({ ...current, priority: Number(event.target.value) }))}>
										{priorityOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
									</select>
								</label>
							</div>
							<div className="task-form-grid">
								<label>
									<span>Due date</span>
									<input type="date" value={form.dueDate} onChange={(event) => setForm((current) => ({ ...current, dueDate: event.target.value }))} />
								</label>
								<label>
									<span>Assignee ID</span>
									<input type="number" min="1" value={form.assigneeId} onChange={(event) => setForm((current) => ({ ...current, assigneeId: event.target.value }))} placeholder="Optional" />
								</label>
							</div>
							<div className="modal-actions">
								<button className="secondary-button" type="button" onClick={closeForm} disabled={mutating}>Cancel</button>
								<button className="primary-button" type="submit" disabled={mutating}>{mutating ? 'Saving...' : editingTask ? 'Save changes' : 'Create task'}</button>
							</div>
						</form>
					</section>
				</div>
			)}
		</div>
	)
}
