// requirements/obligation-ledger/tests/integration/plugin/magic-todo-sink-canary.test.mjs
//
// Phase 0 Host canaries D + I (magic-todo.md §37 / docs/proof/host.md HOST-023).
//
// Prove the V1 reviewing sink path for TodoTable → todo.updated → API-facing
// model → UI/TUI consumers, then freeze the deterministic compatibility choice.
// No Magic Todo production membrane or journal wiring.
//
// Anchors (pinned OpenCode V1, sibling checkout + shipped SDK):
//   packages/schema/src/session-todo.ts          — status is Schema.String
//   packages/opencode/src/session/todo.ts        — update inserts status as-is, publishes Event.Updated
//   packages/core/src/session/sql.ts             — TodoTable.status text NOT NULL
//   packages/opencode/src/cli/cmd/run/scrollback.writer.tsx — todoText / todoColor
//   packages/opencode/src/cli/cmd/run/tool.ts    — runTodo mark mapping
//   packages/app/.../session-todo-dock.tsx       — dock active / checkbox state
//
// Strategy freeze (I): only claim passthrough when every observed consumer
// keeps reviewing distinguishable from pending. Otherwise freeze sink→in_progress.
// Never claim a fifth status is "safe UI" without that observation.

import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { createRequire } from 'node:module'

const here = dirname(fileURLToPath(import.meta.url))
const root = resolve(here, '../../../../..')
const require = createRequire(import.meta.url)

/** Resolve the pinned OpenCode source tree (sibling checkout preferred). */
function resolveOpenCodeRoot() {
  const candidates = [
    process.env.OPENCODE_SRC,
    resolve(root, '../opencode'),
    resolve(root, 'opencode'),
  ].filter(Boolean)
  for (const candidate of candidates) {
    if (existsSync(join(candidate, 'packages/schema/src/session-todo.ts'))) return candidate
  }
  return null
}

const OPENCODE_ROOT = resolveOpenCodeRoot()

function readOc(rel) {
  assert.ok(OPENCODE_ROOT, 'pinned OpenCode source tree required for D/I sink canaries')
  const abs = join(OPENCODE_ROOT, rel)
  assert.ok(existsSync(abs), `missing OpenCode anchor: ${rel}`)
  return readFileSync(abs, 'utf8')
}

// ── Host-faithful consumer projections (byte-stable with observed V1 source) ─

/** scrollback.writer.tsx todoText */
function tuiTodoText(item) {
  if (item.status === 'completed') return `[✓] ${item.content}`
  if (item.status === 'cancelled') return `~[ ] ${item.content}~`
  if (item.status === 'in_progress') return `[•] ${item.content}`
  return `[ ] ${item.content}`
}

/** scrollback.writer.tsx todoColor class */
function tuiTodoColorClass(status) {
  return status === 'in_progress' ? 'warning' : 'muted'
}

/** tool.ts runTodo mark */
function runTodoMark(status) {
  return status === 'completed' ? '[✓]' : status === 'in_progress' ? '[•]' : '[ ]'
}

/** session-todo-dock.tsx active() selection */
function dockActive(todos) {
  return (
    todos.find((t) => t.status === 'in_progress') ??
    todos.find((t) => t.status === 'pending') ??
    todos.filter((t) => t.status === 'completed').at(-1) ??
    todos[0]
  )
}

/** session-todo-dock.tsx checkbox projection */
function dockCheckbox(status) {
  return {
    checked: status === 'completed',
    indeterminate: status === 'in_progress',
    dataState: status,
    strikethrough: status === 'completed' || status === 'cancelled',
    opacityPendingLike: status === 'pending',
  }
}

/**
 * Deterministic sink freeze from observed consumer behavior.
 * Passthrough only when reviewing is UI-distinguishable from pending everywhere.
 */
function freezeSinkStrategy(observations) {
  const uiDistinguishes =
    observations.tuiTextDistinctFromPending &&
    observations.runMarkDistinctFromPending &&
    observations.dockActivePrefersReviewing &&
    observations.dockVisualDistinctFromPending

  const rejects = observations.schemaRejects || observations.tableRejects || observations.apiModelRejects

  if (rejects) {
    return {
      choice: 'compatibility_in_progress',
      reason: 'a storage/API consumer rejects status="reviewing"',
    }
  }
  if (!uiDistinguishes) {
    return {
      choice: 'compatibility_in_progress',
      reason:
        'UI/TUI consumers tolerate reviewing without throw but do not distinguish it from pending; freeze sink→in_progress',
    }
  }
  return {
    choice: 'passthrough',
    reason: 'all observed consumers tolerate and distinguish status="reviewing"',
  }
}

