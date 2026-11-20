import { test, expect } from '@playwright/test'
import { createProject, currentUserId, dateOffset, registerAccount } from './helpers.js'

test('registers an isolated account and keeps its session after a refresh', async ({ page }) => {
  await registerAccount(page)

  await expect(page.getByText('No projects yet')).toBeVisible()
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Dashboard', level: 1 })).toBeVisible()
  await expect(page.getByText('No projects yet')).toBeVisible()
})

test('logs out and redirects unauthenticated users away from protected routes', async ({ page }) => {
  await registerAccount(page)

  await page.getByRole('button', { name: 'Logout' }).click()
  await expect(page).toHaveURL(/\/login$/)

  await page.goto('/tasks')
  await expect(page).toHaveURL(/\/login$/)
  await expect(page.getByRole('heading', { name: 'Sign in to TaskFlow' })).toBeVisible()
})

test('logs in with the registered account credentials', async ({ page }) => {
  const account = await registerAccount(page)
  await page.getByRole('button', { name: 'Logout' }).click()

  await page.getByLabel('Email or username').fill(account.username)
  await page.getByLabel('Password', { exact: true }).fill(account.password)
  await page.getByRole('button', { name: 'Sign in' }).click()

  await expect(page.getByRole('heading', { name: 'Dashboard', level: 1 })).toBeVisible()
  await expect(page.getByText('No projects yet')).toBeVisible()
})

test('an invalid stored session returns the user to login', async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem('taskflow_token', 'not-a-valid-jwt'))
  await page.goto('/projects')

  await expect(page).toHaveURL(/\/login$/, { timeout: 15_000 })
  await expect(page.getByRole('heading', { name: 'Sign in to TaskFlow' })).toBeVisible()
  await expect.poll(() => page.evaluate(() => window.localStorage.getItem('taskflow_token'))).toBeNull()
})

test('creates, edits, clears and deletes a project with changes surviving refresh', async ({ page }) => {
  await registerAccount(page)
  await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: 'Projects' }).click()
  await page.getByRole('button', { name: 'Create Project', exact: true }).first().click()

  const originalName = `Project ${Date.now()}`
  const editedName = `${originalName} updated`
  const createDialog = page.getByRole('dialog', { name: 'Create project' })
  await createDialog.getByLabel('Name').fill(originalName)
  await createDialog.getByLabel('Description').fill('Description to be cleared')
  await createDialog.getByLabel('Due date').fill(dateOffset(20))
  await createDialog.getByRole('button', { name: 'Create project', exact: true }).click()
  await expect(page.getByRole('heading', { name: originalName, level: 3 })).toBeVisible()

  await page.getByRole('button', { name: `More actions for ${originalName}` }).click()
  await page.getByRole('button', { name: 'Edit', exact: true }).click()
  const editDialog = page.getByRole('dialog', { name: 'Edit project' })
  await editDialog.getByLabel('Name').fill(editedName)
  await editDialog.getByLabel('Description').fill('')
  await editDialog.getByLabel('Due date').fill('')
  await editDialog.getByRole('button', { name: 'Save changes' }).click()

  await expect(page.getByRole('heading', { name: editedName, level: 3 })).toBeVisible()
  await expect(page.getByText('No description provided.')).toBeVisible()
  await expect(page.getByText('No due date')).toBeVisible()
  await page.getByRole('searchbox', { name: 'Search projects' }).fill(editedName)
  await expect(page.getByRole('heading', { name: editedName, level: 3 })).toBeVisible()
  await page.getByRole('searchbox', { name: 'Search projects' }).fill('no matching project')
  await expect(page.getByText('No matching projects')).toBeVisible()
  await page.getByRole('searchbox', { name: 'Search projects' }).fill('')

  await page.reload()
  await expect(page.getByRole('heading', { name: editedName, level: 3 })).toBeVisible()
  await expect(page.getByText('No description provided.')).toBeVisible()
  await expect(page.getByText('No due date')).toBeVisible()

  await page.getByRole('button', { name: `More actions for ${editedName}` }).click()
  page.once('dialog', (dialog) => dialog.accept())
  await page.getByRole('button', { name: 'Delete', exact: true }).click()
  await expect(page.getByText('No projects yet')).toBeVisible()
})

