#!/usr/bin/env node
/**
 * G9 capability-isomorphism static ratchet (no dist).
 *
 * Fail-closed on source-level isomorphism contracts:
 *   1. ToolRegistry.fs projects js-* only via JsToolGenerator.generate
 *   2. JsTools.fs JsFragmentRegistry declares the four member caps:
 *      read / glob / rewrite / write (grep is Read+Glob derived, not a member)
 *   3. tests/unit/js-tools/js-surface.test.mjs keeps JS004 / layersOf /
 *      memberBinding layer tokens
 *   4. Roles.fs has no Student / Teacher role surface
 *
 * Usage: node scripts/checks/capability-isomorphism-gate.mjs
 */

import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

export const TOOL_REGISTRY_REL = 'src/Wanxiangshu/Infrastructure/OpenCode/Tools/ToolRegistry.fs'
export const JS_TOOLS_REL = 'src/Wanxiangshu/Domain/JsTools.fs'
export const JS_SURFACE_TEST_REL = 'tests/unit/js-tools/js-surface.test.mjs'
export const ROLES_REL = 'src/Wanxiangshu/Kernel/Roles.fs'

/** Member fragments — Grep is a derived example, not a js-* member. */
export const REQUIRED_FRAGMENT_CAPS = Object.freeze([
  'read',
  'glob',
  'rewrite',
  'write',
])

/** Layer-exactness tokens that must remain in js-surface unit tests. */
export const REQUIRED_SURFACE_TEST_TOKENS = Object.freeze([
  'JS004',
  'layersOf',
  'memberBinding',
])

/** Role surface that must never return (G3 rebase debt). */
export const FORBIDDEN_ROLE_TOKENS = Object.freeze(['Student', 'Teacher'])

/**
 * @typedef {{
 *   code: string,
 *   path?: string,
 *   detail?: string,
 * }} Violation
 */

/**
 * ToolRegistry must generate js-* specs exclusively through JsToolGenerator.generate
 * (no handwritten js-* ToolSpec path beside the generator loop).
 * @param {string} text
 * @returns {Violation[]}
 */
export const scanToolRegistry = (text) => {
  /** @type {Violation[]} */
  const violations = []
  if (!text.includes('JsToolGenerator.generate')) {
    violations.push({
      code: 'missing-js-tool-generator',
      path: TOOL_REGISTRY_REL,
      detail: 'ToolRegistry must project js-* via JsToolGenerator.generate',
    })
  }

  // Handwritten ToolSpec / create paths that name a literal js-* role tool.
  const handwritten =
    /\b(?:JsToolSpec\.create|ToolSpec\.create|name\s*=\s*)[^;\n]*["']js-(?:coder|inspector|reviewer|devops|browser|meditator|student|teacher)["']/i
  if (handwritten.test(text)) {
    violations.push({
      code: 'handwritten-js-tool-spec',
      path: TOOL_REGISTRY_REL,
      detail: 'js-* ToolSpec must not be handwritten; only JsToolGenerator.generate',
    })
  }

  // A second generator / alternate factory for js tools is also a break.
  if (/\bJsTool(?:s)?(?:Factory|Builder|Registry)\.(?:create|build|register)\b/.test(text)) {
    violations.push({
      code: 'alternate-js-tool-factory',
      path: TOOL_REGISTRY_REL,
      detail: 'only JsToolGenerator.generate may produce js-* surfaces',
    })
  }

  return violations
}

/**
 * JsFragmentRegistry must expose exactly the five named fragment bindings.
 * @param {string} text
 * @returns {Violation[]}
 */
export const scanJsFragmentRegistry = (text) => {
  /** @type {Violation[]} */
  const violations = []

  if (!/\bmodule\s+JsFragmentRegistry\b/.test(text)) {
    violations.push({
      code: 'missing-js-fragment-registry',
      path: JS_TOOLS_REL,
      detail: 'JsTools.fs must declare module JsFragmentRegistry',
    })
    return violations
  }

  for (const cap of REQUIRED_FRAGMENT_CAPS) {
    // `let read:` / `let glob:` style bindings on the registry module.
    const binding = new RegExp(`\\blet\\s+${cap}\\s*:`)
    if (!binding.test(text)) {
      violations.push({
        code: 'missing-fragment-cap',
        path: JS_TOOLS_REL,
        detail: `JsFragmentRegistry missing let ${cap}: binding`,
      })
    }
  }

  // `let all` should list the five caps so isomorphism stays closed.
  const allMatch = text.match(/\blet\s+all\s*:\s*[^=]*=\s*\[([^\]]*)\]/)
  if (!allMatch) {
    violations.push({
      code: 'missing-fragment-all',
      path: JS_TOOLS_REL,
      detail: 'JsFragmentRegistry.let all list is required',
    })
  } else {
    const body = allMatch[1]
    for (const cap of REQUIRED_FRAGMENT_CAPS) {
      if (!new RegExp(`\\b${cap}\\b`).test(body)) {
        violations.push({
          code: 'fragment-all-incomplete',
          path: JS_TOOLS_REL,
          detail: `JsFragmentRegistry.all must include ${cap}`,
        })
      }
    }
  }

  return violations
}

