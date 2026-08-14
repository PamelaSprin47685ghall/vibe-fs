/**
 * degradation-list.mjs — VERIFY-004's forbidden-degradation list, read from the clause.
 *
 * W7 requires a failing test per forbidden degradation, and the completeness gate
 * that checks "per" needs to know what the items are. They live as prose inside
 * `requirements/verification-system/WHAT.md` under `### 禁止退化清单`, so either a human retypes them here or this
 * file reads them. W1 and W2 of this same package exist to delete two hand-kept
 * mirrors — a third would be the same defect wearing a new name.
 *
 * ── the anchor is load-bearing ──────────────────────────────────────────────
 *
 * The verify doc holds many fenced ```text blocks. Measured: scanning the file for the
 * first one hands back VERIFY-002's five-level test ladder, and every downstream
 * case would then claim to cover a degradation while naming a test layer. So the
 * search starts at the heading and refuses to cross the next heading — a section
 * with no block is an error, never the next section's block.
 *
 * ── why the ids are named rather than numbered ──────────────────────────────
 *
 * An ordinal (`D01`…`D13`) is derived and therefore never stale, which is exactly
 * its defect. Ids exist so a case can say WHICH degradation it covers. Insert an
 * item at position 3 in the SSOT and every ordinal above it shifts by one: each
 * downstream case silently re-points at its neighbour, still runs, still passes,
 * and now claims coverage of something it never tested. Nothing goes red. That is
 * the shape of all four pseudo-gates this repo has measured — `epochCold`,
 * `faultFor`, `boundaryFor`, the empty `resetHeartbeat` — a green light describing
 * something other than what it says.
 *
 * A named id cannot drift silently, because `NAMED` is checked against the clause
 * text on every import, in both directions. Add an item and the parser has no id
 * for it; reword or delete one and an id has no item; either way it throws naming
 * the text or the id. The cost is that editing the clause forces an edit here, and
 * that cost is the feature: whoever adds a forbidden degradation is made to look at
 * whether W7 covers it.
 *
 * `NAMED` is not a second source of truth. The clause decides which items exist,
 * their order and their count; `NAMED` only supplies names for whatever the clause
 * says, and is rejected the moment the two disagree. Its keys restate the text for
 * the same reason the test's expectations do — to fail when the text moves.
 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/** The heading the list lives under. Exported so a failure message and the test agree. */
export const ANCHOR = '### 禁止退化清单';

export const SSOT_ORIGIN = 'requirements/verification-system/WHAT.md';

/** Resolved from this module, not from `cwd`: two trees import it. */
// e2e/support sits three levels below the clause file (support -> e2e -> tests ->
// verification-system). The origin string stays repo-relative so messages and
// the pin test name the same path.
const CLAUSE_FILE = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'WHAT.md');

/**
 * Clause text → stable id.
 *
 * Keyed by the whole item, because a discriminating substring would still match a
 * reworded item and the binding would stop proving anything.
 */
const NAMED = new Map([
  ['把 wall-clock 总超时当作唯一挂死判据', 'VERIFY_004_D_WALL_CLOCK_AS_ONLY_HANG_CRITERION'],
  ['让原始 SSE 或 provider 流量续期 watchdog', 'VERIFY_004_D_RAW_TRAFFIC_RENEWS_WATCHDOG'],
  ['让背景车道进展续期 watchdog', 'VERIFY_004_D_BACKGROUND_LANE_RENEWS_WATCHDOG'],
  ['删除 watchdog 的诊断转储，只保留退出码', 'VERIFY_004_D_WATCHDOG_DUMP_REDUCED_TO_EXIT_CODE'],
  ['让 watchdog 计时器持有事件循环，使干净结束也要等满静默窗口', 'VERIFY_004_D_WATCHDOG_TIMER_HOLDS_EVENT_LOOP'],
  ['存在只有总超时保护的时间窗', 'VERIFY_004_D_WINDOW_GUARDED_ONLY_BY_TOTAL_TIMEOUT'],
  ['声明了断言心跳但未接线', 'VERIFY_004_D_DECLARED_HEARTBEAT_NOT_WIRED'],
  ['用固定 sleep 代替因果 bark 交错启动', 'VERIFY_004_D_FIXED_SLEEP_REPLACES_CAUSAL_BARK'],
  ['就绪超时或就绪前退出被当作通过', 'VERIFY_004_D_READY_TIMEOUT_OR_EARLY_EXIT_PASSES'],
  ['Release gate 变成「最多 N 轮」或「重跑直到通过」', 'VERIFY_004_D_RELEASE_GATE_BECOMES_AT_MOST_N_ROUNDS'],
  ['数量常量与清单各自维护', 'VERIFY_004_D_COUNT_CONSTANT_MAINTAINED_APART_FROM_LIST'],
  ['静态门禁的路径判据指向不存在的目录', 'VERIFY_004_D_STATIC_GATE_PATH_DOES_NOT_EXIST'],
  ['延长静默窗口或测试超时以掩盖竞态', 'VERIFY_004_D_WINDOW_WIDENED_TO_HIDE_A_RACE'],
]);

