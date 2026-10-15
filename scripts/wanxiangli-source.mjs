import fs from 'node:fs'
import path from 'node:path'
import { execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const npm = process.platform === 'win32' ? 'npm.cmd' : 'npm'

function git(cwd, args) {
  return execFileSync('git', args, { cwd, encoding: 'utf8' }).trim()
}

export function assertNoTrackedArchives(directory) {
  const tracked = execFileSync('git', ['ls-files', '-z'], { cwd: directory, encoding: 'utf8' })
  const archives = tracked.split('\0').filter(name => /\.tgz$/i.test(name))
  if (archives.length) throw new Error(`Tracked .tgz archives are forbidden: ${archives.join(', ')}`)
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
  assertNoTrackedArchives(directory)
  assertNoTrackedArchives(source)
  const entry = git(directory, ['ls-files', '--stage', '--', 'wanxiangli'])
  if (!entry.startsWith('160000 ')) throw new Error('wanxiangli must be registered as a Git submodule')
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
  execFileSync(npm, ['install', '--package-lock-only', '--ignore-scripts', '--no-audit', '--no-fund'], {
    cwd: directory, stdio: 'inherit',
  })
  console.log(`wanxiangli now follows origin/main at ${git(source, ['rev-parse', 'HEAD'])}; review and commit the gitlink and lockfile.`)
}

export function buildSourceDependency(directory = root) {
  const source = checkSourceDependency(directory)
  const installed = path.join(directory, 'node_modules', 'wanxiangli')
  if (!fs.existsSync(installed) || fs.realpathSync(installed) !== fs.realpathSync(source)) {
    throw new Error('Run npm ci to replace the old installed tarball with the wanxiangli source link')
  }
  execFileSync(npm, ['ci', '--ignore-scripts', '--no-audit', '--no-fund'], { cwd: source, stdio: 'inherit' })
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
