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

const enforcerRoot = path.join(root, 'resources/enforcer')
if (!fs.existsSync(enforcerRoot)) fail(`missing rulebook root: ${enforcerRoot}`)
const ruleDirs = fs
  .readdirSync(enforcerRoot, { withFileTypes: true })
  .filter((entry) => entry.isDirectory())
  .map((entry) => entry.name)
if (ruleDirs.length < 1) fail(`enforcer rulebook has no rule directories under ${enforcerRoot}`)
const catalogJson = path.join(enforcerRoot, 'catalog.json')
if (fs.existsSync(catalogJson)) fail(`catalog.json must be removed after folder cutover: ${catalogJson}`)
for (const name of ['primitive-obsession', ruleDirs[0]]) {
  const enforcerMd = path.join(enforcerRoot, name, 'enforcer.md')
  const mainMd = path.join(enforcerRoot, name, 'main.md')
  if (!fs.existsSync(enforcerMd)) fail(`missing rulebook file: ${enforcerMd}`)
  if (!fs.existsSync(mainMd)) fail(`missing rulebook file: ${mainMd}`)
}

const providerRoles = [
  'manager',
  'coder',
  'devops',
  'inspector',
  'reviewer',
  'browser',
  'inquiry',
  'orchestrator',
  'distiller',
  'blogger',
  'bookkeeper',
]
for (const name of providerRoles) {
  for (const locale of ['en.md', 'zh-CN.md']) {
    const rolePath = path.join(root, 'resources/provider/role', name, locale)
    if (!fs.existsSync(rolePath)) fail(`missing Role Law: ${rolePath}`)
  }
}
for (const leaf of ['world/common-law', 'library/ingress', 'library/closing']) {
  for (const locale of ['en.md', 'zh-CN.md']) {
    const asset = path.join(root, 'resources/provider', leaf, locale)
    if (!fs.existsSync(asset)) fail(`missing provider asset: ${asset}`)
  }
}

const sphinxEntry = path.join(dist, 'Sphinx', 'McpServer.js')
if (!fs.existsSync(sphinxEntry)) fail(`missing sphinx entry: ${sphinxEntry}`)

console.log('build ok')