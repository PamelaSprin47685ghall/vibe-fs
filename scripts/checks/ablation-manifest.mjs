#!/usr/bin/env node

import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'

const ROOT = process.cwd()

export function check(context) {
  const root = context?.root ?? ROOT
  const readAt = (rel) => readFileSync(join(root, rel), 'utf8')
  const issues = []
  const index = readAt('requirements/INDEX.md')
  const nodes = JSON.parse(readAt('resources/ablation/nodes.json'))
  const toolMap = JSON.parse(readAt('resources/ablation/tool-map.json'))
  const factMap = JSON.parse(readAt('resources/ablation/fact-map.json'))
  const profiles = JSON.parse(readAt('resources/ablation/profiles.json'))
  const staticTools = readAt('src/Wanxiangshu/OpenCode/Tools/StaticTools.fs')

  const packageDirs = () =>
    readdirSync(join(root, 'requirements'), { withFileTypes: true })
      .filter((entry) => entry.isDirectory())
      .map((entry) => entry.name)
      .filter((name) => {
        try {
          readAt(`requirements/${name}/WHAT.md`)
          return true
        } catch {
          return false
        }
      })

  const primaryNodes = nodes.nodes.filter((node) => node.kind === 'package').map((node) => node.id)
  const packages = packageDirs()

  for (const pkg of packages) {
    if (!index.includes(`\`${pkg}\``)) {
      issues.push({ code: 'index-missing', message: `requirements/${pkg} missing from INDEX.md` })
    }
  }

  for (const id of primaryNodes) {
    if (!packages.includes(id)) {
      issues.push({ code: 'node-orphan', message: `ablation node ${id} has no requirements/${id}` })
    }
  }

  for (const pkg of packages) {
    if (pkg === 'feature-ablation') continue
    if (!primaryNodes.includes(pkg)) {
      issues.push({ code: 'node-missing', message: `package ${pkg} missing from resources/ablation/nodes.json` })
    }
  }

  const knownMatch = staticTools.match(/let knownToolNames =\s*\[([\s\S]*?)\]/)
  const knownTools = knownMatch
    ? [...knownMatch[1].matchAll(/"([^"]+)"/g)].map((match) => match[1])
    : []

  for (const tool of knownTools) {
    if (!toolMap.tools[tool]) {
      issues.push({ code: 'tool-unmapped', message: `known tool ${tool} missing from resources/ablation/tool-map.json` })
    }
  }

  for (const [tool, node] of Object.entries(toolMap.tools)) {
    if (!knownTools.includes(tool)) {
      issues.push({ code: 'tool-stale', message: `tool-map entry ${tool} not in StaticTools.knownToolNames` })
    }
    if (!nodes.nodes.some((entry) => entry.id === node)) {
      issues.push({ code: 'tool-node', message: `tool ${tool} maps to unknown node ${node}` })
    }
  }

  for (const [tag, node] of Object.entries(factMap.facts)) {
    if (!nodes.nodes.some((entry) => entry.id === node)) {
      issues.push({ code: 'fact-node', message: `fact ${tag} maps to unknown node ${node}` })
    }
  }

  if (Object.keys(factMap.facts).length === 0) {
    issues.push({ code: 'fact-empty', message: 'fact-map.json has no entries' })
  }

  if (!profiles.profiles.production) {
    issues.push({ code: 'profile-production', message: 'profiles.json missing production profile' })
  }

  for (const [profileId, profile] of Object.entries(profiles.profiles)) {
    for (const node of primaryNodes) {
      if (!profile.modes?.[node]) {
        issues.push({ code: 'profile-incomplete', message: `profile ${profileId} missing node ${node}` })
      }
    }
  }

  return { issues }
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const { issues } = check({ root: ROOT })
  if (issues.length === 0) {
    console.log('ablation-manifest: OK')
    process.exit(0)
  }
  console.error(`ablation-manifest: FAILED (${issues.length})`)
  for (const issue of issues) {
    console.error(`  [${issue.code}] ${issue.message}`)
  }
  process.exit(1)
}
