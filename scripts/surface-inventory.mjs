/**
 * surface-inventory.mjs — the runtime textual surfaces, derived from their sinks (ARCH-010, N2).
 *
 * ARCH-010's gate requires 「所有纳入范围的 runtime synthetic text 已建立 inventory」, and the
 * motion's M1 asks for that inventory split four ways:
 *
 *   NativeSystemPrompt      excluded — the provider's own instruction channel
 *   HumanRaw                excluded — a real user's words, kept verbatim
 *   ModelNative             excluded — assistant output in its original transcript
 *   RuntimeSyntheticToml    in scope — composed by the runtime, read by the model as text
 *
 * ── why this is derived from sinks rather than from producers ────────────────
 *
 * A hand-written list of "places that build prompt text" is the defect W1 and W2 of this same
 * package deleted twice: a mirror nobody updates. Producers cannot be enumerated mechanically —
 * any `sprintf` is a candidate — so the ground truth here is the SINK side, which is a closed set
 * and is enumerable.
 *
 * PROMPT-005 makes that closure real: every user-shaped prompt the plugin sends passes through
 * `PromptDispatcher`, so its three send members plus `sendFirstPrompt` are the only ways plugin
 * text reaches `SendPrompt` and therefore the provider. Scanning their call sites yields the
 * surfaces; the registry then says what each one carries. Both directions are checked, so a new
 * send site with no entry fails, and an entry whose site has vanished fails too.
 *
 * That is the same shape as `degradation-list.mjs`: parse the ground truth, bind names to it, and
 * reject the moment the two disagree. A binding only proves something if both ends are held.
 *
 * ── the two exclusions are structural, not asserted in prose ────────────────
 *
 * `NativeSystemPrompt` and `HumanRaw` are not "sites we chose not to migrate". They have no
 * dispatcher site at all, and that absence is what the audit checks:
 *
 *   system prompts reach the model through the Host's agent config, so no file that calls a send
 *   member may also read `prompts/*.md` — measured: none does
 *
 *   `HumanRoot` is inbound only. `AcceptHumanRoot` records a root the Host already delivered;
 *   there is no `SendHumanRoot`, so the plugin cannot re-send a user's words as synthetic text
 *
 * Both are checked rather than trusted, because "we don't do that" is exactly the claim that rots.
 *
 * `ModelNative` needs no check here: assistant text re-entering a payload does so as a value
 * inside a Blogger delta item, which `BloggerToml` renders as data by construction.
 */

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { walk } from './repo-scan.mjs';

const REPO_ROOT = fileURLToPath(new URL('../', import.meta.url));

export const SURFACE_CLASSES = Object.freeze([
  'NativeSystemPrompt',
  'HumanRaw',
  'ModelNative',
  'RuntimeSyntheticToml',
]);

/** The only ways plugin-composed text reaches `ISessionHostPort.SendPrompt` (PROMPT-005). */
const SINKS = Object.freeze([
  'SendAgentOwnerRoot',
  'SendContinuation',
  'SendInteractionRepair',
  'sendFirstPrompt',
]);

/**
 * Files that own the system prompt assets. A send-site file naming any of these would mean a
 * system prompt had been routed into a conversation-level synthetic message — forbidden outright.
 */
const SYSTEM_PROMPT_MARKERS = Object.freeze(['PromptAssets', 'systemPromptOf', 'prompts/']);

/**
 * Every registered surface, keyed by `<file>#<sink>`.
 *
 * `composer` is where the text is actually built, which is the field that makes this inventory
 * actionable: N3 and N5 migrate composers, not sinks. Several sites share a sink and differ only
 * in composer, which is why the key is the pair rather than the sink alone.
 */
const SURFACES = new Map([
  [
    'next/OpenCode/OneShotAgentTool.fs#SendAgentOwnerRoot',
    {
      class: 'RuntimeSyntheticToml',
      surface: 'one-shot agent assignment',
      composer: 'the calling tool s arguments, wrapped by the tool handler',
    },
  ],
  [
    'next/Session/CompanionHostBlogger.fs#SendAgentOwnerRoot',
    {
      class: 'RuntimeSyntheticToml',
      surface: 'Blogger delta prompt (normal and post-restart re-anchor)',
      composer: 'CompanionHostBlogger.fs:72,77 + CompanionPrompt.fs + BloggerToml',
    },
  ],
  [
    'next/Session/HostForkRunLifecycle.fs#SendAgentOwnerRoot',
    {
      class: 'RuntimeSyntheticToml',
      surface: 'forked child first prompt (lifecycle path)',
      composer: 'caller-supplied assignment, unwrapped',
    },
  ],
  [
    'next/Session/HostForkAgentOwner.fs#SendAgentOwnerRoot',
    {
      class: 'RuntimeSyntheticToml',
      surface: 'forked child first prompt (shared helper)',
      composer: 'caller-supplied assignment, unwrapped',
    },
  ],
  [
    'next/Session/HostForkRuntimeFork.fs#sendFirstPrompt',
    {
      class: 'RuntimeSyntheticToml',
      surface: 'forked child assignment envelope',
      composer:
        'HostForkRuntimeFork.fs:196 conditional envelope + :98 reviewer requirements — N3 target, ' +
        'and the shared root cause of the currently red canaries',
    },
  ],
  [
    'next/OpenCode/HostSessionNudge.fs#SendContinuation',
    {
      class: 'RuntimeSyntheticToml',
      surface: 'continuation nudge',
      composer: 'TurnCompletionProgram.fs:92,158,227 + HostReviewGuard.fs:147,164',
    },
  ],
  [
    'next/Session/HostForkBusyNudge.fs#SendContinuation',
    {
      class: 'RuntimeSyntheticToml',
      surface: 'busy-agent fire-and-forget nudge (EXEC-002)',
      composer: 'caller-supplied assignment, unwrapped',
    },
  ],
  [
    'next/OpenCode/HostSessionNudge.fs#SendInteractionRepair',
    {
      class: 'RuntimeSyntheticToml',
      surface: 'interaction repair',
      composer: 'TurnCompletionProgram.fs:227 missing-final-report text',
    },
  ],
]);

