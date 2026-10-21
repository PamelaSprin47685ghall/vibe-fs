#!/usr/bin/env node
// Generates the TS mirror of the host-neutral ports from the F# signature files.
//
// Source of truth: src/Wanxiangshu/Host/Ports/*.fsi (HNI P-00..P-12), plus the two
// foundation files the ports depend on:
//   src/Wanxiangshu/Foundation/Identity.fsi  (opaque ids: SessionId, ToolCallId, …)
//   src/Wanxiangshu/Foundation/Outcome.fsi  (Outcome.SendOutcome)
//
// The generated file is proposals/host-neutral-interface/adapters/omp/ports.ts.
// mirror-check.mjs regenerates it and fails on ANY byte difference, so the TS mirror
// is mechanically derived from the F# contract — not hand-written, and not merely
// name-compared.
//
// Mapping rules (mechanical; see docs and the generated header):
//   F# record            -> TS interface, field order preserved
//   F# `T option` field  -> TS optional property `name?: T`
//   F# DU, >=2 cases     -> TS tagged union `{ tag: "Case", field: T }`
//   F# single-case string DU, and Foundation's `private X of string` -> opaque
//                            branded TS string
//   F# interface member with `->` -> TS method; without -> readonly property
//   F# type alias with `->` -> TS function type
//   F# `Task`/`Task<X>`  -> `Promise<void>` / `Promise<X>`
//   F# `HostResult<'T>`  -> `HostResult<T>`
//   F# `Result<'T,'E>`   -> `Result<T, E>`
//   F# `list`            -> `T[]`
//   F# `Map<K,V>`        -> `Record<K, V>`
//   F# `Set<T>`          -> `ReadonlySet<T>`
//   F# `unit`            -> `void`; `int`/`float` -> `number`; `string` -> `string`;
//                            `bool` -> `boolean`
//
// Two F# types have no direct TypeScript equivalent. The mapping for them is a
// deliberate adapter binding, stated here so it is never mistaken for a mechanical
// equivalence:
//   System.Threading.CancellationToken -> AbortSignal   (host cancellation handle)
//   System.IDisposable                 -> IDisposableHandle ({ dispose(): void })
import { readdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const repoRoot = resolve(here, '../../../..')
const portRoot = resolve(repoRoot, 'src/Wanxiangshu/Host/Ports')

// Port families are DISCOVERED from the sources, never listed here: every
// src/Wanxiangshu/Host/Ports/*.fsi is a family, and its reserved port id is the
// `HNI P-xx` marker in its header. A family another lane lands is therefore
// mirrored automatically — it cannot silently escape the TS mirror.
const familyIdPattern = /\bHNI (P-\d{2})\b/

export function discoverPortFamilies() {
  const files = readdirSync(portRoot).filter((name) => name.endsWith('.fsi')).sort()
  const families = files.map((name) => {
    const text = readFileSync(resolve(portRoot, name), 'utf8')
    const marker = text.match(familyIdPattern)
    if (!marker) {
      throw new Error(`port family ${name} declares no reserved port id: add an \`HNI P-xx\` marker to its header`)
    }
    return { family: name.replace(/\.fsi$/, ''), id: marker[1] }
  })
  families.sort((a, b) => a.id.localeCompare(b.id))
  return families
}

// Foundation ids the ports use, mapped to opaque branded TS strings.
const FOUNDATION_OPAQUE = [
  'SessionId', 'ToolCallId', 'PromptKey', 'TranscriptMessageAddress',
  'TransportReceipt', 'PhysicalUserMessageId',
]

const PRIMITIVES = {
  unit: 'void', string: 'string', bool: 'boolean', int: 'number', int32: 'number',
  int64: 'number', float: 'number', double: 'number', obj: 'unknown',
}

// Host bindings for F# types with no TypeScript equivalent (documented in the header).
const HOST_BINDINGS = {
  CancellationToken: 'AbortSignal',
  IDisposable: 'IDisposableHandle',
}

// ---------------------------------------------------------------- splitting helpers

const splitTop = (text, separator) => {
  const parts = []
  let depth = 0
  let current = ''
  for (let i = 0; i < text.length; i++) {
    const ch = text[i]
    // `->` is a function arrow, not a closing angle bracket.
    if (ch === '-' && text[i + 1] === '>') { current += '->'; i++; continue }
    if (ch === '<' || ch === '(' || ch === '[') depth++
    else if (ch === '>' || ch === ')' || ch === ']') depth--
    if (ch === separator && depth === 0) { parts.push(current); current = ''; continue }
    current += ch
  }
  parts.push(current)
  return parts.map((p) => p.trim()).filter((p) => p.length > 0)
}

const splitArrow = (text) => {
  const parts = []
  let depth = 0
  let current = ''
  for (let i = 0; i < text.length; i++) {
    const ch = text[i]
    if (ch === '-' && text[i + 1] === '>') {
      if (depth === 0) { parts.push(current.trim()); current = ''; i++; continue }
      current += '->'; i++
      continue
    }
    if (ch === '<' || ch === '(' || ch === '[') depth++
    else if (ch === '>' || ch === ')' || ch === ']') depth--
    current += ch
  }
  parts.push(current.trim())
  return parts
}

/**
 * Map an F# type expression to a TS type.
 * Returns { ts, optional } where `optional` means the F# type was `T option`.
 */
export function fsType(text) {
  const s = text.trim()
  if (!s) return { ts: 'unknown', optional: false }

  // function type: `a -> b`
  const arrow = splitArrow(s)
  if (arrow.length === 2) {
    const argument = fsType(arrow[0])
    const result = fsType(arrow[1])
    const parameters = argument.ts === 'void' ? '' : `arg: ${argument.ts}`
    return { ts: `(${parameters}) => ${result.ts}`, optional: false }
  }

  if (s.startsWith('(') && s.endsWith(')')) {
    const inner = s.slice(1, -1)
    const innerArrow = splitArrow(inner)
    if (innerArrow.length === 2) return fsType(inner)
    const tuple = splitTop(inner, '*')
    if (tuple.length > 1) return { ts: `[${tuple.map((p) => fsType(p).ts).join(', ')}]`, optional: false }
    return fsType(inner)
  }
  // `T option` and `T list` suffixes bind looser than a generic application, so they
  // are matched before the generic form.
  if (s.endsWith(' option')) {
    const inner = fsType(s.slice(0, -' option'.length))
    return { ts: inner.ts, optional: true }
  }
  if (s.endsWith(' list')) return { ts: `${fsType(s.slice(0, -' list'.length)).ts}[]`, optional: false }
  const generic = s.match(/^([A-Za-z]\w*)<(.+)>$/)
  if (generic) {
    const name = generic[1]
    const args = splitTop(generic[2], ',').map((a) => fsType(a))
    // `T option` inside a generic argument is `T | undefined` in TypeScript.
    const argType = (a) => (a.optional ? `${a.ts} | undefined` : a.ts)
    if (name === 'Map') return { ts: `Record<${args.map(argType).join(', ')}>`, optional: false }
    if (name === 'Set') return { ts: `ReadonlySet<${args.map(argType).join(', ')}>`, optional: false }
    if (name === 'Task') {
      if (args.length !== 1) throw new Error(`unsupported Task arity: ${s}`)
      return { ts: `Promise<${argType(args[0])}>`, optional: false }
    }
    if (name === 'Result') {
      if (args.length !== 2) throw new Error(`unsupported Result arity: ${s}`)
      return { ts: `Result<${args.map(argType).join(', ')}>`, optional: false }
    }
    if (name === 'HostResult') {
      if (args.length !== 1) throw new Error(`unsupported HostResult arity: ${s}`)
      return { ts: `HostResult<${argType(args[0])}>`, optional: false }
    }
    throw new Error(`unsupported generic type: ${s}`)
  }

  if (s === 'Task') return { ts: 'Promise<void>', optional: false }
  if (s.startsWith("'")) return { ts: s.slice(1), optional: false }
  if (s in HOST_BINDINGS) return { ts: HOST_BINDINGS[s], optional: false }
  if (s in PRIMITIVES) return { ts: PRIMITIVES[s], optional: false }
  return { ts: s, optional: false }
}

/** Render a field list from F# `name: type` entries. */
function parseFields(body) {
  // F# separates record fields with `;` (or newlines); the mirror keeps either.
  const entries = splitTop(body, ';').flatMap((chunk) => splitTop(chunk, ','))
  return entries.map((entry) => {
    const colon = entry.indexOf(':')
    if (colon < 0) throw new Error(`unparsable field: ${entry}`)
    const name = entry.slice(0, colon).trim()
    const mapped = fsType(entry.slice(colon + 1))
    return { name, ts: mapped.ts, optional: mapped.optional }
  })
}

/**
 * Parse a union case payload. F# payloads are either
 *   `a: t * b: t`  (named fields)  or  `t` (a single unnamed payload).
 */
function parseCaseFields(text) {
  const parts = splitTop(text, '*')
  if (parts.length === 1) {
    const trimmed = parts[0].trim()
    if (!trimmed) return []
    const colon = trimmed.indexOf(':')
    if (colon < 0) {
      // unnamed payload: the case carries one value of this type
      const mapped = fsType(trimmed)
      return [{ name: 'value', ts: mapped.ts, optional: mapped.optional, unnamed: true }]
    }
    return parseFields(trimmed)
  }
  return parts.map((part) => {
    const colon = part.indexOf(':')
    if (colon < 0) throw new Error(`unparsable union field: ${part}`)
    const name = part.slice(0, colon).trim()
    const mapped = fsType(part.slice(colon + 1))
    return { name, ts: mapped.ts, optional: mapped.optional }
  })
}

// ---------------------------------------------------------------- F# signature parsing

/** Parse `Name: Type` field lists (records) into [{ name, ts, optional }]. */
export function parseFsi(text) {
  const lines = text.split('\n').map((l) => l.replace(/\/\/.*$/, ''))
  const decls = []
  let current = null
  const flush = () => { if (current) decls.push(current); current = null }
  for (const raw of lines) {
    const line = raw.replace(/\s+$/, '')
    if (!line.trim()) continue
    // Types may be nested inside a `module X =` block, so leading indentation is
    // allowed on the `type` keyword.
    const typeStart = line.match(/^\s*type\s+([A-Za-z]\w*)(<[^>]*>)?\s*=\s*(.*)$/)
    if (typeStart) {
      flush()
      const name = typeStart[1]
      const generics = (typeStart[2] ?? '').replace(/[<>]/g, '').split(',').map((g) => g.trim()).filter(Boolean)
      const rest = typeStart[3].trim()
      current = { name, generics, kind: 'alias', body: rest }
      if (rest === '{' || rest.startsWith('{ ')) current.kind = 'record'
      continue
    }
    if (!current) continue
    const indented = /^\s+/.test(line)
    if (!indented) { flush(); continue }
    const trimmed = line.trim()
    if (current.kind === 'alias' && current.body === '') current.body = trimmed
    else current.body += `\n${trimmed}`
  }
  flush()
  return decls.map(normaliseDecl)
}

function normaliseDecl(decl) {
  const body = decl.body.trim()
  if (decl.generics.length > 0) return { ...decl, kind: 'alias', alias: body }
  if (body.startsWith('{')) {
    const inner = body.replace(/^\{/, '').replace(/\}$/, '')
    // F# record fields are separated by `;` and/or newlines.
    const flat = inner.replace(/\n\s*/g, ';').replace(/;\s*$/, '')
    return { ...decl, kind: 'record', fields: parseFields(flat) }
  }
  if (body.includes('abstract ')) {
    const members = body.split('\n').map((l) => l.trim()).filter((l) => l.startsWith('abstract '))
    return { ...decl, kind: 'interface', members: members.map(parseAbstract) }
  }
  if (body.includes(' of ') || body.split('\n').some((l) => l.trim().startsWith('|'))) {
    const cases = []
    const source = body.split('\n').map((l) => l.trim()).filter(Boolean)
    for (const line of source) {
      const stripped = line.replace(/^\|\s*/, '')
      const m = stripped.match(/^([A-Z]\w*)\s*(?:of\s+(.*))?$/)
      if (!m) continue
      const fields = m[2] ? parseCaseFields(m[2]) : []
      cases.push({ name: m[1], fields })
    }
    return { ...decl, kind: 'union', cases }
  }
  return { ...decl, kind: 'alias', alias: body }
}

function parseAbstract(line) {
  const text2 = line.replace(/^abstract\s+/, '')
  const colon = text2.indexOf(':')
  const name = text2.slice(0, colon).trim()
  const signature = text2.slice(colon + 1).trim()
  const arrow = splitArrow(signature)
  if (arrow.length === 1) {
    const mapped = fsType(signature)
    return { name, kind: 'property', ts: mapped.ts }
  }
  const parameters = []
  if (arrow[0].trim() !== 'unit') {
    for (const part of splitTop(arrow[0], '*')) {
      const c = part.indexOf(':')
      if (c < 0) throw new Error(`unparsable parameter: ${part}`)
      const pname = part.slice(0, c).trim()
      const mapped = fsType(part.slice(c + 1))
      parameters.push({ name: pname, ts: mapped.ts, optional: mapped.optional })
    }
  }
  return { name, kind: 'method', parameters, returns: fsType(arrow[1]).ts }
}

// ---------------------------------------------------------------- emitting TS

const opaque = (name) => `export type ${name} = string & { readonly __brand: "${name}" };\n/** Construct a ${name} from its underlying string. */\nexport const ${lowerFirst(name)} = (value: string): ${name} => value as ${name};`

const lowerFirst = (name) => name.charAt(0).toLowerCase() + name.slice(1)

function emitDecl(decl) {
  if (decl.kind === 'record') {
    const lines = decl.fields.map((f) => `  ${f.name}${f.optional ? '?' : ''}: ${f.ts}`)
    return `export interface ${decl.name} {\n${lines.join('\n')}\n}`
  }
  if (decl.kind === 'interface') {
    const lines = decl.members.map((m) => {
      if (m.kind === 'property') return `  readonly ${m.name}: ${m.ts}`
      // An F# `option` parameter is optional in TS only when it is the last one;
      // otherwise TypeScript forbids an optional parameter before a required one.
      const parameters = m.parameters.map((p, index) => {
        const last = index === m.parameters.length - 1
        if (!p.optional) return `${p.name}: ${p.ts}`
        return last ? `${p.name}?: ${p.ts}` : `${p.name}: ${p.ts} | undefined`
      }).join(', ')
      return `  ${m.name}(${parameters}): ${m.returns}`
    })
    return `export interface ${decl.name} {\n${lines.join('\n')}\n}`
  }
  if (decl.kind === 'union') {
    // A single-case DU over a string is an opaque value in F#; mirror it as one.
    if (decl.cases.length === 1) {
      const only = decl.cases[0]
      if (only.fields.length === 1 && only.fields[0].ts === 'string' && !only.fields[0].optional) {
        return opaque(decl.name)
      }
    }
    // A DU whose cases all carry no payload is a set of names; mirror it as a
    // string-literal union so comparisons stay plain equality.
    const allPayloadLess = decl.cases.every((c) => c.fields.length === 0)
    if (allPayloadLess) {
      const names = decl.cases.map((c) => `  | "${c.name}"`).join('\n')
      return `export type ${decl.name} =\n${names};`
    }
    // Otherwise every case is tagged, so cases with no payload still carry a tag.
    const cases = decl.cases.map((c) => {
      const fields = c.fields.map((f) => `; ${f.name}${f.optional ? '?' : ''}: ${f.ts}`).join('')
      return `  | { tag: "${c.name}"${fields} }`
    })
    return `export type ${decl.name} =\n${cases.join('\n')};`
  }
  // alias
  const alias = (decl.alias ?? '').trim()
  const arrow = splitArrow(alias)
  if (arrow.length === 2) {
    const argument = fsType(arrow[0])
    const result = fsType(arrow[1])
    const parameter = argument.ts === 'void' ? '' : `arg: ${argument.ts}`
    return `export type ${decl.name} = (${parameter}) => ${result.ts};`
  }
  if (decl.generics.length > 0) {
    const params = decl.generics.map((g) => g.replace(/'/g, '')).join(', ')
    const body = fsType(alias).ts.replace(/<'(\w+)>/g, '<$1>')
    return `export type ${decl.name}<${params}> = ${body};`
  }
  return `export type ${decl.name} = ${fsType(alias).ts};`
}

// ---------------------------------------------------------------- assembly

function readFoundationOpaqueNames() {
  const identity = readFileSync(resolve(repoRoot, 'src/Wanxiangshu/Foundation/Identity.fsi'), 'utf8')
  const names = new Set()
  for (const m of identity.matchAll(/^\s*type\s+([A-Za-z]\w*)\s*=\s*private\s+\1\s+of\s+string\s*$/gm)) names.add(m[1])
  return names
}

export function generate() {
  const opaqueFoundation = readFoundationOpaqueNames()
  const families = discoverPortFamilies()
  const sections = []
  const emitted = new Set()

  const outcomeText = readFileSync(resolve(repoRoot, 'src/Wanxiangshu/Foundation/Outcome.fsi'), 'utf8')
  const outcomeModule = outcomeText.slice(outcomeText.indexOf('module Outcome ='))
  const outcomeDecls = parseFsi(outcomeModule).filter((d) => d.name === 'SendOutcome')

  for (const { family, id } of families) {
    const text = readFileSync(resolve(portRoot, `${family}.fsi`), 'utf8')
    const decls = parseFsi(text)
    const blocks = []
    for (const decl of decls) {
      if (emitted.has(decl.name)) continue
      emitted.add(decl.name)
      blocks.push(emitDecl(decl))
    }
    if (family === 'Lifecycle') {
      for (const decl of outcomeDecls) {
        if (emitted.has(decl.name)) continue
        emitted.add(decl.name)
        blocks.push(emitDecl(decl))
      }
    }
    sections.push({ family, id, blocks })
  }

  const foundationOpaque = FOUNDATION_OPAQUE
    .filter((name) => opaqueFoundation.has(name))
    .map((name) => opaque(name))

  const header = [
    '// GENERATED FILE — do not edit by hand.',
    `// Source of truth: src/Wanxiangshu/Host/Ports/*.fsi (HNI ${families.map((f) => f.id).join(', ')}), plus`,
    '// src/Wanxiangshu/Foundation/{Identity,Outcome}.fsi for the opaque ids and',
    '// Outcome.SendOutcome.',
    '// The port families are discovered from the sources, so a family another lane lands shows up',
    '// here and the mirror check fails until the mirror is regenerated.',
    '// Regenerate:  node proposals/host-neutral-interface/adapters/omp/gen-ports.mjs --write',
    '// Verify:      node proposals/host-neutral-interface/adapters/omp/mirror-check.mjs',
    '//',
    '// Mapping (mechanical, see gen-ports.mjs):',
    '//   F# record -> TS interface; `T option` field -> optional property',
    '//   F# DU (>=2 cases) -> tagged union { tag: "Case", field: T }',
    '//   F# single-case string DU, and Foundation `private X of string` -> opaque string',
    '//   F# abstract member -> TS method; abstract property -> readonly property',
    '//   F# alias with `->` -> TS function type',
    '//   Task -> Promise; HostResult<T> -> HostResult<T>; Result<T,E> -> Result<T,E>;',
    '//   list -> []; Map -> Record; Set -> ReadonlySet; unit -> void; int/float -> number',
    '//',
    '// Host bindings (deliberate, not mechanical):',
    '//   System.Threading.CancellationToken -> AbortSignal',
    '//   System.IDisposable -> IDisposableHandle',
    '',
    'export type Result<T, E> = { ok: true; value: T } | { ok: false; error: E };',
    'export interface IDisposableHandle { dispose(): void }',
    '',
  ].join('\n')

  const body = sections.map((s) => `// ---- ${s.id} ${s.family}\n${s.blocks.join('\n\n')}`).join('\n\n')

  return `${header}${foundationOpaque.join('\n')}\n\n${body}\n`
}

export const GENERATED_PATH = resolve(here, 'ports.ts')

if (process.argv[1] && resolve(process.argv[1]) === resolve(fileURLToPath(import.meta.url))) {
  const text = generate()
  if (process.argv.includes('--write')) {
    writeFileSync(GENERATED_PATH, text)
    console.log(`ports.ts written: ${text.split('\n').length} lines`)
  } else {
    process.stdout.write(text)
  }
}