const FENCE_OPEN = '```text';
const FENCE_CLOSE = '```';

/** The item texts inside the FIRST fenced block of the anchored section. */
const readBlock = (lines, origin) => {
  const anchor = lines.findIndex((line) => line.trim() === ANCHOR);
  if (anchor === -1) {
    throw new Error(
      `${origin}: no line equal to '${ANCHOR}' — the forbidden-degradation list has moved or been renamed`,
    );
  }

  let open = -1;
  for (let index = anchor + 1; index < lines.length; index += 1) {
    const text = lines[index].trim();
    if (text === FENCE_OPEN) {
      open = index;
      break;
    }
    // A heading ends the section. Reading past it would return a later clause's block.
    if (text.startsWith('#')) break;
  }
  if (open === -1) {
    throw new Error(
      `${origin}: '${ANCHOR}' (line ${anchor + 1}) is followed by no fenced block before the next heading`,
    );
  }

  const items = [];
  for (let index = open + 1; index < lines.length; index += 1) {
    const text = lines[index].trim();
    if (text === FENCE_CLOSE) return items;
    if (text !== '') items.push({ text, line: index + 1 });
  }
  throw new Error(`${origin}: the block opened at line ${open + 1} under '${ANCHOR}' is never closed`);
};

/**
 * The list, or a throw. Never a short list, never an empty one.
 *
 * A parser that returned `[]` here would make the completeness gate vacuously
 * green, which design-script-forest.md:630 calls worse than having no gate at all:
 * 「一个能对错误实现给出绿灯的验证装置，比没有验证装置更危险」.
 */
export function parseDegradations(source, { origin }) {
  const items = readBlock(source.split('\n'), origin);

  if (items.length === 0) {
    throw new Error(`${origin}: the block under '${ANCHOR}' has no items — the clause lists ${NAMED.size}`);
  }

  const unnamed = items.filter((item) => !NAMED.has(item.text));
  if (unnamed.length > 0) {
    throw new Error(
      `${origin}: ${unnamed.length} item(s) under '${ANCHOR}' have no id in degradation-list.mjs, ` +
        `so no gate case can cite them: ${unnamed.map((item) => `line ${item.line} '${item.text}'`).join('; ')}`,
    );
  }

  const present = new Set(items.map((item) => item.text));
  const orphaned = [...NAMED].filter(([text]) => !present.has(text));
  if (orphaned.length > 0) {
    throw new Error(
      `${origin}: ${orphaned.length} id(s) in degradation-list.mjs name text absent from '${ANCHOR}', ` +
        `so a gate case could cite a degradation the clause no longer forbids: ` +
        orphaned.map(([text, id]) => `${id} ('${text}')`).join('; '),
    );
  }

  // Backstop for the one shape the two checks above admit: one item written twice
  // while another is written twice too. Every item stays named and no id orphaned.
  if (items.length !== NAMED.size) {
    throw new Error(
      `${origin}: '${ANCHOR}' yielded ${items.length} items but ${NAMED.size} ids are named — a duplicated line`,
    );
  }

  return items.map((item) => Object.freeze({ id: NAMED.get(item.text), text: item.text, line: item.line }));
}

/** In clause order, because the clause's order is the only order anyone can cite. */
export const DEGRADATIONS = Object.freeze(
  parseDegradations(readFileSync(CLAUSE_FILE, 'utf8'), { origin: SSOT_ORIGIN }),
);
