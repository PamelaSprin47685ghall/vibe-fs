#!/usr/bin/env node
import crypto from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { execFileSync } from 'node:child_process'

import { run as runSurfaceManifest } from './checks/js-surface-manifest.mjs'
import { run as runModuleLinkage } from './checks/js-module-linkage.mjs'
import {
  writeLoopDetectorEnvelopeArtifact,
} from './lib/derive-loop-detector-envelope.mjs'
import {
  compileIncremental,
  resetOutputDirectory,
  readImpactInventory,
  planImpactFromInventory,
} from './lib/owner-compile.mjs'
import {
  MANIFEST_SCHEMA,
  collectCompilerInputs,
  collectGeneratedInputs,
  collectArtifactInputs,
  collectOutputs,
  computeDigest,
  readManifest,
  writeManifest,
  invalidateManifest,
} from './lib/build-state.mjs'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const dist = path.join(root, 'dist')
const buildStateDir = path.join(root, '.fable-build')
const buildLockFile = path.join(buildStateDir, 'build.lock')

// ── Diagnostics & Output ─────────────────────────────────────────────────────

function formatBanner(title, color = '\x1b[31m') {
  const line = '═'.repeat(80)
  return `${color}${line}\n  ${title}\n${line}\x1b[0m`
}

function fail(message, details = null) {
  console.error(formatBanner('BUILD FAILED'))
  if (message) console.error(message)
  if (details) console.error(`\n${details}`)
  process.exit(1)
}

function logInfo(msg) {
  console.log(`\x1b[36m[build]\x1b[0m ${msg}`)
}

async function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

// ── Build Serialization ──────────────────────────────────────────────────────

function isPidRunning(pid) {
  if (!pid || typeof pid !== 'number' || isNaN(pid) || pid <= 0) return false
  try {
    process.kill(pid, 0)
    return true
  } catch (err) {
    return err.code === 'EPERM'
  }
}

export class CrossProcessMutex {
  constructor(lockPath, name = 'lock') {
    this.lockPath = lockPath
    this.name = name
    this.held = false
  }

  async acquire(waitTimeoutMs = 180_000) {
    fs.mkdirSync(path.dirname(this.lockPath), { recursive: true })
    const deadline = Date.now() + waitTimeoutMs
    while (Date.now() < deadline) {
      try {
        const payload = JSON.stringify({ pid: process.pid })
        fs.writeFileSync(this.lockPath, payload, { flag: 'wx', encoding: 'utf8' })
        this.held = true
        return true
      } catch (err) {
        if (err.code !== 'EEXIST') throw err

        // Check if existing lock is dead or stale
        try {
          const raw = fs.readFileSync(this.lockPath, 'utf8')
          const info = JSON.parse(raw)
          const isDead = !isPidRunning(info.pid)

          if (isDead) {
            try {
              fs.unlinkSync(this.lockPath)
              continue
            } catch {}
          }
        } catch {
          try {
            fs.unlinkSync(this.lockPath)
            continue
          } catch {}
        }

        await sleep(100)
      }
    }

    throw new Error(`Failed to acquire ${this.name} after ${waitTimeoutMs}ms (lock at ${this.lockPath})`)
  }

  release() {
    if (!this.held) return
    try {
      if (fs.existsSync(this.lockPath)) {
        const raw = fs.readFileSync(this.lockPath, 'utf8')
        const info = JSON.parse(raw)
        if (info.pid === process.pid) {
          fs.unlinkSync(this.lockPath)
        }
      }
    } catch {}
    this.held = false
  }
}

// ── Resource & Artifact Verification ─────────────────────────────────────────

