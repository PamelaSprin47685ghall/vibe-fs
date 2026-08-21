#!/usr/bin/env node

import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(join(fileURLToPath(new URL('.', import.meta.url)), '../..'))
const sourceRoot = join(root, 'src/Wanxiangshu')
const lowLevelTomlAccess = /\bSyntheticToml\./
const formattingOwners = new Set([
  'src/Wanxiangshu/Foundation/LlmFacing.fs',
  'src/Wanxiangshu/Foundation/SyntheticTomlSurface.fs',
])
const forbiddenEnvelopes = [
  '<skill_content',
  '</skill_content>',
  '<work-log>',
  '</work-log>',
  '<requirement_read',
  '</requirement_read>',
]

const walk = (directory) =>
  readdirSync(directory).flatMap((name) => {
    const path = join(directory, name)
    return statSync(path).isDirectory() ? walk(path) : path.endsWith('.fs') ? [path] : []
  })

const violations = []
for (const path of walk(sourceRoot)) {
  const rel = relative(root, path).replaceAll('\\', '/')
  const text = readFileSync(path, 'utf8')
  // Architecture prose may name a forbidden legacy representation while
  // explaining why it is forbidden. Gate executable source, not comments.
  const codeText = text
    .split('\n')
    .map((line) => line.replace(/\/\/.*$/, ''))
    .join('\n')

  if (!formattingOwners.has(rel) && lowLevelTomlAccess.test(codeText)) {
    violations.push(`${rel}: direct SyntheticToml access bypasses LlmFacing`)
  }

  for (const envelope of forbiddenEnvelopes) {
    if (codeText.includes(envelope)) violations.push(`${rel}: forbidden LLM-facing envelope ${envelope}`)
  }
}

if (violations.length > 0) {
  console.error('llm-facing-format-gate: FAIL')
  for (const violation of violations) console.error(`- ${violation}`)
  process.exit(1)
}

console.log('llm-facing-format-gate: OK — LlmFacing owns synthetic LLM representation')