/**
 * js-surface tests must retain four-layer exactness tokens.
 * @param {string} text
 * @returns {Violation[]}
 */
export const scanJsSurfaceTest = (text) => {
  /** @type {Violation[]} */
  const violations = []
  for (const token of REQUIRED_SURFACE_TEST_TOKENS) {
    if (!text.includes(token)) {
      violations.push({
        code: 'missing-surface-token',
        path: JS_SURFACE_TEST_REL,
        detail: `js-surface.test.mjs must contain token '${token}'`,
      })
    }
  }
  return violations
}

/**
 * Roles.fs must not revive Student/Teacher as Role cases or identifiers.
 * @param {string} text
 * @returns {Violation[]}
 */
export const scanRoles = (text) => {
  /** @type {Violation[]} */
  const violations = []
  const lines = text.split('\n')
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    // Skip pure doc-comments that mention historical debt only if they don't
    // declare cases — still forbid bare Role surface identifiers.
    for (const token of FORBIDDEN_ROLE_TOKENS) {
      // Role case: `| Student` / `| Teacher`
      if (new RegExp(`\\|\\s*${token}\\b`).test(line)) {
        violations.push({
          code: 'forbidden-role',
          path: ROLES_REL,
          detail: `line ${i + 1}: Role case '${token}' is forbidden`,
        })
        continue
      }
      // Role.Student / Role.Teacher / type refs
      if (new RegExp(`\\bRole\\.${token}\\b`).test(line)) {
        violations.push({
          code: 'forbidden-role',
          path: ROLES_REL,
          detail: `line ${i + 1}: Role.${token} is forbidden`,
        })
        continue
      }
      // Standalone identifier used as a role name string or binding.
      if (new RegExp(`\\b${token}\\b`).test(line) && !/^\s*\/\//.test(line)) {
        // Allow historical mentions only inside block/line comments already handled;
        // triple-slash docs still count as surface revival if they name the role.
        if (/^\s*\/\/\//.test(line) || /^\s*\(\*/.test(line)) continue
        violations.push({
          code: 'forbidden-role',
          path: ROLES_REL,
          detail: `line ${i + 1}: token '${token}' must not appear in Roles.fs`,
        })
      }
    }
  }
  return violations
}

/**
 * @param {{
 *   toolRegistry?: string,
 *   jsTools?: string,
 *   jsSurfaceTest?: string,
 *   roles?: string,
 * }} texts
 * @returns {{ ok: boolean, violations: Violation[] }}
 */
export const scanTexts = (texts) => {
  /** @type {Violation[]} */
  const violations = []
  if (typeof texts.toolRegistry === 'string') {
    violations.push(...scanToolRegistry(texts.toolRegistry))
  }
  if (typeof texts.jsTools === 'string') {
    violations.push(...scanJsFragmentRegistry(texts.jsTools))
  }
  if (typeof texts.jsSurfaceTest === 'string') {
    violations.push(...scanJsSurfaceTest(texts.jsSurfaceTest))
  }
  if (typeof texts.roles === 'string') {
    violations.push(...scanRoles(texts.roles))
  }
  return { ok: violations.length === 0, violations }
}

/**
 * Read production paths relative to repoRoot and scan.
 * @param {string} [repoRoot]
 */
export const scanRepo = (repoRoot = process.cwd()) => {
  /** @type {Violation[]} */
  const violations = []
  /** @type {Record<string, string>} */
  const texts = {}

  const load = (rel, key) => {
    const abs = resolve(repoRoot, rel)
    if (!existsSync(abs)) {
      violations.push({
        code: 'missing-file',
        path: rel,
        detail: `required file does not exist: ${rel}`,
      })
      return
    }
    texts[key] = readFileSync(abs, 'utf8')
  }

  load(TOOL_REGISTRY_REL, 'toolRegistry')
  load(JS_TOOLS_REL, 'jsTools')
  load(JS_SURFACE_TEST_REL, 'jsSurfaceTest')
  load(ROLES_REL, 'roles')

  if (violations.length > 0) {
    return { ok: false, violations }
  }

  return scanTexts(texts)
}

const formatViolation = (v) => {
  const loc = v.path ? v.path : 'capability-isomorphism'
  const detail = v.detail ? ` — ${v.detail}` : ''
  return `  ${loc}: ${v.code}${detail}`
}

const runCli = () => {
  const result = scanRepo()
  if (result.ok) {
    console.log(
      'capability-isomorphism-gate: OK — ToolRegistry→JsToolGenerator.generate; ' +
        `JsFragmentRegistry={${REQUIRED_FRAGMENT_CAPS.join(',')}}; ` +
        `js-surface tokens={${REQUIRED_SURFACE_TEST_TOKENS.join(',')}}; Roles has no Student/Teacher`,
    )
    process.exit(0)
  }

  console.error(
    `capability-isomorphism-gate: ${result.violations.length} violation(s)\n`,
  )
  for (const v of result.violations) {
    console.error(formatViolation(v))
  }
  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
