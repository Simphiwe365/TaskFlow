import { useEffect, useMemo, useState } from 'react'
import { Search, Plus, CalendarDays, MoreHorizontal, Pencil, Trash2, X } from 'lucide-react'
import api from '../services/api'
import useModalAccessibility from '../hooks/useModalAccessibility'
import { formatDate, toDateInputValue } from '../utils/dateUtils'

const emptyForm = {
  name: '',
  description: '',
  dueDate: '',
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

export default function Projects() {
  const [projects, setProjects] = useState([])
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [mutating, setMutating] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [formError, setFormError] = useState('')
  const [formOpen, setFormOpen] = useState(false)
  const [editingProject, setEditingProject] = useState(null)
  const [form, setForm] = useState(emptyForm)
  const [openMenuId, setOpenMenuId] = useState(null)

  useEffect(() => {
    let ignore = false

    const loadInitialProjects = async () => {
      setLoading(true)
      setError('')

      try {
        const response = await api.get('/projects')
        if (!ignore) {
          setProjects(Array.isArray(response.data) ? response.data : [])
        }
      } catch (requestError) {
        if (!ignore) {
          setError(getApiError(requestError, 'Could not load projects.'))
        }
      } finally {
        if (!ignore) {
          setLoading(false)
        }
      }
    }

    loadInitialProjects()

    return () => {
      ignore = true
    }
  }, [])

  const fetchProjects = async () => {
    setLoading(true)
    setError('')

    try {
      const response = await api.get('/projects')
      setProjects(Array.isArray(response.data) ? response.data : [])
    } catch (requestError) {
      setError(getApiError(requestError, 'Could not load projects.'))
    } finally {
      setLoading(false)
    }
  }

  const filteredProjects = useMemo(() => {
    const term = search.trim().toLowerCase()
    if (!term) return projects

    return projects.filter((project) =>
      `${project.name ?? ''} ${project.description ?? ''}`.toLowerCase().includes(term),
    )
  }, [projects, search])

  const openCreate = () => {
    setEditingProject(null)
    setForm(emptyForm)
    setFormError('')
    setSuccess('')
    setFormOpen(true)
  }

  const openEdit = (project) => {
    setEditingProject(project)
    setForm({
      name: project.name ?? '',
      description: project.description ?? '',
      dueDate: toDateInputValue(project.dueDate),
    })
    setFormError('')
    setSuccess('')
    setOpenMenuId(null)
    setFormOpen(true)
  }

  const closeForm = () => {
    if (mutating) return
    setFormOpen(false)
    setEditingProject(null)
    setForm(emptyForm)
    setFormError('')
    setSuccess('')
  }

  const dialogRef = useModalAccessibility(formOpen, closeForm)

  const handleSubmit = async (event) => {
    event.preventDefault()
    if (mutating) return

    setFormError('')
    setSuccess('')

    if (!form.name.trim()) {
      setFormError('Project name is required.')
      return
    }

    if (form.name.trim().length > 100) {
      setFormError('Project name cannot exceed 100 characters.')
      return
    }

    if ((form.description ?? '').trim().length > 1000) {
      setFormError('Project description cannot exceed 1000 characters.')
      return
    }

    const payload = {
      name: form.name.trim(),
      description: form.description.trim() || null,
      dueDate: form.dueDate ? new Date(`${form.dueDate}T00:00:00Z`).toISOString() : null,
    }

    setMutating(true)

    try {
      if (editingProject) {
        await api.put(`/projects/${editingProject.id}`, payload)
      } else {
        await api.post('/projects', payload)
      }

      setFormOpen(false)
      setEditingProject(null)
      setForm(emptyForm)
      setFormError('')
      await fetchProjects()
      setSuccess(editingProject ? 'Project updated.' : 'Project created.')
    } catch (requestError) {
      setFormError(getApiError(requestError, 'Could not save the project.'))
    } finally {
      setMutating(false)
    }
  }

  const handleDelete = async (project) => {
    if (mutating) return

    const confirmed = window.confirm(`Delete "${project.name}"?`)
    if (!confirmed) return

    setMutating(true)
    setError('')
    setSuccess('')

    try {
      await api.delete(`/projects/${project.id}`)
      setOpenMenuId(null)
      await fetchProjects()
      setSuccess('Project deleted.')
    } catch (requestError) {
      setError(getApiError(requestError, 'Could not delete the project.'))
    } finally {
      setMutating(false)
    }
  }

  return (
    <div className="page">
      <section className="page-heading-row">
        <div>
          <p className="eyebrow">Workspace</p>
          <h2>Projects</h2>
        </div>
        <div className="heading-actions">
          <label className="inline-search">
            <Search size={18} aria-hidden="true" />
            <input
              type="search"
              aria-label="Search projects"
              placeholder="Search projects"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </label>
          <button className="primary-button" type="button" onClick={openCreate}><Plus size={18} /> Create Project</button>
        </div>
      </section>

      {error && <div className="api-state api-error" role="alert">{error}</div>}
      {success && <div className="api-state api-success" role="status">{success}</div>}

      {loading ? (
        <div className="api-state" role="status" aria-live="polite">Loading projects...</div>
      ) : filteredProjects.length === 0 ? (
        <div className="api-state api-empty">
          <h3>{search ? 'No matching projects' : 'No projects yet'}</h3>
          <p>{search ? 'Try a different search term.' : 'Create your first project to start organizing work.'}</p>
          {!search && <button className="primary-button" type="button" onClick={openCreate}><Plus size={18} /> Create Project</button>}
        </div>
      ) : (
        <section className="project-card-grid">
          {filteredProjects.map((project) => {
            const tone = ['blue', 'violet', 'green', 'amber'][project.id % 4]

            return (
              <article className="project-card" key={project.id}>
                <div className="project-card-top">
                  <div className={`project-avatar large ${tone}`}>{project.name?.charAt(0)?.toUpperCase() ?? 'P'}</div>
                  <div className="project-menu-wrap">
                    <button
                      className="icon-button plain"
                      type="button"
                      aria-label={`More actions for ${project.name}`}
                      aria-expanded={openMenuId === project.id}
                      aria-controls={`project-menu-${project.id}`}
                      onClick={() => setOpenMenuId(openMenuId === project.id ? null : project.id)}
                    >
                      <MoreHorizontal size={20} />
                    </button>
                    {openMenuId === project.id && (
                      <div className="project-menu" id={`project-menu-${project.id}`}>
                        <button type="button" onClick={() => openEdit(project)}><Pencil size={15} /> Edit</button>
                        <button className="delete-action" type="button" onClick={() => handleDelete(project)}><Trash2 size={15} /> Delete</button>
                      </div>
                    )}
                  </div>
                </div>
                <h3>{project.name}</h3>
                <p className="project-description">{project.description || 'No description provided.'}</p>
                <div className="project-progress-heading">
                  <span>Tasks</span>
                  <strong>{project.taskCount ?? 0}</strong>
                </div>
                <div className="project-card-footer">
                  <div className="due-date"><CalendarDays size={15} aria-hidden="true" /> {formatDate(project.dueDate) ?? 'No due date'}</div>
                </div>
              </article>
            )
          })}
        </section>
      )}

      {formOpen && (
        <div className="modal-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && closeForm()}>
          <section
            className="project-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="project-form-title"
            aria-describedby={formError ? 'project-form-error' : undefined}
            ref={dialogRef}
            tabIndex={-1}
          >
            <div className="modal-header">
              <div>
                <p className="eyebrow">Project details</p>
                <h3 id="project-form-title">{editingProject ? 'Edit project' : 'Create project'}</h3>
              </div>
              <button className="icon-button plain" type="button" onClick={closeForm} aria-label="Close project form"><X size={20} /></button>
            </div>

            <form className="project-form" onSubmit={handleSubmit}>
              {formError && <div id="project-form-error" className="api-state api-error" role="alert">{formError}</div>}
              <label>
                <span>Name</span>
                <input type="text" value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} maxLength={101} required />
              </label>
              <label>
                <span>Description</span>
                <textarea value={form.description} onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))} maxLength={1001} rows={4} />
              </label>
              <label>
                <span>Due date</span>
                <input type="date" value={form.dueDate} onChange={(event) => setForm((current) => ({ ...current, dueDate: event.target.value }))} />
              </label>
              <div className="modal-actions">
                <button className="secondary-button" type="button" onClick={closeForm} disabled={mutating}>Cancel</button>
                <button className="primary-button" type="submit" disabled={mutating}>{mutating ? 'Saving...' : editingProject ? 'Save changes' : 'Create project'}</button>
              </div>
            </form>
          </section>
        </div>
      )}
    </div>
  )
}