test('creates, edits, assigns, clears, completes and deletes a task', async ({ page }) => {
  await registerAccount(page)
  const projectName = `Task project ${Date.now()}`
  await createProject(page, projectName)
  const userId = await currentUserId(page)

  await page.getByRole('link', { name: 'My Tasks' }).click()
  await page.getByRole('button', { name: 'Add Task', exact: true }).first().click()

  const originalTitle = `Task ${Date.now()}`
  const updatedTitle = `${originalTitle} updated`
  const createDialog = page.getByRole('dialog', { name: 'Create task' })
  await createDialog.getByLabel('Title').fill(originalTitle)
  await createDialog.getByLabel('Description').fill('Task description to be cleared')
  await createDialog.getByLabel('Status').selectOption({ label: 'To Do' })
  await createDialog.getByLabel('Priority').selectOption({ label: 'High' })
  await createDialog.getByLabel('Due date').fill(dateOffset(14))
  await createDialog.getByLabel('Assignee ID').fill(String(userId))
  await createDialog.getByRole('button', { name: 'Create task', exact: true }).click()

  const taskRow = page.getByRole('row').filter({ hasText: originalTitle })
  await expect(taskRow).toBeVisible()
  await expect(taskRow.getByText('High')).toBeVisible()
  await expect(taskRow.getByText('To Do')).toBeVisible()
  await expect(taskRow.getByText(/^e2e-/)).toBeVisible()

  await taskRow.getByRole('button', { name: `More actions for ${originalTitle}` }).click()
  await page.getByRole('button', { name: 'Edit', exact: true }).click()
  const editDialog = page.getByRole('dialog', { name: 'Edit task' })
  await editDialog.getByLabel('Title').fill(updatedTitle)
  await editDialog.getByLabel('Description').fill('')
  await editDialog.getByLabel('Status').selectOption({ label: 'Review' })
  await editDialog.getByLabel('Priority').selectOption({ label: 'Urgent' })
  await editDialog.getByLabel('Due date').fill('')
  await editDialog.getByLabel('Assignee ID').fill('')
  await editDialog.getByRole('button', { name: 'Save changes' }).click()

  const updatedRow = page.getByRole('row').filter({ hasText: updatedTitle })
  await expect(updatedRow).toBeVisible()
  await expect(updatedRow.getByText('Urgent')).toBeVisible()
  await expect(updatedRow.getByText('Review')).toBeVisible()
  await expect(updatedRow.getByText('Unassigned')).toBeVisible()
  await expect(updatedRow.getByText('No due date')).toBeVisible()

  await page.reload()
  const persistedRow = page.getByRole('row').filter({ hasText: updatedTitle })
  await expect(persistedRow.getByText('Urgent')).toBeVisible()
  await expect(persistedRow.getByText('Review')).toBeVisible()
  await expect(persistedRow.getByText('Unassigned')).toBeVisible()
  await expect(persistedRow.getByText('No due date')).toBeVisible()
  await persistedRow.getByRole('button', { name: `More actions for ${updatedTitle}` }).click()
  await page.getByRole('button', { name: 'Edit', exact: true }).click()
  const persistedEditDialog = page.getByRole('dialog', { name: 'Edit task' })
  await expect(persistedEditDialog.getByLabel('Description')).toHaveValue('')
  await persistedEditDialog.getByRole('button', { name: 'Cancel' }).click()

  await page.getByRole('button', { name: `Mark ${updatedTitle} complete` }).click()
  await expect(persistedRow.getByText('Done')).toBeVisible()
  await page.reload()
  const completedRow = page.getByRole('row').filter({ hasText: updatedTitle })
  await expect(completedRow.getByText('Done')).toBeVisible()

  await completedRow.getByRole('button', { name: `More actions for ${updatedTitle}` }).click()
  page.once('dialog', (dialog) => dialog.accept())
  await page.getByRole('button', { name: 'Delete', exact: true }).click()
  await expect(page.getByText(`No tasks in ${projectName}`)).toBeVisible()
})

