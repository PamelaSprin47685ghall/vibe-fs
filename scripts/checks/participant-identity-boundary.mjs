#!/usr/bin/env node

import { existsSync, readFileSync } from 'node:fs'
import { relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

const IDENTITY_OWNER = 'src/Wanxiangshu/Participant/Persona/Identity.fs'
const AUTHORITY_FACTS = 'src/Wanxiangshu/Interaction/Authority/Facts.fs'
const AUTHORITY_MODEL = 'src/Wanxiangshu/Interaction/Authority/Model.fs'
const SOURCE_ROOT = 'src/Wanxiangshu'
const RETIRED_PRODUCTION_FILES = [
  'src/Wanxiangshu/Participant/Persona/SessionPersona.fs',
  'src/Wanxiangshu/Participant/Persona/SessionSurface.fs',
  'src/Wanxiangshu/Participant/Persona/RoleIdentity.fs',
  'src/Wanxiangshu/OpenCode/Host/PersonaBinding.fs',
]
const SURFACE_MANIFEST = 'scripts/lib/test-surface-scan.mjs'
const RETIRED_SESSION_SURFACE_MODULE = 'Participant/Persona/SessionSurface.js'
const RETIRED_EMITTED_SURFACE = `dist/${RETIRED_SESSION_SURFACE_MODULE}`

const normalize = (path) => path.replaceAll('\\', '/')
const lineAt = (text, offset) => text.slice(0, offset).split('\n').length
const withoutLineComments = (text) => text.replace(/\/\/.*$/gm, '')

const violation = (file, line, rule, message) => ({ file, line, rule, message })

const requiredFile = (root, relativePath, failures) => {
  const path = resolve(root, relativePath)
  if (!existsSync(path)) {
    failures.push(violation(relativePath, 1, 'required-surface', 'required identity surface is missing'))
    return null
  }
  return readFileSync(path, 'utf8')
}

const requirePattern = (text, pattern, file, rule, message, failures) => {
  if (!pattern.test(withoutLineComments(text))) failures.push(violation(file, 1, rule, message))
}

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

const scanRetiredIdentitySurfaces = (root, failures) => {
  for (const file of RETIRED_PRODUCTION_FILES) {
    if (existsSync(resolve(root, file))) {
      failures.push(violation(file, 1, 'retired-identity-file', 'retired identity production file must not exist'))
    }
  }

  const manifestPath = resolve(root, SURFACE_MANIFEST)
  if (existsSync(manifestPath)) {
    const manifest = readFileSync(manifestPath, 'utf8')
    const registration = /module:\s*['"]Participant\/Persona\/SessionSurface\.js['"]/g
    scanPattern(
      manifest,
      SURFACE_MANIFEST,
      'retired-session-surface-registration',
      registration,
      `retired surface '${RETIRED_SESSION_SURFACE_MODULE}' must not be registered`,
      failures,
    )
  }

  if (existsSync(resolve(root, RETIRED_EMITTED_SURFACE))) {
    failures.push(
      violation(
        RETIRED_EMITTED_SURFACE,
        1,
        'retired-session-surface-emission',
        `retired surface '${RETIRED_SESSION_SURFACE_MODULE}' must not be emitted`,
      ),
    )
  }
}

const scanRetiredIdentityTokens = (text, file, failures) => {
  for (const token of ['SessionPersona', 'PersonaBinding', 'SessionSurface', 'AgentRoleIdentity']) {
    scanPattern(
      text,
      file,
      'retired-identity-token',
      new RegExp(`\\b${token}\\b`, 'g'),
      `retired identity token '${token}' is forbidden in production source`,
      failures,
    )
  }
}

const scanIdentityCollections = (text, file, failures) => {
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

const scanPrivateConstruction = (text, file, failures) => {
  if (file === IDENTITY_OWNER) return

  const patterns = [
    /:\s*ParticipantIdentity(?:Evidence)?\s*=\s*\{/g,
    /\{(?=[^}]{0,1000}\bSelectedAgent\s*=)(?=[^}]{0,1000}\bPeerAgent\s*=)(?=[^}]{0,1000}\bKind\s*=)(?=[^}]{0,1000}\bInitialTier\s*=)(?=[^}]{0,1000}\bPersona\s*=)(?=[^}]{0,1000}\bPersonaCatalogVersion\s*=)(?=[^}]{0,1000}\bOrigin\s*=)[^}]{0,1000}\}/g,
    /\bPersonaName\s*(?:\(|")/g,
    /\bPersonaCatalogVersion\s*(?:\(|\d)/g,
    /\bParticipantKind\b/g,
    /\bManagedRole\b/g,
  ]
  for (const pattern of patterns) {
    scanPattern(
      text,
      file,
      'private-identity-construction',
      pattern,
      'raw construction of opaque ParticipantIdentity internals is forbidden outside its owner',
      failures,
    )
  }
}

const scanAuthorityShape = (text, file, typeName, failures) => {
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

export const scanRepo = (root = process.cwd()) => {
  const failures = []
  scanRetiredIdentitySurfaces(root, failures)
  const identity = requiredFile(root, IDENTITY_OWNER, failures)
  const facts = requiredFile(root, AUTHORITY_FACTS, failures)
  const model = requiredFile(root, AUTHORITY_MODEL, failures)

  if (identity !== null) {
    requirePattern(
      identity,
      /type\s+ParticipantIdentity\s*=\s*private\s*\{/s,
      IDENTITY_OWNER,
      'opaque-identity-owner',
      'ParticipantIdentity must remain an opaque private record',
      failures,
    )
    requirePattern(
      identity,
      /type\s+ParticipantIdentityEvidence\s*=\s*private\s*\{/s,
      IDENTITY_OWNER,
      'opaque-identity-owner',
      'ParticipantIdentityEvidence must remain opaque',
      failures,
    )
    for (const member of ['resolveAtRoot', 'inheritFromOwner', 'rehydrate', 'selectedAgent', 'peerAgent', 'role', 'persona', 'origin']) {
      requirePattern(
        identity,
        new RegExp(`\\blet\\s+${member}\\b`),
        IDENTITY_OWNER,
        'opaque-identity-api',
        `ParticipantIdentity owner API is missing '${member}'`,
        failures,
      )
    }
  }

  if (facts !== null) scanAuthorityShape(facts, AUTHORITY_FACTS, 'AuthorityRootAcceptedPayload', failures)
  if (model !== null) {
    scanAuthorityShape(model, AUTHORITY_MODEL, 'AuthorityExecutionProfile', failures)
    requirePattern(
      model,
      /member\s+this\.ParticipantIdentity\s*=[\s\S]{0,250}?(?:StoredIdentitySeed|identitySeedParticipantIdentity|PromptIdentitySeed\.participantIdentity)/,
      AUTHORITY_MODEL,
      'authority-derived-identity',
      'AuthorityExecutionProfile must derive ParticipantIdentity from IdentitySeed',
      failures,
    )
  }

  const sourcePath = resolve(root, SOURCE_ROOT)
  if (!existsSync(sourcePath)) {
    failures.push(violation(SOURCE_ROOT, 1, 'required-surface', 'production source root is missing'))
    return failures
  }

  for (const absolute of walk(sourcePath, ['.fs'])) {
    const file = normalize(relative(root, absolute))
    const text = withoutLineComments(readFileSync(absolute, 'utf8'))
    scanRetiredIdentityTokens(text, file, failures)
    scanPattern(
      text,
      file,
      'duplicate-identity-fact',
      /\bParticipantIdentityEstablished\b/g,
      'ParticipantIdentityEstablished would create a second identity fact owner',
      failures,
    )
    if (file !== IDENTITY_OWNER) scanIdentityCollections(text, file, failures)
    scanPrivateConstruction(text, file, failures)
  }

  return failures
}

export const run = (root = process.cwd()) => {
  let failures
  try {
    failures = scanRepo(root)
  } catch (error) {
    console.error(`participant-identity-boundary: ${error.message}`)
    return 1
  }

  if (failures.length > 0) {
    console.error(`participant-identity-boundary: ${failures.length} violation(s)`)
    for (const failure of failures) {
      console.error(`  ${failure.file}:${failure.line} [${failure.rule}] ${failure.message}`)
    }
    return 1
  }

  console.log('participant-identity-boundary: OK — opaque logical-run evidence, atomic root authority boundary, and zero session-scoped or parallel identity owners')
  return 0
}

const isMain = process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)
if (isMain) process.exit(run())
