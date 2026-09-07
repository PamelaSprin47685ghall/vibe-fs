#!/usr/bin/env node

import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { readCompileShardInventory, stronglyConnectedComponents } from '../lib/compile-shards.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const POLICY_PATH = resolve(ROOT, 'scripts/checks/subsystems.json')

export function readSubsystemPolicy(policyPath = POLICY_PATH) {
  const policy = JSON.parse(readFileSync(policyPath, 'utf8'))
  if (policy.schema_version !== 1 || !Array.isArray(policy.subsystems) || policy.subsystems.length === 0) {
    throw new Error('subsystems.json: expected schema_version=1 and a non-empty subsystems array')
  }
  const ids = new Set()
  const legacyOwnerSubsystem = new Map()
  for (const subsystem of policy.subsystems) {
    if (!/^[a-z][a-z0-9-]*$/.test(subsystem.id ?? '') || ids.has(subsystem.id)) throw new Error(`subsystems.json: invalid/duplicate subsystem id '${subsystem.id ?? ''}'`)
    ids.add(subsystem.id)
    for (const owner of subsystem.legacy_owners ?? []) {
      if (legacyOwnerSubsystem.has(owner)) throw new Error(`subsystems.json: legacy owner '${owner}' belongs to multiple subsystems`)
      legacyOwnerSubsystem.set(owner, subsystem.id)
    }
  }
  return { policy, ids, legacyOwnerSubsystem }
}

export function buildSubsystemInventory({ compileInventory = readCompileShardInventory(), policyState = readSubsystemPolicy() } = {}) {
  const violations = []
  const projects = new Map()
  const shardKeys = new Set()
  const usedLegacyOwners = new Set()
  for (const project of compileInventory.projects.values()) {
    const subsystem = project.explicitSubsystem || policyState.legacyOwnerSubsystem.get(project.legacyOwner) || ''
    const shard = project.explicitCompileShard || project.legacyLocality || ''
    if (!subsystem || !policyState.ids.has(subsystem)) violations.push(`${project.projectRepoPath}: compile shard has no valid subsystem`)
    if (!shard) violations.push(`${project.projectRepoPath}: compile shard has no stable shard id`)
    if (project.legacyOwner) usedLegacyOwners.add(project.legacyOwner)
    const shardKey = `${subsystem}/${shard}`
    if (subsystem && shard && shardKeys.has(shardKey)) violations.push(`${project.projectRepoPath}: duplicate compile shard ${shardKey}`)
    shardKeys.add(shardKey)
    projects.set(project.projectPath, { ...project, subsystem, shard, shardKey })
  }
  for (const owner of usedLegacyOwners) {
    const explicitlyOwned = [...projects.values()].some((project) => project.legacyOwner === owner && project.explicitSubsystem)
    if (!policyState.legacyOwnerSubsystem.has(owner) && !explicitlyOwned) violations.push(`legacy owner '${owner}' is not mapped to a subsystem`)
  }

  const subsystemEdges = new Set()
  let crossSubsystemReferences = 0
  for (const project of projects.values()) {
    for (const reference of project.references) {
      const provider = projects.get(reference)
      if (!provider) continue
      if (project.subsystem !== provider.subsystem) {
        crossSubsystemReferences += 1
        subsystemEdges.add(`${project.subsystem}\0${provider.subsystem}`)
      }
      if (
        project.explicitSubsystem === 'runtime-platform'
        && project.explicitCompileShard
        && provider.subsystem !== 'runtime-platform'
      ) {
        violations.push(`${project.shardKey}: reusable runtime-platform shard depends on domain subsystem ${provider.subsystem}/${provider.shard}`)
      }
    }
  }
  const subsystemIds = new Set([...projects.values()].map((project) => project.subsystem).filter(Boolean))
  const edgePairs = [...subsystemEdges].map((edge) => edge.split('\0'))
  const components = stronglyConnectedComponents(subsystemIds, edgePairs)
  const cyclicComponents = components.filter((component) => component.length > 1)
  return {
    ok: violations.length === 0,
    violations,
    compileInventory,
    projects,
    subsystemIds,
    subsystemEdges: edgePairs,
    subsystemCount: subsystemIds.size,
    shardCount: projects.size,
    sourceCount: compileInventory.sourceCount,
    projectReferenceCount: compileInventory.projectReferenceCount,
    crossSubsystemReferences,
    cyclicComponents,
    largestSubsystemCycle: cyclicComponents[0] ?? [],
  }
}

export function checkSubsystems() {
  try {
    return buildSubsystemInventory()
  } catch (error) {
    return { ok: false, violations: [error instanceof Error ? error.message : String(error)] }
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const result = checkSubsystems()
  if (!result.ok) {
    console.error(`subsystems: FAILED — ${result.violations.length} violation(s)`)
    for (const violation of result.violations) console.error(`  ${violation}`)
    process.exit(1)
  }
  const cycle = result.largestSubsystemCycle.length > 1 ? `, largest subsystem cycle=${result.largestSubsystemCycle.length}` : ''
  console.log(`subsystems: OK — ${result.subsystemCount} subsystems, ${result.shardCount} compile shards, ${result.sourceCount} sources, ${result.projectReferenceCount} refs, shard DAG${cycle}`)
}