test('dashboard shows API-backed project and task statistics, recent tasks and deadlines', async ({ page }) => {
  await registerAccount(page)
  const projectName = `Dashboard project ${Date.now()}`
  await createProject(page, projectName)
  const dueDate = dateOffset(12)

  await page.getByRole('link', { name: 'My Tasks' }).click()
  await page.getByRole('button', { name: 'Add Task', exact: true }).first().click()
  const upcomingTitle = `Upcoming ${Date.now()}`
  const firstDialog = page.getByRole('dialog', { name: 'Create task' })
  await firstDialog.getByLabel('Title').fill(upcomingTitle)
  await firstDialog.getByLabel('Status').selectOption({ label: 'To Do' })
  await firstDialog.getByLabel('Priority').selectOption({ label: 'High' })
  await firstDialog.getByLabel('Due date').fill(dueDate)
  await firstDialog.getByRole('button', { name: 'Create task', exact: true }).click()

  await page.getByRole('button', { name: 'Add Task', exact: true }).first().click()
  const completedTitle = `Completed ${Date.now()}`
  const secondDialog = page.getByRole('dialog', { name: 'Create task' })
  await secondDialog.getByLabel('Title').fill(completedTitle)
  await secondDialog.getByRole('button', { name: 'Create task', exact: true }).click()
  await page.getByRole('button', { name: `Mark ${completedTitle} complete` }).click()

  await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: 'Dashboard' }).click()
  await expect(page.locator('.stat-card').filter({ hasText: 'Total Projects' }).locator('strong')).toHaveText('1')
  await expect(page.locator('.stat-card').filter({ hasText: 'Total Tasks' }).locator('strong')).toHaveText('2')
  await expect(page.locator('.stat-card').filter({ hasText: 'Completed Tasks' }).locator('strong')).toHaveText('1')
  await expect(page.locator('.stat-card').filter({ hasText: 'Incomplete Tasks' }).locator('strong')).toHaveText('1')
  await expect(page.getByRole('heading', { name: 'Recent Tasks' })).toBeVisible()
  await expect(page.locator('.recent-tasks').getByText(upcomingTitle)).toBeVisible()
  await expect(page.locator('.recent-tasks').getByText(completedTitle)).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Upcoming Deadlines' })).toBeVisible()
  await expect(page.locator('.upcoming-deadlines').getByText(upcomingTitle)).toBeVisible()
  await expect(page.locator('.upcoming-deadlines').getByText(completedTitle)).toHaveCount(0)
  await expect(page.getByRole('heading', { name: 'Tasks by Status' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Tasks by Priority' })).toBeVisible()
  const statusSummary = page.locator('.dashboard-detail-grid .panel').nth(0)
  const prioritySummary = page.locator('.dashboard-detail-grid .panel').nth(1)
  await expect(statusSummary.locator('.summary-row').filter({ hasText: 'To Do' }).locator('strong')).toHaveText('1')
  await expect(statusSummary.locator('.summary-row').filter({ hasText: 'Done' }).locator('strong')).toHaveText('1')
  await expect(prioritySummary.locator('.summary-row').filter({ hasText: 'High' }).locator('strong')).toHaveText('1')
  await expect(prioritySummary.locator('.summary-row').filter({ hasText: 'Medium' }).locator('strong')).toHaveText('1')
})

test('dashboard and projects show empty states for a new account', async ({ page }) => {
  await registerAccount(page)
  await expect(page.getByText('No projects yet')).toBeVisible()
  await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: 'Projects' }).click()
  await expect(page.getByText('Create your first project to start organizing work.')).toBeVisible()
  await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: 'Dashboard' }).click()
  await expect(page.getByText('No projects yet')).toBeVisible()
})

test('dashboard exposes loading and recoverable API error states', async ({ page }) => {
  await registerAccount(page)

  let releaseProjectsRequest
  const projectsRequestGate = new Promise((resolve) => { releaseProjectsRequest = resolve })
  await page.route('**/api/projects', async (route) => {
    await projectsRequestGate
    await route.continue()
  })
  await page.reload()
  await expect(page.getByText('Loading dashboard...')).toBeVisible()
  releaseProjectsRequest()
  await expect(page.getByText('No projects yet')).toBeVisible()
  await page.unroute('**/api/projects')

  await page.route('**/api/projects', (route) => route.fulfill({
    status: 503,
    contentType: 'application/problem+json',
    body: JSON.stringify({ title: 'E2E service error', status: 503 }),
  }))
  await page.reload()
  await expect(page.getByText('E2E service error')).toBeVisible()
  await page.unroute('**/api/projects')

  await page.getByRole('button', { name: 'Try again' }).click()
  await expect(page.getByText('No projects yet')).toBeVisible()
})

test('navigation and logout remain usable at desktop, tablet and mobile sizes', async ({ page }) => {
  await registerAccount(page)
  const primaryNavigation = page.getByRole('navigation', { name: 'Primary navigation' })

  for (const viewport of [{ width: 1280, height: 900 }, { width: 768, height: 900 }, { width: 390, height: 844 }]) {
    await page.setViewportSize(viewport)
    const projectsLink = primaryNavigation.getByRole('link', { name: 'Projects' })
    await expect(projectsLink).toBeVisible()
    await projectsLink.click()
    await expect(projectsLink).toHaveAttribute('aria-current', 'page')
    await expect(page.getByRole('heading', { name: 'Projects', level: 2 })).toBeVisible()

    const tasksLink = page.getByRole('link', { name: 'My Tasks' })
    await tasksLink.click()
    await expect(tasksLink).toHaveAttribute('aria-current', 'page')
    await expect(page.getByRole('heading', { name: 'My Tasks', level: 2 })).toBeVisible()
  }

  await expect(page.getByRole('button', { name: 'Logout' })).toBeVisible()
  await page.getByRole('button', { name: 'Logout' }).click()
  await expect(page).toHaveURL(/\/login$/)
})
