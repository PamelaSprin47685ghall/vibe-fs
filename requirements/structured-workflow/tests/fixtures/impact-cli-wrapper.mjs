// Thin shim kept isolated under tests/: `compile-impact.mjs` was retired
// in W4, but this suite's semantics (focused compile + `--output` +
// `--scratch` hash) must still live. This runner speaks the same
// compileIncremental contract as `scripts/build.mjs` — no manifest writes,
// no dist writes — while still running one real Fable compile per call.
import { createHash } from 'node:crypto'
import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs'
import { join, resolve } from 'node:path'

import {
  compileIncremental,
  DEFAULT_SCRATCH_ROOT,
} from '../../../../scripts/lib/owner-compile.mjs'

const args = process.argv.slice(2)

const props = []
const changed = []
for (let index = 0; index < args.length; index++) {
  const arg = args[index]
  if (arg === '--projects' || arg === '--props' || arg === '--scratch' || arg === '-o' || arg === '--output') {
    props.push({ name: arg === '-o' ? '--output' : arg, value: args[++index] })
  } else if (arg.startsWith('--')) {
    console.error(`unknown flag: ${arg}`)
    process.exit(1)
  } else {
    changed.push(arg)
  }
}

const projectsIndex = props.findIndex((arg) => arg.name === '--projects')
if (projectsIndex === -1) {
  console.error('--projects required')
  process.exit(1)
}

// `--projects` is the directory `discoverOwnerProjects` walks for the
// `Wanxiangshu.Owner.*.fsproj` shard graph. `compileIncremental` keeps
// caller-supplied roots (build root / scratch / output) orthogonal.
const projectDirectory = resolve(props[projectsIndex].value)
const scratchRoot = resolve(props.find((arg) => arg.name === '--scratch')?.value ?? DEFAULT_SCRATCH_ROOT)
const rootPropsPath = resolve(props.find((arg) => arg.name === '--props')?.value ?? 'Directory.Build.props')
const outputDir = resolve(props.find((arg) => arg.name === '--output')?.value ?? 'out')

function scanSourceHashes(dir, ignoredDirs = new Set()) {
  const hashes = new Map()
  const scan = (current) => {
    if (!existsSync(current)) return
    for (const entry of readdirSync(current, { withFileTypes: true })) {
      const full = join(current, entry.name)
      if (entry.isDirectory()) {
        if (
          entry.name.startsWith('.') ||
          entry.name === 'bin' ||
          entry.name === 'obj' ||
          ignoredDirs.has(full)
        ) {
          continue
        }
        scan(full)
      } else if (/\.(fs|fsi|fsproj|props)$/i.test(entry.name)) {
        try {
          const content = readFileSync(full)
          const hash = createHash('sha256').update(content).digest('hex')
          hashes.set(full, hash)
        } catch {
          // Ignore read errors
        }
      }
    }
  }
  scan(dir)
  return hashes
}

const ignoredDirs = new Set([scratchRoot, outputDir])
const snapshotPath = resolve(scratchRoot, '.cli-snapshot.json')

// Changed paths must be explicit — the production `build.mjs` detects via
// manifest diff; the fixture contract is that a CLI caller names the
// changed file or falls back to snapshot diff in scratchRoot.
let effectiveChanged = changed
if (effectiveChanged.length === 0) {
  const currentHashes = scanSourceHashes(projectDirectory, ignoredDirs)
  const changedPaths = []
  if (existsSync(snapshotPath)) {
    try {
      const previous = JSON.parse(readFileSync(snapshotPath, 'utf8'))
      for (const [file, hash] of currentHashes) {
        if (previous[file] !== hash) {
          changedPaths.push(file)
        }
      }
      for (const file of Object.keys(previous)) {
        if (!currentHashes.has(file)) {
          changedPaths.push(file)
        }
      }
    } catch {
      changedPaths.push(...currentHashes.keys())
    }
  } else {
    changedPaths.push(...currentHashes.keys())
  }
  effectiveChanged = changedPaths
}

if (effectiveChanged.length === 0) {
  console.log('[impact-cli] no inputs changed')
  process.exit(0)
}

const result = await compileIncremental({
  changedPaths: effectiveChanged,
  root: projectDirectory,
  projectDirectory,
  scratchRoot,
  rootPropsPath,
  outputDir,
  // Pipe stdio: capture Fable diagnostics onto stderr on failure; on success
  // the boilerplate banner does not pollute the [impact-cli] log line.
  stdio: 'pipe',
})
if (result.ok) {
  try {
    mkdirSync(scratchRoot, { recursive: true })
    const finalHashes = scanSourceHashes(projectDirectory, ignoredDirs)
    writeFileSync(
      snapshotPath,
      JSON.stringify(Object.fromEntries(finalHashes), null, 2),
      'utf8',
    )
  } catch {
    // Snapshot save is best-effort in fixture wrapper
  }
  // Emit the [owner-compile] success trace unconditionally — with
  // `stdio: 'pipe'` compileIncremental does not echo it, and the test
  // surface asserts on that banner as the "real Fable completed" marker.
  console.log(`[owner-compile] OK: Wanxiangshu.Impact.fsproj in ${result.elapsedMs}ms (fp:${result.fingerprint?.slice(0, 12) ?? 'n/a'})`)
}
if (!result.ok) {
  console.error(`[impact-cli] compile failed (code=${result.code})`)
  if (result.stderr) console.error(result.stderr)
  process.exit(result.code ?? 1)
}

console.log(`[impact-cli] compiled ${result.mode} impact (${result.compileItems?.length ?? 0} items)`)
