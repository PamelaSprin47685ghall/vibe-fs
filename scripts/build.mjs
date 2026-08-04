import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { execFileSync } from 'node:child_process'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const dist = path.join(root, 'dist')

function fail(message) {
  console.error(message)
  process.exit(1)
}

function removeGitignores(dir) {
  if (!fs.existsSync(dir)) return
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name)
    if (entry.isDirectory()) removeGitignores(full)
    else if (entry.name === '.gitignore') fs.unlinkSync(full)
  }
}

function removeSources(dir) {
  if (!fs.existsSync(dir)) return
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name)
    if (entry.isDirectory()) removeSources(full)
    else if (entry.name.endsWith('.fs') || entry.name.endsWith('.fsproj')) fs.unlinkSync(full)
  }
}

fs.rmSync(dist, { recursive: true, force: true })

try {
  execFileSync(
    'dotnet',
    [
      'tool',
      'run',
      'fable',
      'precompile',
      'src/Wanxiangshu/Wanxiangshu.fsproj',
      '-o',
      'dist',
    ],
    { cwd: root, stdio: 'inherit' },
  )
} catch {
  fail('fable precompile failed')
}

removeGitignores(dist)
removeSources(dist)

const entry = path.join(root, 'dist/Infrastructure/OpenCode/Plugin/Plugin.js')
if (!fs.existsSync(entry)) fail(`missing entry: ${entry}`)

const catalog = path.join(root, 'resources/enforcer/catalog.json')
if (!fs.existsSync(catalog)) fail(`missing catalog: ${catalog}`)

const prompts = [
  'manager',
  'coder',
  'devops',
  'inspector',
  'reviewer',
  'browser',
  'meditator',
  'orchestrator',
  'executor',
  'blogger',
]
for (const name of prompts) {
  const promptPath = path.join(root, 'resources/prompts', `${name}-system.md`)
  if (!fs.existsSync(promptPath)) fail(`missing prompt: ${promptPath}`)
}

console.log('build ok')
