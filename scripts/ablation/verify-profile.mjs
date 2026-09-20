#!/usr/bin/env node

import { createRequire } from 'node:module'
import { check } from '../checks/ablation-manifest.mjs'

const require = createRequire(import.meta.url)
const ROOT = new URL('../..', import.meta.url).pathname

const profileId = process.argv[2]
if (!profileId) {
  console.error('Usage: node scripts/ablation/verify-profile.mjs <profile-id>')
  console.error('Example: node scripts/ablation/verify-profile.mjs station-42')
  process.exit(2)
}

const { issues } = check({ root: ROOT })
if (issues.length > 0) {
  console.error(`ablation-manifest: FAILED (${issues.length})`)
  for (const issue of issues) {
    console.error(`  [${issue.code}] ${issue.message}`)
  }
  process.exit(1)
}

const previous = process.env.WANXIANGSHU_ABLATION_PROFILE
process.env.WANXIANGSHU_ABLATION_PROFILE = profileId

try {
  const Ablation = require(`${ROOT}/dist/Ablation/Surface.js`)
  const result = Ablation.load()
  if (!result.ok) {
    console.error(`verify-profile: FAILED profile=${profileId}`)
    console.error(`  [${result.kind}] ${result.error}`)
    process.exit(1)
  }
  if (result.profile !== profileId) {
    console.error(`verify-profile: FAILED profile=${profileId} loaded=${result.profile}`)
    process.exit(1)
  }
  console.log(`verify-profile: OK profile=${profileId} nodes=${result.nodeCount} manifest=${result.manifestVersion}`)
} finally {
  if (previous === undefined) delete process.env.WANXIANGSHU_ABLATION_PROFILE
  else process.env.WANXIANGSHU_ABLATION_PROFILE = previous
}
