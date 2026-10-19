import fs from 'node:fs'
import path from 'node:path'
import { execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { createHash } from 'node:crypto'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const npm = process.platform === 'win32' ? 'npm.cmd' : 'npm'

// npm run 把全局 .npmrc 的 allow-scripts 以 npm_config_allow_scripts 形式导出给
// 子进程；npm 12 在项目级安装里把它当 CLI 层配置并拒绝（EALLOWSCRIPTS）。
// 子进程的安装策略应归属各项目自身的 package.json/.npmrc，与全局 shell 配置无关。
function npmCommand(args, cwd) {
  const env = { ...process.env }
  delete env.npm_config_allow_scripts
  return execFileSync(npm, args, { cwd, env, stdio: 'inherit' })
}

function git(cwd, args) {
  return execFileSync('git', args, { cwd, encoding: 'utf8' }).trim()
}

export function checkTrackedArchives(directory) {
  const tracked = execFileSync('git', ['ls-files', '-z'], { cwd: directory, encoding: 'utf8' })
  const archives = tracked.split('\0').filter(name => /\.tgz$/i.test(name))
  const manifest = path.join(directory, 'evidence/retained-archives.json')
  const evidence = fs.existsSync(manifest) ? JSON.parse(fs.readFileSync(manifest, 'utf8')) : null
  const retained = evidence?.archives ?? {}
  if (!retained || typeof retained !== 'object' || Array.isArray(retained)) throw new Error('Invalid retained evidence manifest')
  const unexpected = archives.filter(name => !Object.hasOwn(retained, name))
  if (unexpected.length) throw new Error(`Tracked dependency archives are forbidden: ${unexpected.join(', ')}`)
  const historical = new Map()
  if (evidence !== null) {
    if (!/^[0-9a-f]{40}$/.test(evidence.baseline ?? '')) throw new Error('Retained evidence requires a fixed baseline commit')
    const tree = execFileSync('git', ['ls-tree', '-r', '-z', evidence.baseline], { cwd: directory, encoding: 'utf8' })
    for (const entry of tree.split('\0').filter(Boolean)) {
      const match = /^[0-9]+ blob ([0-9a-f]{40})\t(.+\.tgz)$/s.exec(entry)
      if (match && /^(?:evidence\/|proposals\/extraction-kanban\/evidence\/)/.test(match[2])) historical.set(match[2], match[1])
    }
    if (historical.size !== Object.keys(retained).length) throw new Error('Retained evidence must match the complete baseline inventory')
  }
  for (const [name, digest] of Object.entries(retained)) {
    if (!/^(?:evidence\/|proposals\/extraction-kanban\/evidence\/).+\.tgz$/.test(name) || !archives.includes(name)) {
      throw new Error(`Retained evidence is outside its tracked scope: ${name}`)
    }
    const bytes = fs.readFileSync(path.join(directory, name))
    const actual = createHash('sha256').update(bytes).digest('hex')
    if (actual !== digest) throw new Error(`Retained evidence changed: ${name}`)
    const objectId = createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex')
    if (historical.get(name) !== objectId) throw new Error(`Retained evidence differs from its baseline: ${name}`)
  }
}

function sourceDirectory(directory) {
  const source = path.join(directory, 'wanxiangli')
  if (!fs.existsSync(path.join(source, 'package.json'))) {
    throw new Error('Initialize the source dependency: git submodule update --init -- wanxiangli')
  }
  const spec = JSON.parse(fs.readFileSync(path.join(directory, 'package.json'), 'utf8'))
  if (spec.dependencies?.wanxiangli !== 'file:wanxiangli') {
    throw new Error('wanxiangli must be consumed from file:wanxiangli, not an archive or a second checkout')
  }
  return source
}

export function checkSourceDependency(directory = root) {
  const source = sourceDirectory(directory)
  checkTrackedArchives(directory)
  checkTrackedArchives(source)
  const entry = git(directory, ['ls-files', '--stage', '--', 'wanxiangli'])
  const pinned = /^160000 ([0-9a-f]{40}) 0\twanxiangli$/.exec(entry)
  if (!pinned) throw new Error('wanxiangli must have one resolved Git submodule entry')
  if (git(source, ['rev-parse', 'HEAD']) !== pinned[1]) throw new Error('wanxiangli checkout must match its pinned gitlink')
  const branch = git(directory, ['config', '-f', '.gitmodules', '--get', 'submodule.wanxiangli.branch'])
  if (branch !== 'main') throw new Error('wanxiangli must track its main branch')
  return source
}

// 开发更新是显式 Git 操作；构建和运行不会悄悄联网换版本。
export function updateSourceDependency(directory = root) {
  let source = path.join(directory, 'wanxiangli')
  if (!fs.existsSync(path.join(source, 'package.json'))) {
    git(directory, ['submodule', 'update', '--init', '--', 'wanxiangli'])
  }
  source = checkSourceDependency(directory)
  if (git(source, ['status', '--porcelain'])) throw new Error('Commit or preserve local wanxiangli edits before updating')
  git(source, ['fetch', 'origin', 'main'])
  execFileSync('git', ['merge-base', '--is-ancestor', 'HEAD', 'origin/main'], { cwd: source })
  git(source, ['merge', '--ff-only', 'origin/main'])
  npmCommand(['install', '--package-lock-only', '--ignore-scripts', '--no-audit', '--no-fund'], directory)
  console.log(`wanxiangli now follows origin/main at ${git(source, ['rev-parse', 'HEAD'])}; review and commit the gitlink and lockfile.`)
}

export function buildSourceDependency(directory = root) {
  const source = checkSourceDependency(directory)
  const installed = path.join(directory, 'node_modules', 'wanxiangli')
  if (!fs.existsSync(installed) || fs.realpathSync(installed) !== fs.realpathSync(source)) {
    throw new Error('Run npm ci to replace the old installed tarball with the wanxiangli source link')
  }
  npmCommand(['ci', '--ignore-scripts', '--no-audit', '--no-fund'], source)
  execFileSync(process.execPath, ['scripts/build.mjs'], { cwd: source, stdio: 'inherit' })
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    switch (process.argv[2]) {
      case 'check': checkSourceDependency(); break
      case 'update': updateSourceDependency(); break
      case 'build': buildSourceDependency(); break
      default: throw new Error('Usage: node scripts/wanxiangli-source.mjs check|update|build')
    }
  } catch (error) {
    console.error(error.message)
    process.exitCode = 1
  }
}
