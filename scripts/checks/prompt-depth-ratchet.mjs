#!/usr/bin/env node
/**
 * Prompt-depth shrink ratchet — anti-accidental-amputation alarm.
 *
 * Not a quality metric. A Role Law that suddenly loses >20% normalized prose
 * must update resources/provider/prompt-depth-baseline.json explicitly and
 * explain which cognitive obligation was removed.
 *
 * Modes:
 *   node scripts/checks/prompt-depth-ratchet.mjs
 *   node scripts/checks/prompt-depth-ratchet.mjs --generate [--out=<file>]
 */

import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { ROLE_ANCHOR_DIRS } from './semantic-anchors.mjs'

const SHRINK_LIMIT = 0.2
const ROLE_DIRS = ROLE_ANCHOR_DIRS
const LOCALES = Object.freeze(['en.md', 'zh-CN.md'])

const root = resolve(process.argv.includes('--root')
  ? process.argv[process.argv.indexOf('--root') + 1]
  : join(dirname(fileURLToPath(import.meta.url)), '../..'))

const DEFAULT_BASELINE = join(root, 'resources/provider/prompt-depth-baseline.json')
const PROVIDER_ROLE = join(root, 'resources/provider/role')

const argValue = (flag) => {
  const args = process.argv.slice(2)
  const inline = args.find((a) => a.startsWith(`${flag}=`))
  if (inline) return inline.slice(flag.length + 1)
  const index = args.indexOf(flag)
  return index >= 0 ? args[index + 1] : undefined
}

const normalizeProse = (text) =>
  text
    .replace(/\r\n/g, '\n')
    .replace(/```[\s\S]*?```/g, ' ')
    .replace(/[ \t]+/g, ' ')
    .replace(/\n{3,}/g, '\n\n')
    .trim()

const metricsFor = (text) => {
  const normalized = normalizeProse(text)
  const paragraphs = normalized.split(/\n\s*\n/).filter((p) => p.trim().length > 0)
  const headings = (text.match(/^#{1,3} .+$/gm) ?? []).length
  return {
    normalizedBytes: Buffer.byteLength(normalized, 'utf8'),
    paragraphCount: paragraphs.length,
    headingCount: headings,
  }
}

const snapshot = () => {
  const roles = {}
  for (const role of ROLE_DIRS) {
    roles[role] = {}
    for (const locale of LOCALES) {
      const abs = join(PROVIDER_ROLE, role, locale)
      if (!existsSync(abs)) {
        throw new Error(`missing Role Law: role/${role}/${locale}`)
      }
      roles[role][locale] = metricsFor(readFileSync(abs, 'utf8'))
    }
  }
  return {
    version: 1,
    shrinkLimit: SHRINK_LIMIT,
    note: 'Anti-accidental-amputation alarm for Prompt Restoration. Update deliberately when removing cognitive obligations.',
    roles,
  }
}

const check = (baseline) => {
  const findings = []
  const current = snapshot()
  for (const role of ROLE_DIRS) {
    for (const locale of LOCALES) {
      const before = baseline.roles?.[role]?.[locale]
      const after = current.roles[role][locale]
      if (!before) {
        findings.push({
          level: 'error',
          message: `${role}/${locale}: missing from baseline — run with --generate after intentional restore`,
        })
        continue
      }
      if (before.normalizedBytes <= 0) continue
      const shrink = (before.normalizedBytes - after.normalizedBytes) / before.normalizedBytes
      if (shrink > (baseline.shrinkLimit ?? SHRINK_LIMIT)) {
        findings.push({
          level: 'error',
          message:
            `${role}/${locale}: normalizedBytes shrank ${(shrink * 100).toFixed(1)}% ` +
            `(${before.normalizedBytes} → ${after.normalizedBytes}); update baseline only when a cognitive obligation is deliberately removed`,
        })
      }
    }
  }
  return findings
}

const main = () => {
  if (process.argv.includes('--generate')) {
    const out = argValue('--out') ?? DEFAULT_BASELINE
    writeFileSync(out, `${JSON.stringify(snapshot(), null, 2)}\n`)
    console.log(`prompt-depth-ratchet: baseline written to ${out}`)
    return
  }

  const baselinePath = argValue('--baseline') ?? DEFAULT_BASELINE
  if (!existsSync(baselinePath)) {
    console.error(`prompt-depth-ratchet: baseline missing at ${baselinePath}; run --generate after Role Law restore`)
    process.exit(1)
  }

  const baseline = JSON.parse(readFileSync(baselinePath, 'utf8'))
  const findings = check(baseline)
  for (const finding of findings) console.error(`${finding.level}: ${finding.message}`)
  if (findings.some((f) => f.level === 'error')) {
    console.error(`prompt-depth-ratchet: ${findings.length} finding(s)`)
    process.exit(1)
  }
  console.log('prompt-depth-ratchet: ok')
}

main()
