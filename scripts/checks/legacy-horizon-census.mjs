#!/usr/bin/env node

import { createHash } from 'node:crypto'
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { dirname, isAbsolute, join, resolve } from 'node:path'
import { pathToFileURL } from 'node:url'

const pre050Markers = [
  '"FailuresOnCurrentSide"', '"IsDead"', '"TotalFailures"', '"BaseModelID"',
  '"BaseProviderID"', '"EffectiveModelID"', '"EffectiveProviderID"',
  '"PluginPromptAccepted"', '"HumanPromptAccepted"', '"GuardPromptAccepted"',
  '"InteractionRepairClaimed"', '"ReviewConfirmedIdle"', '"AgentLinked"',
  '"AgentForked"', '"AgentUnlinked"', '"OrchestratorManagerJobCreated"',
  '"OrchestratorCandidateRegistered"', '"OrchestratorPublished"',
  '"OrchestratorRejected"', '"OrchestratorRebased"', '"OrchestratorConflictDetected"',
  '"OrchestratorPreRebaseReviewConfirmed"', '"OrchestratorPostRebaseReviewConfirmed"',
  '"OrchestratorPublishClaimed"', '"EnforcementCycleCommitted"',
  '"DurableEffectRequested"', '"DurableEffectAccepted"',
]

const emptyCounts = () => ({
  pre050: 0,
  scoreVector: 0,
  unanchoredGuideline: 0,
  incompleteHandleCompleted: 0,
})

const classify = (line) => {
  const observation = line.includes('"BlogObservationCommitted"') || line.includes('"BlogEntryCommitted"')
  const handleCompleted = line.includes('"HandleCompleted"')
  return {
    pre050: pre050Markers.some((marker) => line.includes(marker)),
    scoreVector: observation && (line.includes('"ScoreVectorRef"') || !line.includes('"TipRuleId"')),
    unanchoredGuideline: line.includes('"PairProgrammingGuidelineAppended"'),
    incompleteHandleCompleted: handleCompleted
      && (!line.includes('"CompletionRef"') || !line.includes('"CompletionDigest"')),
  }
}

const inventoryRoots = (rootsFile) => {
  const absoluteInventory = resolve(rootsFile)
  if (!existsSync(absoluteInventory)) throw new Error(`legacy-horizon-census: missing roots file ${absoluteInventory}`)
  const base = dirname(absoluteInventory)
  const manifest = readFileSync(absoluteInventory, 'utf8')
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0 && !line.startsWith('#'))
  if (manifest.length === 0) throw new Error('legacy-horizon-census: roots inventory is empty')
  const uniqueManifest = [...new Set(manifest)].sort()
  return {
    manifest: uniqueManifest,
    roots: uniqueManifest.map((line) => resolve(isAbsolute(line) ? line : join(base, line))),
  }
}

export async function censusFromRootsFile(rootsFile) {
  const { manifest, roots } = inventoryRoots(rootsFile)
  const counts = emptyCounts()
  let journals = 0
  let lines = 0

  for (const root of roots) {
    if (!existsSync(root) || !statSync(root).isDirectory()) {
      throw new Error(`legacy-horizon-census: declared workspace is missing or not a directory: ${root}`)
    }
    const events = join(root, '.git', 'wanxiang', 'events')
    if (!existsSync(events) || !statSync(events).isDirectory()) {
      throw new Error(`legacy-horizon-census: missing events directory: ${events}`)
    }
    const files = readdirSync(events, { withFileTypes: true })
      .filter((entry) => entry.isFile() && entry.name.endsWith('.ndjson'))
      .map((entry) => join(events, entry.name))
      .sort()
    journals += files.length

    for (const file of files) {
      const rawLines = readFileSync(file, 'utf8').split(/\r?\n/)
      for (let index = 0; index < rawLines.length; index += 1) {
        const line = rawLines[index]
        if (line.length === 0 && index === rawLines.length - 1) continue
        if (line.trim().length === 0) throw new Error(`legacy-horizon-census: blank NDJSON line ${file}:${index + 1}`)
        try { JSON.parse(line) } catch (error) {
          throw new Error(`legacy-horizon-census: invalid NDJSON ${file}:${index + 1}: ${error.message}`)
        }
        lines += 1
        const flags = classify(line)
        for (const key of Object.keys(counts)) if (flags[key]) counts[key] += 1
      }
    }
  }

  return {
    workspaces: roots.length,
    journals,
    lines,
    counts,
    rootsDigest: createHash('sha256').update(manifest.join('\n')).digest('hex'),
    roots: manifest,
  }
}

const parseArgs = (argv) => {
  const arg = argv.find((value) => value.startsWith('--roots-file='))
  if (!arg || argv.length !== 1) throw new Error('legacy-horizon-census: usage --roots-file=<inventory>')
  return arg.slice('--roots-file='.length)
}

const runCli = async () => {
  try {
    console.log(JSON.stringify(await censusFromRootsFile(parseArgs(process.argv.slice(2))), null, 2))
  } catch (error) {
    console.error(error.message)
    process.exit(2)
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) await runCli()
