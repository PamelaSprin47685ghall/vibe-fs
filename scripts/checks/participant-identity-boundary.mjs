#!/usr/bin/env node

import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { dirname, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const OWNER = 'participant-identity-owner'
const CATALOG_FILE = 'src/Wanxiangshu/Participant/Persona/Catalog.fs'
const SESSION_FILE = 'src/Wanxiangshu/Participant/Persona/SessionPersona.fs'
const BINDING_FILE = 'src/Wanxiangshu/OpenCode/Host/PersonaBinding.fs'
const ROLES_FILE = 'src/Wanxiangshu/Foundation/Roles.fs'
const LEGACY_FILE = 'src/Wanxiangshu/Participant/Persona/RoleIdentity.fs'
const SOURCE_ROOT = 'src/Wanxiangshu'

export const PARTICIPANT_IDENTITY_OWNER_FILES = Object.freeze([
  CATALOG_FILE,
  SESSION_FILE,
  BINDING_FILE,
  ROLES_FILE,
])

const PERSONAS = Object.freeze([
  'Integrator', 'Director', 'Coordinator', 'Lead', 'Coder', 'Engineer', 'Scout',
  'Investigator', 'Technician', 'Operator', 'Navigator', 'Researcher', 'Analyst',
  'Inquirer', 'Examiner', 'Auditor', 'Scribe', 'Chronicler', 'Condenser',
  'Distiller', 'Clerk', 'Curator',
])

const normalizePath = (path) => path.replace(/\\/g, '/')
const lineAt = (source, offset) => source.slice(0, offset).split('\n').length
const textAt = (source, offset) => source.slice(offset).split('\n', 1)[0].trim()

const violation = (rule, file, source, offset, text = textAt(source, offset)) => ({
  id: OWNER,
  rule,
  file: normalizePath(file),
  line: lineAt(source, offset),
  text,
})

// Removes comments and string contents while preserving offsets and newlines.
const codeOnly = (source) => {
  let output = ''
  let index = 0
  let blockDepth = 0
  let string = false
  while (index < source.length) {
    const char = source[index]
    const next = source[index + 1]
    if (blockDepth > 0) {
      if (char === '(' && next === '*') { output += '  '; blockDepth += 1; index += 2 }
      else if (char === '*' && next === ')') { output += '  '; blockDepth -= 1; index += 2 }
      else { output += char === '\n' ? '\n' : ' '; index += 1 }
    } else if (string) {
      if (char === '\\' && next !== undefined) { output += '  '; index += 2 }
      else if (char === '"') { output += ' '; string = false; index += 1 }
      else { output += char === '\n' ? '\n' : ' '; index += 1 }
    } else if (char === '/' && next === '/') {
      const end = source.indexOf('\n', index)
      if (end === -1) return output + ' '.repeat(source.length - index)
      output += ' '.repeat(end - index); index = end
    } else if (char === '(' && next === '*') {
      output += '  '; blockDepth = 1; index += 2
    } else if (char === '"') {
      output += ' '; string = true; index += 1
    } else {
      output += char; index += 1
    }
  }
  return output
}

const firstOffset = (source, pattern) => {
  const match = pattern.exec(source)
  pattern.lastIndex = 0
  return match?.index ?? 0
}

/** Scan the Persona catalog's closed type and canonical rendering contract. */
export const scanPersonaCatalogSource = (file, source) => {
  const violations = []
  const typeMatch = /(?:\[<RequireQualifiedAccess>\]\s*)?type\s+Persona\s*=([\s\S]*?)(?=\n(?:\[<|module\s|type\s|let\s)|$)/.exec(source)
  const typeBody = typeMatch?.[1] ?? ''
  const cases = [...typeBody.matchAll(/\|\s*([A-Z][A-Za-z0-9_]*)\b/g)].map((match) => match[1])
  if (!typeMatch || !typeMatch[0].includes('RequireQualifiedAccess') ||
      cases.length !== PERSONAS.length || PERSONAS.some((name, index) => cases[index] !== name)) {
    violations.push(violation('catalog-closed-persona', file, source, typeMatch?.index ?? 0,
      'Catalog must declare the exhaustive RequireQualifiedAccess Persona DU'))
  }

  const renderMatch = /let\s+(?:canonical|render)\s*\(\s*persona\s*:\s*Persona\s*\)[^=]*=([\s\S]*?)(?=\n\s*let\s|$)/.exec(source)
  const renderBody = renderMatch?.[1] ?? ''
  const rendersAll = PERSONAS.every((name) =>
    new RegExp(`\\|\\s*Persona\\.${name}\\s*->\\s*"${name}"`).test(renderBody))
  if (!renderMatch || !rendersAll) {
    violations.push(violation('catalog-canonical-rendering', file, source, renderMatch?.index ?? 0,
      'Catalog must render every Persona to its canonical name'))
  }
  return violations
}

/** Scan typed, SessionId-scoped, immutable SessionPersona storage. */
export const scanSessionPersonaSource = (file, source) => {
  const violations = []
  const code = codeOnly(source)
  const compact = code.replace(/\s+/g, ' ')
  if (!/Dictionary\s*<\s*SessionId\s*,\s*Persona\s*>/.test(code)) {
    violations.push(violation('session-persona-storage', file, source,
      firstOffset(code, /Dictionary\s*</), 'SessionPersona storage must contain Persona values'))
  }
  if (!/type\s+PersonaRejection\s*=/.test(code) ||
      !/Result\s*<\s*Persona\s*,\s*PersonaRejection\s*>/.test(code)) {
    violations.push(violation('session-typed-rejection', file, source,
      firstOffset(code, /(?:type\s+PersonaRejection|let\s+bindOnce)/),
      'bindOnce must expose Result<Persona, PersonaRejection>'))
  }
  const stringResult = /Result\s*<[^>]*,\s*string\s*>/g
  for (let match = stringResult.exec(code); match !== null; match = stringResult.exec(code)) {
    violations.push(violation('session-string-rejection', file, source, match.index, match[0].replace(/\s+/g, ' ')))
  }
  const bindOnceShape = /let\s+bindOnce\b/.test(code) &&
    /true\s*,\s*existing\s+when\s+existing\s*=\s*persona\s*->\s*Ok\s+existing/.test(compact) &&
    /true\s*,\s*existing\s*->\s*Error\b[^|]*existing[^|]*persona/.test(compact) &&
    /false\s*,\s*_\s*->[^|]*\[sessionId\][^|]*<-\s*persona[^|]*Ok\s+persona/.test(compact)
  if (!bindOnceShape) {
    violations.push(violation('session-bind-once', file, source,
      firstOffset(code, /let\s+bindOnce/), 'bindOnce must preserve the original SessionId-scoped Persona'))
  }
  return violations
}

/** Scan the Host adapter for swallowed typed binding conflicts. */
export const scanPersonaBindingSource = (file, source) => {
  const code = codeOnly(source)
  const match = /\|\s*Error\s+_\s*->[\s\S]{0,240}?SessionPersona\.tryGet[\s\S]{0,160}?(?:Option\.default|defaultValue|defaultWith)/m.exec(code)
  return match
    ? [violation('binding-conflict-swallowed', file, source, match.index,
        'PersonaBinding must propagate PersonaRejection conflicts')]
    : []
}

/** Scan Foundation/Roles.fs for office-capability vocabulary. */
export const scanFoundationRolesSource = (file, source) => {
  const code = codeOnly(source)
  const violations = []
  const forbidden = /\b(?:ToolPermission|permissions|isAllowed|RoleDefinition)\b/g
  for (let match = forbidden.exec(code); match !== null; match = forbidden.exec(code)) {
    violations.push(violation('foundation-role-capability', file, source, match.index, match[0]))
  }
  return violations
}

/** Scan any production F# caller for legacy or text-derived identity authority. */
export const scanProductionIdentitySource = (file, source) => {
  const code = codeOnly(source)
  const violations = []
  const legacy = /\bAgentRoleIdentity\s*\./g
  for (let match = legacy.exec(code); match !== null; match = legacy.exec(code)) {
    violations.push(violation('legacy-role-identity-reference', file, source, match.index, 'AgentRoleIdentity.'))
  }

  const lowercase = /PersonaCatalog\.(?:persona|render|canonical)\b[\s\S]{0,180}?\.ToLowerInvariant\s*\(\s*\)/g
  for (let match = lowercase.exec(code); match !== null; match = lowercase.exec(code)) {
    violations.push(violation('persona-lowercase-authority', file, source, match.index,
      textAt(source, match.index)))
  }

  const lines = code.split('\n')
  const rawLines = source.split('\n')
  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index]
    if (!/(?:persona|displayName|display_name)/i.test(line)) continue
    if (!/(?:\bif\b|\bwhen\b)[^\n]*(?:=|<>|String\.Equals)|String\.Equals\s*\(/.test(line)) continue
    violations.push({ id: OWNER, rule: 'persona-text-authority', file: normalizePath(file),
      line: index + 1, text: rawLines[index].trim() })
  }
  return violations
}

/** Scan injectable production entries. */
export const scanParticipantIdentityEntries = (entries) => {
  const byFile = new Map(entries.map(({ file, source }) => [normalizePath(file), source]))
  const violations = []
  if (byFile.has(CATALOG_FILE)) violations.push(...scanPersonaCatalogSource(CATALOG_FILE, byFile.get(CATALOG_FILE)))
  if (byFile.has(SESSION_FILE)) violations.push(...scanSessionPersonaSource(SESSION_FILE, byFile.get(SESSION_FILE)))
  if (byFile.has(BINDING_FILE)) violations.push(...scanPersonaBindingSource(BINDING_FILE, byFile.get(BINDING_FILE)))
  if (byFile.has(ROLES_FILE)) violations.push(...scanFoundationRolesSource(ROLES_FILE, byFile.get(ROLES_FILE)))
  if (byFile.has(LEGACY_FILE)) {
    violations.push({ id: OWNER, rule: 'legacy-role-identity-file', file: LEGACY_FILE, line: 1,
      text: 'RoleIdentity.fs must be absent' })
  }
  for (const { file, source } of entries) {
    const normalizedFile = normalizePath(file)
    if (normalizedFile === CATALOG_FILE || normalizedFile === SESSION_FILE) continue
    violations.push(...scanProductionIdentitySource(normalizedFile, source))
  }
  return violations.sort((left, right) => left.file.localeCompare(right.file) || left.line - right.line ||
    left.rule.localeCompare(right.rule) || left.text.localeCompare(right.text))
}

const productionFiles = (repoRoot) => {
  const root = resolve(repoRoot, SOURCE_ROOT)
  const visit = (directory) => readdirSync(directory, { withFileTypes: true })
    .sort((left, right) => left.name.localeCompare(right.name))
    .flatMap((entry) => {
      const absolute = resolve(directory, entry.name)
      if (entry.isDirectory()) return visit(absolute)
      return entry.isFile() && entry.name.endsWith('.fs') ? [absolute] : []
    })
  return visit(root)
}

/** Scan the fixed owners and all production identity callers. */
export const scanParticipantIdentityRepo = (repoRoot) => {
  for (const file of PARTICIPANT_IDENTITY_OWNER_FILES) {
    if (!existsSync(resolve(repoRoot, file))) throw new Error(`${OWNER}: scan owner missing: ${file}`)
  }
  const files = productionFiles(repoRoot)
  return scanParticipantIdentityEntries(files.map((absolute) => ({
    file: normalizePath(relative(repoRoot, absolute)),
    source: readFileSync(absolute, 'utf8'),
  })))
}

export const run = (repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..')) => {
  const violations = scanParticipantIdentityRepo(repoRoot)
  if (violations.length === 0) {
    console.log(`${OWNER}: OK`)
    return 0
  }
  console.error(`${OWNER}: VIOLATIONS`)
  for (const item of violations) console.error(`  ${item.file}:${item.line} [${item.rule}] ${item.text}`)
  return 1
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = run()