async function verifyArtifacts(targetRoot = root) {
  // DG-004: repository is the SSOT. Derive the current envelope on every build;
  // materialize it only as an ephemeral runtime import.
  try {
    await writeLoopDetectorEnvelopeArtifact(targetRoot)
  } catch (err) {
    throw new Error(`Failed to derive loop detector repository envelope: ${err.message}`)
  }

  const entry = path.join(targetRoot, 'dist/OpenCode/Plugin/Plugin.js')
  if (!fs.existsSync(entry)) throw new Error(`missing entry artifact: ${entry}`)

  const sphinxEntry = path.join(targetRoot, 'dist/Sphinx/ServeEntry.js')
  if (!fs.existsSync(sphinxEntry)) throw new Error(`missing sphinx entry artifact: ${sphinxEntry}`)

  const enforcerRoot = path.join(targetRoot, 'resources/enforcer')
  if (!fs.existsSync(enforcerRoot)) throw new Error(`missing rulebook root: ${enforcerRoot}`)
  const ruleDirs = fs
    .readdirSync(enforcerRoot, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
  if (ruleDirs.length < 1) throw new Error(`enforcer rulebook has no rule directories under ${enforcerRoot}`)
  const catalogJson = path.join(enforcerRoot, 'catalog.json')
  if (fs.existsSync(catalogJson)) throw new Error(`catalog.json must be removed after folder cutover: ${catalogJson}`)

  for (const name of ['primitive-obsession', ruleDirs[0]]) {
    const enforcerMd = path.join(enforcerRoot, name, 'enforcer.md')
    const mainMd = path.join(enforcerRoot, name, 'main.md')
    if (!fs.existsSync(enforcerMd)) throw new Error(`missing rulebook file: ${enforcerMd}`)
    if (!fs.existsSync(mainMd)) throw new Error(`missing rulebook file: ${mainMd}`)
  }

  const providerRoles = [
    'manager',
    'coder',
    'devops',
    'inspector',
    'browser',
    'inquiry',
    'orchestrator',
    'distiller',
    'blogger',
    'bookkeeper',
  ]
  for (const name of providerRoles) {
    for (const locale of ['en.md', 'zh-CN.md']) {
      const rolePath = path.join(targetRoot, 'resources/provider/role', name, locale)
      if (!fs.existsSync(rolePath)) throw new Error(`missing Role Law: ${rolePath}`)
    }
  }

  for (const leaf of ['world/common-law', 'library/ingress', 'library/closing']) {
    for (const locale of ['en.md', 'zh-CN.md']) {
      const asset = path.join(targetRoot, 'resources/provider', leaf, locale)
      if (!fs.existsSync(asset)) throw new Error(`missing provider asset: ${asset}`)
    }
  }

  // JS-SEMANTIC-SURFACE-003/005: dist surface manifest validation (post-compile).
  if (runSurfaceManifest({ root: targetRoot }) !== 0) {
    throw new Error('js-surface-manifest: dist surface manifest validation failed')
  }
  if (runModuleLinkage({ root: targetRoot }) !== 0) {
    throw new Error('js-module-linkage: emitted ESM graph is not package-closed')
  }
}

function getToolchainIdentity() {
  let dotnetVer = 'unknown'
  let fableVer = 'unknown'
  try {
    dotnetVer = execFileSync('dotnet', ['--version'], { encoding: 'utf8' }).trim()
  } catch {}
  try {
    fableVer = execFileSync('dotnet', ['tool', 'run', 'fable', '--version'], { encoding: 'utf8' }).trim()
  } catch {}
  return `dotnet ${dotnetVer} / fable ${fableVer}`
}

function computeCorpusDigest(generatedInputs, targetRoot) {
  const hasher = crypto.createHash('sha256')
  for (const entry of generatedInputs) {
    const abs = path.resolve(targetRoot, entry.path)
    if (fs.existsSync(abs)) {
      hasher.update(fs.readFileSync(abs))
    }
  }
  return hasher.digest('hex')
}

// ── Run Build Orchestrator ───────────────────────────────────────────────────

export async function runBuild({
  targetRoot = root,
  clean = false,
  stdio = 'inherit',
} = {}) {
  const resolvedRoot = path.resolve(targetRoot)
  const targetDist = path.join(resolvedRoot, 'dist')
  const lockFile = path.join(resolvedRoot, '.fable-build/build.lock')
  const mutex = new CrossProcessMutex(lockFile, 'build lock')
  await mutex.acquire()

  try {
    const existingManifest = readManifest({ root: resolvedRoot })

    // Snapshot current inputs
    // WP5 cutover: the wrapper aggregate fsproj is deleted. collectCompilerInputs
    // reads through the compile-shard inventory itself — passing the null
    // aggregate says "no wrapper to exclude", letting shards be the only
    // source of truth without placeholder plumbing.
    const compilerInputs = collectCompilerInputs(resolvedRoot, null)
    const compilerInputDigest = computeDigest(compilerInputs)

    const generatedInputs = collectGeneratedInputs(resolvedRoot)
    const generatedInputDigest = computeDigest(generatedInputs)

    const artifactInputs = collectArtifactInputs(resolvedRoot)
    const artifactInputDigest = computeDigest(artifactInputs)

    let buildMode = 'clean'
    let changedCompilerPaths = []

    if (!clean && existingManifest && existingManifest.schema === MANIFEST_SCHEMA) {
      // Check outputs
      const recordedOutputs = existingManifest.outputs ?? {}
      const currentOutputs = collectOutputs(targetDist)
      const recordedKeys = Object.keys(recordedOutputs)
      const currentKeys = Object.keys(currentOutputs)

      const outputsValid = recordedKeys.length > 0 &&
        recordedKeys.length === currentKeys.length &&
        recordedKeys.every((k) => currentOutputs[k] && currentOutputs[k][0] === recordedOutputs[k][0])

      if (outputsValid) {
        // Compare compiler inputs
        const oldCompilerInputs = existingManifest.compiler?.inputs ?? []
        const oldMap = new Map(oldCompilerInputs.map((e) => [e.path, e]))
        const currentMap = new Map(compilerInputs.map((e) => [e.path, e]))

        let topologyChanged = false
        if (oldCompilerInputs.length !== compilerInputs.length) {
          topologyChanged = true
        } else {
          for (const curr of compilerInputs) {
            const old = oldMap.get(curr.path)
            if (!old) {
              topologyChanged = true
              break
            }
            if (old.sha256 !== curr.sha256) {
              changedCompilerPaths.push(path.resolve(resolvedRoot, curr.path))
              const ext = path.extname(curr.path).toLowerCase()
              if (ext !== '.fs' && ext !== '.fsi') {
                topologyChanged = true
              }
            }
          }
        }

        if (topologyChanged) {
          buildMode = 'clean'
        } else if (changedCompilerPaths.length > 0) {
          buildMode = 'focused'
        } else if (
          generatedInputDigest === existingManifest.generated?.inputDigest &&
          artifactInputDigest === existingManifest.artifacts?.inputDigest
        ) {
          buildMode = 'no-op'
        } else {
          // Non-compiler inputs changed (e.g. envelope corpus or artifacts)
          buildMode = 'focused'
        }
      }
    }

    if (buildMode === 'no-op') {
      logInfo('build up-to-date (no-op)')
      return {
        ok: true,
        mode: 'no-op',
        generation: existingManifest.generation ?? 1,
        reused: true,
      }
    }

    // Invalidate manifest before running compiler/writing to dist
    invalidateManifest({ root: resolvedRoot })

    const compileNeeded = buildMode === 'clean' || (buildMode === 'focused' && changedCompilerPaths.length > 0)

    if (compileNeeded && buildMode === 'clean') {
      logInfo('Compiling F# (clean)...')
      resetOutputDirectory(targetDist)
      const compileResult = await compileIncremental({
        root: resolvedRoot,
        outputDir: targetDist,
        stdio,
      })
      if (!compileResult.ok) {
        throw new Error(
          `Fable compilation failed${compileResult.signal ? ` by signal ${compileResult.signal}` : ` with exit code ${compileResult.code}`}`,
        )
      }
      logInfo(`compiled clean impact (${compileResult.compileItems?.length ?? 0} items in ${compileResult.elapsedMs}ms)`)
    } else if (compileNeeded) {
      logInfo('Compiling F# (focused)...')
      const compileResult = await compileIncremental({
        changedPaths: changedCompilerPaths.length > 0 ? changedCompilerPaths : undefined,
        root: resolvedRoot,
        outputDir: targetDist,
        stdio,
      })
      if (!compileResult.ok) {
        throw new Error(
          `Fable compilation failed${compileResult.signal ? ` by signal ${compileResult.signal}` : ` with exit code ${compileResult.code}`}`,
        )
      }
      logInfo(`compiled focused impact (${compileResult.compileItems?.length ?? 0} items in ${compileResult.elapsedMs}ms)`)
    }

    // Envelope & artifact verification
    await verifyArtifacts(resolvedRoot)

    // Recheck input snapshot inside lock
    const finalCompilerInputs = collectCompilerInputs(resolvedRoot, null)
    const finalCompilerDigest = computeDigest(finalCompilerInputs)
    if (finalCompilerDigest !== compilerInputDigest) {
      throw new Error('Mid-build mutation detected: compiler inputs changed during compilation')
    }

    const finalGeneratedInputs = collectGeneratedInputs(resolvedRoot)
    const finalGeneratedDigest = computeDigest(finalGeneratedInputs)
    if (finalGeneratedDigest !== generatedInputDigest) {
      throw new Error('Mid-build mutation detected: generated inputs changed during compilation')
    }

    const finalArtifactInputs = collectArtifactInputs(resolvedRoot)
    const finalArtifactDigest = computeDigest(finalArtifactInputs)
    if (finalArtifactDigest !== artifactInputDigest) {
      throw new Error('Mid-build mutation detected: artifact inputs changed during compilation')
    }

    // Collect final outputs
    const outputs = collectOutputs(targetDist)
    const nextGeneration = (existingManifest?.generation ?? 0) + 1

    const newManifest = {
      schema: MANIFEST_SCHEMA,
      rootIdentity: resolvedRoot,
      shardInventoryDirectory: 'src/Wanxiangshu',
      outputDir: path.relative(resolvedRoot, targetDist).replace(/\\/g, '/'),
      generation: nextGeneration,
      compiler: {
        configuration: 'Debug',
        toolIdentity: getToolchainIdentity(),
        inputDigest: finalCompilerDigest,
        inputs: finalCompilerInputs,
      },
      generated: {
        inputDigest: finalGeneratedDigest,
        corpusPathList: finalGeneratedInputs.map((e) => e.path),
        corpusDigest: computeCorpusDigest(finalGeneratedInputs, resolvedRoot),
        generatorIdentity: 'derive-loop-detector-envelope.mjs@v1',
        tokenizerIdentity: 'gpt-tokenizer@4.0.0',
      },
      artifacts: {
        inputDigest: finalArtifactDigest,
        inputs: finalArtifactInputs,
      },
      outputs,
    }

    writeManifest({ root: resolvedRoot, manifest: newManifest })
    logInfo(`build ok (generation ${nextGeneration})`)

    return {
      ok: true,
      mode: buildMode,
      generation: nextGeneration,
      reused: false,
    }
  } finally {
    mutex.release()
  }
}

export const buildEntrypoint = runBuild

/**
 * Compare current compiler inputs against the manifest-recorded snapshot to
 * surface the actual changed paths the next build would act on. Cheap
 * content-hash diff keyed by sha256 — identical semantics to `detectChangedFiles`.
 */
function diffInputsForPlan({ resolvedRoot, manifest, compilerInputs }) {
  const previous = new Map(
    (manifest.compiler?.inputs ?? []).map((entry) => [
      path.resolve(resolvedRoot, entry.path),
      entry,
    ]),
  )
  const changedInputs = []
  const previousKept = new Set()
  for (const entry of compilerInputs) {
    const abs = path.resolve(resolvedRoot, entry.path)
    previousKept.add(abs)
    const before = previous.get(abs)
    if (!before || before.sha256 !== entry.sha256) changedInputs.push(abs)
  }
  for (const abs of previous.keys()) {
    if (!previousKept.has(abs)) changedInputs.push(abs)
  }
  return changedInputs
}


/**
 * WP4: read-only preview of what the next `npm run build` would do. Uses the
 * same manifest/digest path the build itself reads — no compiler spawn, no
 * manifest write, no dist write. Returns the structured plan; callers can
 * print or inspect `plan.fableCompileInvocations` and `selectedShards`.
 */
export async function planBuild({
  targetRoot = root,
} = {}) {
  const resolvedRoot = path.resolve(targetRoot)
  const buildStateDirectory = path.join(resolvedRoot, '.fable-build')
  const manifestPath = path.join(buildStateDirectory, 'build-manifest.json')
  const manifest = readManifest({ root: resolvedRoot })
  const compilerInputs = collectCompilerInputs(resolvedRoot, null)
  const compilerInputDigest = computeDigest(compilerInputs)
  const generatedInputs = collectGeneratedInputs(resolvedRoot)
  const generatedInputDigest = computeDigest(generatedInputs)
  const artifactInputs = collectArtifactInputs(resolvedRoot)
  const artifactInputDigest = computeDigest(artifactInputs)


  const missingDist = !fs.existsSync(path.join(resolvedRoot, 'dist'))
  const compilerInputsMatch = manifest.compiler?.inputDigest === compilerInputDigest
  const generatedInputsMatch = manifest.generated?.inputDigest === generatedInputDigest
  const artifactInputsMatch = manifest.artifacts?.inputDigest === artifactInputDigest

  if (compilerInputsMatch && generatedInputsMatch && artifactInputsMatch && !missingDist) {
    return {
      mode: 'no-op',
      reason: 'build up-to-date',
      changedInputs: [],
      selectedShards: [],
      compileItems: [],
      fableCompileInvocations: 0,
      manifestPath,
      compilerInputDigest,
      generatedInputDigest,
      artifactInputDigest,
    }
  }

  const changedInputs = diffInputsForPlan({ resolvedRoot, manifest, compilerInputs })

  const inventory = readImpactInventory({
    projectDirectory: path.join(resolvedRoot, 'src/Wanxiangshu'),
  })
  const plan = planImpactFromInventory({
    inventory,
    changedPaths: changedInputs.length > 0
      ? changedInputs
      : compilerInputs.map((entry) => path.resolve(resolvedRoot, entry.path)),
    fullThreshold: 0.6,
    isClean: false,
  })

  return {
    mode: plan.mode,
    reason: plan.reason,
    changedInputs: changedInputs.map((abs) => path.relative(resolvedRoot, abs).replace(/\\/g, '/')),
    selectedShards: plan.projectPaths,
    compileItems: plan.compileItems,
    fableCompileInvocations: 1,
    manifestPath,
    compilerInputDigest,
    generatedInputDigest,
    artifactInputDigest,
    missingDist,
  }
}

// ── Clean Signal & Exit Handlers ─────────────────────────────────────────────

function registerSignalHandlers() {
  const cleanup = () => {
    try {
      if (fs.existsSync(buildLockFile)) {
        const raw = fs.readFileSync(buildLockFile, 'utf8')
        const info = JSON.parse(raw)
        if (info.pid === process.pid) fs.unlinkSync(buildLockFile)
      }
    } catch {}
  }

  process.on('SIGINT', () => {
    cleanup()
    process.exit(130)
  })
  process.on('SIGTERM', () => {
    cleanup()
    process.exit(143)
  })
  process.on('exit', cleanup)
}

// ── Main Entrypoint ──────────────────────────────────────────────────────────

async function main() {
  registerSignalHandlers()

  const knownOptions = new Set(['--clean', '--plan', '--help', '-h'])
  const unknown = process.argv.slice(2).filter((arg) => arg.startsWith('-') && !knownOptions.has(arg))
  if (unknown.length > 0) {
    console.error(`unknown option(s): ${unknown.join(', ')}`)
    process.exit(1)
  }

  if (process.argv.includes('--help') || process.argv.includes('-h')) {
    console.log(`
Usage: node scripts/build.mjs [options]

Options:
  --clean      Force clean full rebuild and invalidate manifest
  --plan       Compute and print the next build plan as JSON; no compile,
               resource emit, manifest, or dist write is performed
  --help, -h   Show this help message
`)
    process.exit(0)
  }

  const clean = process.argv.includes('--clean')
  const plan = process.argv.includes('--plan')

  if (plan && clean) {
    console.error('--plan is a read-only preview and cannot be combined with --clean')
    process.exit(1)
  }

  if (plan) {
    try {
      const report = await planBuild({ targetRoot: root })
      console.log(JSON.stringify(report, null, 2))
      process.exit(0)
    } catch (err) {
      console.error(`[build:plan] ${err.message}`)
      process.exit(1)
    }
  }

  try {
    await runBuild({ targetRoot: root, clean })
  } catch (err) {
    fail(err.message)
  }
}

const isDirectRun = process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
if (isDirectRun) {
  await main()
}