/** Production `.fs` files, absolute paths. */
const productionFiles = () => walk(`${REPO_ROOT}next`, ['.fs']);

const relative = (file) => file.slice(REPO_ROOT.length);

/**
 * Every call site of a sink, as `{ key, file, sink, line }`.
 *
 * `member` lines are skipped because a definition is not a call site, and the dispatcher's own
 * members would otherwise register as uses of themselves. Doc comments are skipped for the same
 * reason a prose mention is not a send.
 */
export function scanSurfaces(files = productionFiles()) {
  const found = [];

  for (const file of files) {
    readFileSync(file, 'utf8')
      .split('\n')
      .forEach((text, index) => {
        const trimmed = text.trim();
        if (trimmed.startsWith('///') || trimmed.startsWith('//') || trimmed.startsWith('member ')) return;

        for (const sink of SINKS) {
          if (!new RegExp(`\\.${sink}\\b`).test(text)) continue;
          found.push({ key: `${relative(file)}#${sink}`, file: relative(file), sink, line: index + 1 });
        }
      });
  }

  return found;
}

/**
 * Files holding at least one send site, with the system-prompt markers they name in CODE.
 *
 * Comment lines are skipped, and that is not a shortcut. A raw `source.includes` would fail on a
 * doc comment reading "this deliberately does not go through `PromptAssets`" — a sentence that
 * makes the exclusion legible and which a gate must not punish. Measured while red-proving this
 * rule: the first version flagged exactly such a line, which would have trained the next reader to
 * delete the explanation rather than keep it.
 *
 * Reports the line so the message points at the code, not just the file.
 */
const systemPromptLeaks = (sites) => {
  const leaks = [];

  for (const file of new Set(sites.map((site) => site.file))) {
    readFileSync(`${REPO_ROOT}${file}`, 'utf8')
      .split('\n')
      .forEach((text, index) => {
        const trimmed = text.trim();
        if (trimmed.startsWith('///') || trimmed.startsWith('//')) return;

        const named = SYSTEM_PROMPT_MARKERS.filter((marker) => text.includes(marker));
        if (named.length > 0) leaks.push({ file, line: index + 1, named });
      });
  }

  return leaks;
};

/**
 * Send sites that also name `HumanRoot` on the sending line.
 *
 * Line-scoped rather than file-scoped: a file may legitimately mention the kind elsewhere (a match
 * arm, a ledger parse). What must not exist is a send whose payload is a human's own words.
 */
const humanRawLeaks = (sites) =>
  sites.filter((site) => {
    const line = readFileSync(`${REPO_ROOT}${site.file}`, 'utf8').split('\n')[site.line - 1];
    return line.includes('HumanRoot');
  });

/**
 * Audit the tree. Returns violation strings; empty means the inventory matches reality.
 */
export function auditSurfaces(files = productionFiles()) {
  const violations = [];
  const sites = scanSurfaces(files);

  if (sites.length === 0) {
    violations.push(
      'no send site found at all — the sink names have changed, and an empty scan would make ' +
        'every check below vacuously green',
    );
    return violations;
  }

  const seen = new Set(sites.map((site) => site.key));

  for (const site of sites) {
    if (!SURFACES.has(site.key)) {
      violations.push(
        `${site.file}:${site.line} sends through ${site.sink} but is not in the inventory; ` +
          'every runtime synthetic surface must be classified (ARCH-010)',
      );
    }
  }

  for (const key of SURFACES.keys()) {
    if (!seen.has(key)) {
      violations.push(`${key} is in the inventory but no such send site exists; the entry is stale`);
    }
  }

  for (const [key, entry] of SURFACES) {
    if (!SURFACE_CLASSES.includes(entry.class)) {
      violations.push(`${key} has class '${entry.class}', which is not one of ${SURFACE_CLASSES.join(', ')}`);
    }
  }

  for (const leak of systemPromptLeaks(sites)) {
    violations.push(
      `${leak.file}:${leak.line} both sends a prompt and names ${leak.named.join(', ')}; a system ` +
        'prompt must reach the model through the Host agent config, never as a synthetic message ' +
        '(ARCH-010 排除范围)',
    );
  }

  for (const leak of humanRawLeaks(sites)) {
    violations.push(
      `${leak.file}:${leak.line} sends with HumanRoot in scope; human raw messages are inbound ` +
        'only and must not be re-sent as synthetic text (ARCH-010 排除范围)',
    );
  }

  return violations;
}

/** The inventory, for a reader or a report. */
export function inventory() {
  return [...SURFACES].map(([key, entry]) => ({ key, ...entry }));
}

const main = () => {
  const violations = auditSurfaces();

  if (violations.length > 0) {
    console.error('surface-inventory: FAIL');
    for (const violation of violations) console.error(`  ${violation}`);
    process.exit(1);
  }

  const inScope = inventory().filter((entry) => entry.class === 'RuntimeSyntheticToml');
  console.log(
    `surface-inventory: OK — ${SURFACES.size} surface(s), ${inScope.length} in ARCH-010 scope, ` +
      'system prompt and human raw structurally excluded',
  );
};

if (process.argv[1] && import.meta.url === `file://${process.argv[1]}`) main();