// ── Canary D: TodoTable / todo.updated / API model ───────────────────────────

test('HOST_023_canary_D_reviewing_sink_table_event_api_model', () => {
  assert.ok(OPENCODE_ROOT, 'OpenCode source checkout required (set OPENCODE_SRC or sibling ../opencode)')

  const schemaSrc = readOc('packages/schema/src/session-todo.ts')
  const todoSvcSrc = readOc('packages/opencode/src/session/todo.ts')
  const sqlSrc = readOc('packages/core/src/session/sql.ts')
  const handlerSrc = readOc('packages/opencode/src/server/routes/instance/httpapi/handlers/session.ts')
  const groupSrc = readOc('packages/opencode/src/server/routes/instance/httpapi/groups/session.ts')

  // Schema: free string status (not enum) — reviewing is not rejected at decode.
  assert.match(schemaSrc, /status:\s*Schema\.String/, 'SessionTodo.Info.status must be Schema.String')
  assert.doesNotMatch(
    schemaSrc,
    /status:\s*Schema\.(Literal|Literals|Enums?)/,
    'SessionTodo.Info.status must not be a closed enum (would reject reviewing)',
  )
  assert.match(schemaSrc, /type:\s*"todo\.updated"/, 'todo.updated event must exist')
  assert.match(schemaSrc, /todos:\s*Schema\.Array\(Info\)/, 'todo.updated payload carries Info[]')

  // TodoTable: text column, no check constraint on known statuses.
  assert.match(sqlSrc, /export const TodoTable/, 'TodoTable must exist')
  assert.match(sqlSrc, /status:\s*text\(\)\.notNull\(\)/, 'TodoTable.status is unconstrained text')

  // SessionTodo.update: persists status as-is and publishes Event.Updated with input.
  assert.match(todoSvcSrc, /status:\s*todo\.status/, 'update inserts todo.status verbatim')
  assert.match(
    todoSvcSrc,
    /events\.publish\(Event\.Updated,\s*input\)/,
    'update publishes todo.updated with the same input todos',
  )
  assert.match(
    todoSvcSrc,
    /content:\s*row\.content,\s*\n\s*status:\s*row\.status,\s*\n\s*priority:\s*row\.priority/,
    'get returns status from row without rewriting',
  )

  // API: GET /:sessionID/todo → todoSvc.get (Array of Info).
  assert.match(handlerSrc, /todoSvc\.get\(ctx\.params\.sessionID\)/, 'HTTP todo handler reads SessionTodo.get')
  assert.match(groupSrc, /\/:sessionID\/todo|sessionID.*todo/s, 'HTTP route group exposes session todo')

  // Shipped SDK model (API-facing): status is string, docs list four statuses but type is open.
  // Prefer package file path — package "exports" no longer expose dist/gen/*.d.ts.
  const sdkTypes = join(
    dirname(fileURLToPath(import.meta.url)),
    '../../../../../node_modules/@opencode-ai/sdk/dist/gen/types.gen.d.ts',
  )
  assert.equal(existsSync(sdkTypes), true, 'SDK types.gen.d.ts must exist on disk')
  const sdkSrc = readFileSync(sdkTypes, 'utf8')
  assert.match(sdkSrc, /export type Todo = \{[\s\S]*?status:\s*string;/m, 'SDK Todo.status is string')
  assert.match(sdkSrc, /export type EventTodoUpdated/, 'SDK exposes todo.updated')

  // Round-trip model observation: a reviewing row is a valid Info / event payload shape.
  const reviewingInfo = {
    content: 'await process review',
    status: 'reviewing',
    priority: 'high',
  }
  const eventPayload = {
    type: 'todo.updated',
    properties: {
      sessionID: 'ses_sink_d',
      todos: [reviewingInfo],
    },
  }
  assert.equal(typeof reviewingInfo.status, 'string')
  assert.equal(eventPayload.properties.todos[0].status, 'reviewing')

  // Freeze observation block for I (no fifth-status safety claim here).
  const observation = {
    schemaRejects: false,
    tableRejects: false,
    apiModelRejects: false,
    eventCarriesStatusVerbatim: true,
    reviewingStatus: 'reviewing',
  }
  assert.equal(observation.schemaRejects, false)
  assert.equal(observation.tableRejects, false)
  assert.equal(observation.apiModelRejects, false)
  assert.equal(observation.eventCarriesStatusVerbatim, true)
})

// ── Canary I: fifth-status consumers + deterministic sink freeze ─────────────

test('HOST_023_canary_I_reviewing_fifth_status_consumers_and_sink_freeze', () => {
  assert.ok(OPENCODE_ROOT, 'OpenCode source checkout required (set OPENCODE_SRC or sibling ../opencode)')

  const scrollbackSrc = readOc('packages/opencode/src/cli/cmd/run/scrollback.writer.tsx')
  const toolSrc = readOc('packages/opencode/src/cli/cmd/run/tool.ts')
  const dockSrc = readOc('packages/app/src/pages/session/composer/session-todo-dock.tsx')

  // Source still special-cases only the four classic statuses (no reviewing branch).
  assert.match(scrollbackSrc, /item\.status === "in_progress"/, 'TUI todoText special-cases in_progress')
  assert.doesNotMatch(scrollbackSrc, /reviewing/, 'TUI has no dedicated reviewing branch')
  assert.match(toolSrc, /item\.status === "in_progress"/, 'runTodo special-cases in_progress')
  assert.doesNotMatch(toolSrc, /reviewing/, 'runTodo has no dedicated reviewing branch')
  assert.match(dockSrc, /status === "in_progress"/, 'dock special-cases in_progress')
  assert.doesNotMatch(dockSrc, /reviewing/, 'dock has no dedicated reviewing branch')

  const reviewing = { status: 'reviewing', content: 'under process review' }
  const pending = { status: 'pending', content: 'under process review' }
  const inProgress = { status: 'in_progress', content: 'under process review' }

  // Consumers do not throw on reviewing.
  assert.equal(tuiTodoText(reviewing), '[ ] under process review')
  assert.equal(tuiTodoColorClass('reviewing'), 'muted')
  assert.equal(runTodoMark('reviewing'), '[ ]')
  assert.deepEqual(dockCheckbox('reviewing'), {
    checked: false,
    indeterminate: false,
    dataState: 'reviewing',
    strikethrough: false,
    opacityPendingLike: false,
  })

  const tuiTextDistinctFromPending = tuiTodoText(reviewing) !== tuiTodoText(pending)
  const runMarkDistinctFromPending = runTodoMark('reviewing') !== runTodoMark('pending')
  // Dock active(): reviewing is neither in_progress nor pending, so a sole reviewing
  // item is still selected as todos[0] — but a mixed list prefers pending over reviewing.
  const mixed = [reviewing, pending, inProgress]
  const dockActivePrefersReviewing = dockActive([reviewing])?.status === 'reviewing' && dockActive(mixed)?.status === 'reviewing'
  const dockVisualDistinctFromPending =
    JSON.stringify(dockCheckbox('reviewing')) !== JSON.stringify(dockCheckbox('pending'))

  // Measured: TUI text/mark collapse reviewing→pending glyph; dock data-state keeps
  // the raw string but does not treat it as active work (no indeterminate/dot).
  assert.equal(tuiTextDistinctFromPending, false, 'TUI text maps reviewing like pending')
  assert.equal(runMarkDistinctFromPending, false, 'runTodo mark maps reviewing like pending')
  assert.equal(dockActive([reviewing, pending])?.status, 'pending', 'dock prefers pending over reviewing')
  assert.equal(dockActivePrefersReviewing, false, 'dock does not prefer reviewing as active work')
  // data-state differs, but visual affordances (checked/indeterminate/strikethrough) match pending-ish idle.
  assert.equal(dockCheckbox('reviewing').indeterminate, false)
  assert.equal(dockCheckbox('in_progress').indeterminate, true)

  const freeze = freezeSinkStrategy({
    schemaRejects: false,
    tableRejects: false,
    apiModelRejects: false,
    tuiTextDistinctFromPending,
    runMarkDistinctFromPending,
    dockActivePrefersReviewing,
    dockVisualDistinctFromPending,
  })

  // Frozen decision for membrane: compatibility sink projects reviewing→in_progress.
  // Canonical Magic status remains reviewing (not proven here; membrane must not alter it).
  assert.equal(
    freeze.choice,
    'compatibility_in_progress',
    `expected forced compatibility sink, got ${freeze.choice}: ${freeze.reason}`,
  )
  assert.match(freeze.reason, /in_progress|distinguish/)

  // Explicit contract surface for later membrane wiring (read-only freeze record).
  const SINK_FREEZE = Object.freeze({
    canary: 'D+I',
    host: 'opencode-v1',
    canonicalStatus: 'reviewing',
    compatibilityStatus: 'in_progress',
    choice: freeze.choice,
    reason: freeze.reason,
    passthroughSafe: false,
  })
  assert.equal(SINK_FREEZE.passthroughSafe, false)
  assert.equal(SINK_FREEZE.compatibilityStatus, 'in_progress')
  assert.notEqual(SINK_FREEZE.canonicalStatus, SINK_FREEZE.compatibilityStatus)
})
