#!/usr/bin/env node
// Provider-transform composition-root invariant. The exported scanner is pure;
// repository I/O and process exit live only in the guarded CLI.

import { readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

export const ROOT = fileURLToPath(new URL('../..', import.meta.url))
export const PLUGIN_TRANSFORMS_FILE = 'src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs'

export const ORDERING_STEPS = Object.freeze([
  'caps.BeginPhysicalProviderAttempt',
  'caps.BindSessionStartedAt',
  'caps.ApplyStrengthReplay',
  'caps.CaptureXTraceMessages',
  'caps.CommitStrengthTrace',
  'caps.RefreshCompanionXTrace',
  'caps.ApplyManagerNarrative',
  'caps.ApplyCompanion',
  'caps.ApplyXWire',
  'caps.ApplyEnforcerContinuation',
  'caps.ApplyStrengthSpeculate',
  'caps.InjectPairGuideline',
  'caps.ProjectRequirementGrounding',
  'caps.InjectBloggerChronicle',
  'caps.SanitizeMessages',
  'caps.InterruptAfterSubmittedJudgement',
])

const DYNAMIC_PIPELINE_PATTERNS = Object.freeze([
  /\bITransformMiddleware\b/,
  /\bITransform\b/,
  /\bpipeline\s*\.\s*(?:Insert|Add|Register|Remove)\b/,
  /\bList\.(?:map|iter)\s+apply\b/,
  /\bMiddlewarePipeline\b/,
  /\bDecoratorBase\b/,
  /\bIWorkflowDecorator\b/,
])

const FOREIGN_DECISION_HELPERS = Object.freeze([
  /\blet\s+private\s+decide[A-Z]\w*/,
  /\blet\s+private\s+recover[A-Z]\w*/,
  /\blet\s+private\s+classify[A-Z]\w*/,
  /\blet\s+private\s+calculate[A-Z]\w*/,
  /\blet\s+private\s+maintain[A-Z]\w*/,
])

const IMPLICIT_MODE_HELPERS = Object.freeze([
  /\blet\s+private\s+strengthReplicaRuntime\b/,
  /\blet\s+private\s+isExplicitResumeProviderMaterial\b/,
  /\blet\s+private\s+requireReplicaHandled\b/,
  /\blet\s+private\s+ordinaryProviderTransform\b/,
])

const executableText = (text) => {
  let blockDepth = 0
  let inString = false
  let lineComment = false
  let escaped = false
  let result = ''
  for (let index = 0; index < text.length; index++) {
    const character = text[index]
    const next = text[index + 1]
    if (character === '\n') {
      result += '\n'
      lineComment = false
      escaped = false
    } else if (lineComment) result += ' '
    else if (blockDepth > 0) {
      if (character === '(' && next === '*') {
        blockDepth++
        result += '  '
        index++
      } else if (character === '*' && next === ')') {
        blockDepth--
        result += '  '
        index++
      } else result += ' '
    } else if (inString) {
      result += ' '
      if (escaped) escaped = false
      else if (character === '\\') escaped = true
      else if (character === '"') inString = false
    } else if (character === '/' && next === '/') {
      lineComment = true
      result += '  '
      index++
    } else if (character === '(' && next === '*') {
      blockDepth = 1
      result += '  '
      index++
    } else if (character === '"') {
      inString = true
      result += ' '
    } else result += character
  }
  return result
}

const functionBody = (text, name) => {
  const lines = text.split('\n')
  const start = lines.findIndex((line) =>
    new RegExp(`^\\s*let\\s+(?:private\\s+)?${name}\\b`).test(line),
  )
  if (start < 0) return null
  const indent = lines[start].length - lines[start].trimStart().length
  let end = lines.length
  for (let i = start + 1; i < lines.length; i++) {
    if (lines[i].trim() === '') continue
    const nextIndent = lines[i].length - lines[i].trimStart().length
    if (nextIndent <= indent && /^\s*let\s/.test(lines[i])) {
      end = i
      break
    }
  }
  return { lines: lines.slice(start, end), startLine: start + 1 }
}

const indentation = (line) => line.length - line.trimStart().length

const enclosingHeaders = (lines, at) => {
  const headers = []
  let below = indentation(lines[at])
  for (let i = at - 1; i >= 0 && below > 0; i--) {
    if (!lines[i].trim() || indentation(lines[i]) >= below) continue
    headers.push(lines[i].trim())
    below = indentation(lines[i])
  }
  return headers
}

const isNormalPathCall = (lines, at, callColumn, stepIndex) => {
  const beforeCall = lines[at].slice(0, callColumn)
  if (/\bfun\b[^\n]*->/.test(beforeCall)) return false
  if (/^\s*let(?!\s*!)[^=]*=/.test(beforeCall)) return false

  for (const header of enclosingHeaders(lines, at)) {
    if (/^(?:fun\b|function\b|\|)/.test(header)) return false
    if (/^(?:if|elif|else\b|match!?\b|try\b|with\b|for\b|while\b)/.test(header)) {
      const canonicalPrefixBranch = header === 'if prefixHorizon = PrefixPresentationHorizon.Current then'
        && [
          'caps.ApplyStrengthSpeculate',
          'caps.InjectPairGuideline',
          'caps.ProjectRequirementGrounding',
        ].includes(ORDERING_STEPS[stepIndex])
      if (!canonicalPrefixBranch) return false
    }
    if (/^let\b/.test(header) && !/^let\s+normalTransform\b/.test(header)) return false
  }
  return true
}

/** @returns {{file:string,kind:string,line?:number,message:string}[]} */
export const scanPluginTransforms = (text, file = PLUGIN_TRANSFORMS_FILE) => {
  const violations = []
  const add = (kind, message, line) => violations.push({ file, kind, line, message })
  const executable = executableText(text)

  for (const pattern of DYNAMIC_PIPELINE_PATTERNS) {
    if (pattern.test(executable)) add('dynamic-pipeline', `dynamic pipeline pattern: ${pattern}`)
  }
  for (const pattern of FOREIGN_DECISION_HELPERS) {
    if (pattern.test(executable)) add('foreign-decision', `foreign domain decision helper: ${pattern}`)
  }
  for (const pattern of IMPLICIT_MODE_HELPERS) {
    if (pattern.test(executable)) add('implicit-mode', `implicit mode helper: ${pattern}`)
  }

  const body = functionBody(executable, 'normalTransform')
  if (body === null) {
    add('ordering', 'normalTransform function not found')
  } else {
    let prior = -1
    for (let i = 0; i < ORDERING_STEPS.length; i++) {
      const escaped = ORDERING_STEPS[i].replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
      const call = new RegExp(`\\b${escaped}\\b\\s+(?=[A-Za-z_(])`, 'g')
      const occurrences = body.lines.flatMap((line, at) => {
        const found = []
        for (let match; (match = call.exec(line)) !== null;) {
          if (isNormalPathCall(body.lines, at, match.index, i)) found.push({ at, offset: at * 1_000_000 + match.index })
        }
        return found
      })
      const at = occurrences[0]?.at ?? -1
      const offset = occurrences[0]?.offset ?? -1
      if (occurrences.length === 0) {
        add('ordering', `ordering step ${i + 1} not found: ${ORDERING_STEPS[i]}`)
      } else if (occurrences.length !== 1) {
        add('ordering', `ordering step ${i + 1} must execute exactly once: ${ORDERING_STEPS[i]}`, body.startLine + at)
      } else if (offset <= prior) {
        add('ordering', `ordering step ${i + 1} is not after step ${i}: ${ORDERING_STEPS[i]}`, body.startLine + at)
      }
      if (offset >= 0) prior = offset
    }
  }

  if (!/\btype\s+private\s+TransformMode\b/.test(executable)) {
    add('typed-mode', 'missing private TransformMode DU')
  }
  if (!/\blet\s+private\s+determineTransformMode\b/.test(executable)) {
    add('typed-mode', 'missing determineTransformMode')
  }
  if (!/\bmatch\s+determineTransformMode\b/.test(executable)) {
    add('typed-mode', 'missing explicit match on determineTransformMode')
  }
  for (const mode of ['ExplicitResumeDisclosure', 'StrengthReplica', 'Ordinary']) {
    if (!executable.includes(mode)) add('typed-mode', `TransformMode case not wired: ${mode}`)
  }

  return violations
}

export const scanRepo = (root = ROOT) => {
  const file = join(root, PLUGIN_TRANSFORMS_FILE)
  return scanPluginTransforms(readFileSync(file, 'utf8'), PLUGIN_TRANSFORMS_FILE)
}

const runCli = () => {
  const violations = scanRepo()
  if (violations.length > 0) {
    console.error('plugin-transforms-invariant: VIOLATIONS')
    for (const violation of violations) console.error(`  ${violation.file}${violation.line ? `:${violation.line}` : ''} ${violation.message}`)
    process.exit(1)
  }
  console.log('plugin-transforms-invariant: OK — static typed composition and ordering')
}

const isMain = process.argv[1] !== undefined && resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])
if (isMain) runCli()
