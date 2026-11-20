import { randomUUID } from 'node:crypto'
import { expect } from '@playwright/test'

export async function registerAccount(page) {
  const uniqueId = randomUUID().replaceAll('-', '')
  const account = {
    username: `e2e-${uniqueId.slice(0, 12)}`,
    email: `taskflow-${uniqueId}@example.test`,
    password: `E2e-test-${uniqueId}!`,
  }

  await page.goto('/register')
  await page.getByLabel('Username').fill(account.username)
  await page.getByLabel('Email', { exact: true }).fill(account.email)
  await page.getByLabel('Password', { exact: true }).fill(account.password)
  await page.getByLabel('Confirm password').fill(account.password)
  await page.getByRole('button', { name: 'Create account', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Dashboard', level: 1 })).toBeVisible()

  return account
}

export async function createProject(page, projectName, description = 'E2E project description') {
  await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: 'Projects' }).click()
  await expect(page.getByRole('heading', { name: 'Projects', level: 2 })).toBeVisible()
  await page.getByRole('button', { name: 'Create Project', exact: true }).first().click()
  const dialog = page.getByRole('dialog', { name: 'Create project' })
  await dialog.getByLabel('Name').fill(projectName)
  await dialog.getByLabel('Description').fill(description)
  await dialog.getByRole('button', { name: 'Create project', exact: true }).click()
  await expect(page.getByRole('status').getByText('Project created.')).toBeVisible()
  await expect(page.getByRole('heading', { name: projectName, level: 3 })).toBeVisible()
}

export function dateOffset(days) {
  const date = new Date()
  date.setDate(date.getDate() + days)
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export async function currentUserId(page) {
  const token = await page.evaluate(() => window.localStorage.getItem('taskflow_token'))
  expect(token).toBeTruthy()

  const response = await page.request.get(`${process.env.TASKFLOW_E2E_API_URL}/api/auth/me`, {
    headers: { Authorization: `Bearer ${token}` },
  })
  expect(response.ok()).toBeTruthy()
  const user = await response.json()
  return user.id
}
