import { spawn } from 'node:child_process'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, resolve, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { setTimeout as delay } from 'node:timers/promises'
import { randomBytes } from 'node:crypto'
import { createServer } from 'node:net'

const clientDirectory = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const repositoryDirectory = resolve(clientDirectory, '..')
const apiProject = resolve(repositoryDirectory, 'TaskFlow.API/TaskFlow.API.csproj')
const playwrightCli = resolve(clientDirectory, 'node_modules/@playwright/test/cli.js')
const apiUrl = normalizeBaseUrl(process.env.TASKFLOW_E2E_API_URL ?? 'http://localhost:5258', 'API')
const clientUrl = normalizeBaseUrl(process.env.TASKFLOW_E2E_CLIENT_URL ?? 'http://localhost:5173', 'client')
const databaseDirectory = await mkdtemp(join(tmpdir(), 'taskflow-playwright-'))
const databasePath = join(databaseDirectory, 'taskflow.e2e.db')
const clientOrigin = new URL(clientUrl).origin

const testEnvironment = {
  ...process.env,
  ASPNETCORE_ENVIRONMENT: 'Testing',
  DOTNET_ENVIRONMENT: 'Testing',
  ASPNETCORE_URLS: apiUrl,
  ConnectionStrings__DefaultConnection: `Data Source=${databasePath}`,
  JwtSettings__Key: randomBytes(64).toString('base64'),
  JwtSettings__Issuer: 'TaskFlow.Playwright.Tests',
  JwtSettings__Audience: 'TaskFlow.Playwright.Browser',
  JwtSettings__ExpiryMinutes: '30',
  Logging__LogLevel__Default: 'Warning',
  Cors__AllowedOrigins__0: clientOrigin,
  TASKFLOW_E2E_API_URL: apiUrl,
  TASKFLOW_E2E_CLIENT_URL: clientUrl,
  VITE_API_BASE_URL: `${apiUrl}/api`,
}

const childProcesses = []

function normalizeBaseUrl(value, label) {
  const parsed = new URL(value)
  if (!['http:', 'https:'].includes(parsed.protocol) || parsed.pathname !== '/' || parsed.search || parsed.hash) {
    throw new Error(`${label} URL must be an HTTP origin without a path, query, or fragment: ${value}`)
  }
  return parsed.origin
}

async function assertPortAvailable(label, value) {
  const parsed = new URL(value)
  const port = Number(parsed.port || (parsed.protocol === 'https:' ? 443 : 80))

  await new Promise((resolvePromise, rejectPromise) => {
    const server = createServer()
    server.once('error', (error) => {
      if (error.code === 'EADDRINUSE') {
        rejectPromise(new Error(`${label} port ${port} is already in use. Set its TASKFLOW_E2E_*_URL to an unused port.`))
        return
      }

      rejectPromise(error)
    })
    server.listen(port, parsed.hostname, () => {
      server.close((error) => error ? rejectPromise(error) : resolvePromise())
    })
  })
}

function startProcess(label, command, args, options) {
  const child = spawn(command, args, {
    cwd: options.cwd,
    env: options.env,
    stdio: ['ignore', 'pipe', 'pipe'],
  })

  child.stdout.on('data', (data) => process.stdout.write(`[${label}] ${data}`))
  child.stderr.on('data', (data) => process.stderr.write(`[${label}] ${data}`))
  childProcesses.push(child)
  return child
}

async function runProcess(label, command, args, options) {
  return new Promise((resolvePromise, rejectPromise) => {
    const child = startProcess(label, command, args, options)
    child.once('error', rejectPromise)
    child.once('close', (code, signal) => {
      if (code === 0) resolvePromise(0)
      else rejectPromise(new Error(`${label} exited with ${signal ? `signal ${signal}` : `code ${code}`}`))
    })
  })
}

async function waitForUrl(label, url, child, timeoutMs = 120_000) {
  const startedAt = Date.now()
  let lastError

  while (Date.now() - startedAt < timeoutMs) {
    if (child.exitCode !== null || child.signalCode !== null) {
      throw new Error(`${label} exited before becoming ready.`)
    }

    try {
      const response = await fetch(url)
      if (response.ok) return
      lastError = new Error(`${label} readiness check returned HTTP ${response.status}.`)
    } catch (error) {
      lastError = error
    }

    await delay(400)
  }

  throw new Error(`${label} did not become ready at ${url} within ${timeoutMs}ms. ${lastError?.message ?? ''}`)
}

async function stopProcess(child) {
  if (child.exitCode !== null || child.signalCode !== null) return

  const exited = new Promise((resolvePromise) => child.once('close', resolvePromise))
  child.kill('SIGTERM')
  const didExit = await Promise.race([exited.then(() => true), delay(5_000).then(() => false)])
  if (!didExit && child.exitCode === null && child.signalCode === null) child.kill('SIGKILL')
}

let exitCode = 1

try {
  await assertPortAvailable('TaskFlow API', apiUrl)
  await assertPortAvailable('TaskFlow client', clientUrl)

  const api = startProcess('api', 'dotnet', [
    'run',
    '--project', apiProject,
    '--no-launch-profile',
    '--urls', apiUrl,
  ], {
    cwd: repositoryDirectory,
    env: testEnvironment,
  })

  await waitForUrl('TaskFlow API', `${apiUrl}/api/health`, api)

  const client = startProcess(process.env.CI ? 'vite' : 'client', process.execPath, [
    resolve(clientDirectory, 'node_modules/vite/bin/vite.js'),
    '--host',
    new URL(clientUrl).hostname,
    '--port',
    String(new URL(clientUrl).port || (new URL(clientUrl).protocol === 'https:' ? 443 : 80)),
    '--strictPort',
  ], {
    cwd: clientDirectory,
    env: testEnvironment,
  })

  await waitForUrl('TaskFlow client', clientUrl, client)

  await runProcess('Playwright', process.execPath, [playwrightCli, 'test', '--config=playwright.config.js'], {
    cwd: clientDirectory,
    env: testEnvironment,
  })
  exitCode = 0
} catch (error) {
  console.error(`End-to-end test setup failed: ${error.message}`)
} finally {
  await Promise.all(childProcesses.reverse().map(stopProcess))
  await rm(databaseDirectory, { recursive: true, force: true })
}

process.exitCode = exitCode
