#!/usr/bin/env node

import { existsSync, readFileSync } from 'node:fs'
import { relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

const IDENTITY_OWNER = 'src/Wanxiangshu/Participant/Persona/Identity.fs'
const AUTHORITY_FACTS = 'src/Wanxiangshu/Interaction/Authority/Facts.fs'
const AUTHORITY_MODEL = 'src/Wanxiangshu/Interaction/Authority/Model.fs'
const SOURCE_ROOT = 'src/Wanxiangshu'
const normalize = (path) => path.replaceAll('\\', '/')
const lineAt = (text, offset) => text.slice(0, offset).split('\n').length
const withoutLineComments = (text) => text.replace(/\/\/.*$/gm, '')
const violation = (file, line, rule, message) => ({ file, line, rule, message })

const recordDefinition = (text, name) => {
  const declaration = new RegExp(`^\\s*type\\s+${name}\\b`, 'm').exec(text)
  if (!declaration) return null
  const opening = text.indexOf('{', declaration.index)
  if (opening < 0) return null
  const closing = text.indexOf('}', opening)
  if (closing < 0) return null
  return { text: withoutLineComments(text.slice(declaration.index, closing + 1)), offset: declaration.index }
}

const scanPattern = (text, file, rule, pattern, message, failures) => {
  for (const match of text.matchAll(pattern)) {
    failures.push(violation(file, lineAt(text, match.index), rule, message))
  }
}

export const scanIdentityCollections = (text, file, failures) => {
  const identity = '(?:[A-Za-z_][A-Za-z0-9_]*\\.)*(?:ParticipantIdentity(?:Evidence)?|(?:Prompt)?IdentitySeed)'
  const sessionId = '(?:[A-Za-z_][A-Za-z0-9_]*\\.)*SessionId'
  const generic = new RegExp(
    `\\b(?:[A-Za-z_][A-Za-z0-9_]*\\.)*(?:Dictionary|ConcurrentDictionary|IDictionary|IReadOnlyDictionary|ImmutableDictionary|Map)\\s*<\\s*${sessionId}\\s*,\\s*${identity}\\b`,
    'g',
  )
  scanPattern(
    text,
    file,
    'session-identity-cache',
    generic,
    'SessionId-keyed ParticipantIdentity/IdentitySeed collection is forbidden',
    failures,
  )

  const registry = new RegExp(
    `^.*\\b(?:identity\\w*(?:cache|registry|map|dictionary)|(?:cache|registry|map|dictionary)\\w*identity)\\b[^\\n]*(?:SessionId[^\\n]*${identity}|${identity}[^\\n]*SessionId)[^\\n]*$`,
    'gim',
  )
  scanPattern(
    text,
    file,
    'session-identity-registry',
    registry,
    'SessionId-keyed ParticipantIdentity/IdentitySeed registry is forbidden',
    failures,
  )
}

export const scanAuthorityShape = (text, file, typeName, failures) => {
  const definition = recordDefinition(text, typeName)
  if (!definition) {
    failures.push(violation(file, 1, 'authority-identity-seed', `${typeName} record definition is missing`))
    return
  }
  if (!/\bIdentitySeed\s*:\s*(?:Prompt)?IdentitySeed\b|\bStoredIdentitySeed\s*:\s*(?:Prompt)?IdentitySeed\b/.test(definition.text)) {
    failures.push(
      violation(
        file,
        lineAt(text, definition.offset),
        'authority-identity-seed',
        `${typeName} must store IdentitySeed`,
      ),
    )
  }
  const duplicateFields = ['SelectedAgent', 'PeerAgent', 'CanonicalRole', 'SelectedTier'].filter((field) =>
    new RegExp(`\\b(?:Stored)?${field}\\s*:`).test(definition.text),
  )
  if (duplicateFields.length > 0) {
    failures.push(
      violation(
        file,
        lineAt(text, definition.offset),
        'flat-identity-duplicate',
        `${typeName} duplicates IdentitySeed fields: ${duplicateFields.join(', ')}`,
      ),
    )
  }
}

export const scanEntries = (entries) => {
  const failures = []
  for (const entry of entries) {
    const file = normalize(entry.file)
    const text = withoutLineComments(entry.text)

    if (file.endsWith(AUTHORITY_FACTS) || file === AUTHORITY_FACTS) {
      scanAuthorityShape(entry.text, file, 'AuthorityRootAcceptedPayload', failures)
    }
    if (file.endsWith(AUTHORITY_MODEL) || file === AUTHORITY_MODEL) {
      scanAuthorityShape(entry.text, file, 'AuthorityExecutionProfile', failures)
      if (!/member\s+this\.ParticipantIdentity\s*=[\s\S]{0,250}?(?:StoredIdentitySeed|identitySeedParticipantIdentity|PromptIdentitySeed\.participantIdentity)/.test(text)) {
        failures.push(
          violation(file, 1, 'authority-derived-identity', 'AuthorityExecutionProfile must derive ParticipantIdentity from IdentitySeed'),
        )
      }
    }
    scanPattern(
      text,
      file,
      'duplicate-identity-fact',
      /\bParticipantIdentityEstablished\b/g,
      'ParticipantIdentityEstablished would create a second identity fact owner',
      failures,
    )

    if (!file.endsWith(IDENTITY_OWNER) && file !== IDENTITY_OWNER) {
      scanIdentityCollections(text, file, failures)
    }
  }
  return failures
}

export const collectEntries = (root = process.cwd()) => {
  const sourcePath = resolve(root, SOURCE_ROOT)
  if (!existsSync(sourcePath)) {
    throw new Error(`participant-identity-boundary: required directory '${SOURCE_ROOT}' does not exist`)
  }
  return walk(sourcePath, ['.fs']).map((absolute) => ({
    file: normalize(relative(root, absolute)),
    text: readFileSync(absolute, 'utf8'),
  }))
}

export const scanRepo = (root = process.cwd()) => {
  const entries = collectEntries(root)
  return scanEntries(entries)
}

/**
 * §7.2 check(context) interface.
 * @param {{ sourceFiles?: () => string[], readText?: (path: string) => string }} context
 */
export function check(context) {
  const root = process.cwd()
  let entries
  if (context && typeof context.sourceFiles === 'function' && typeof context.readText === 'function') {
    const files = context.sourceFiles().filter((f) => normalize(f).startsWith(SOURCE_ROOT) && f.endsWith('.fs'))
    entries = files.map((file) => ({
      file: normalize(file),
      text: context.readText(file),
    }))
  } else {
    entries = collectEntries(root)
  }
  const failures = scanEntries(entries)
  return {
    issues: failures.map((f) => ({
      code: f.rule,
      path: f.file,
      line: f.line,
      message: f.message,
    })),
  }
}

export const runCli = (root = process.cwd()) => {
  const { issues } = check({
    sourceFiles: () => walk(resolve(root, SOURCE_ROOT), ['.fs']).map((f) => normalize(relative(root, f))),
    readText: (f) => readFileSync(resolve(root, f), 'utf8'),
  })
  if (issues.length > 0) {
    console.error(`participant-identity-boundary: ${issues.length} violation(s)`)
    for (const issue of issues) {
      console.error(`  ${issue.path}:${issue.line} [${issue.code}] ${issue.message}`)
    }
    return 1
  }
  console.log('participant-identity-boundary: OK — opaque logical-run evidence, atomic root authority boundary, and zero session-scoped or parallel identity owners')
  return 0
}

export const run = (root = process.cwd()) => {
  try {
    return runCli(root)
  } catch (error) {
    console.error(`participant-identity-boundary: ${error.message}`)
    return 1
  }
}

const isMain = process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)
if (isMain) process.exit(runCli())
