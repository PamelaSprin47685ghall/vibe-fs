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

/**
 * How a surface stands relative to ARCH-010, which is a different question from its class.
 *
 * The class says what KIND of text a surface carries. This says whether the surface owes the clause
 * anything, and the three-way split is what ARCH-010 §3.2 draws rather than something invented here:
 *
 *   CanonicalPayload    the runtime composes its OWN instruction or data alongside the caller's
 *                       text, so the result is a synthetic payload and must route through the
 *                       canonical writer. Migrated.
 *
 *   VerbatimForward     the runtime delivers the caller's or the model's text UNCHANGED as the whole
 *                       message, adding nothing. §3.2 excludes this: model-native text 「不因本动议
 *                       而重写」 when delivered, and only becomes a value when 「被复制进其他合成
 *                       payload」. Wrapping a bare assignment in `assignment = "…"` would add a layer
 *                       the model must unwrap for zero information, which §17.3 forbids as
 *                       「每个 data payload 强制附加 instruction」.
 *
 *   RuntimeInstruction  the runtime composes instruction text of its own but has not been migrated
 *                       yet. This is the honest N5 worklist, and naming it here is what stops
 *                       §17.8's 「旧 continuation 用裸英语，新 continuation 用 TOML」 from becoming
 *                       permanent by inattention.
 *
 * The distinction is checkable rather than declarative: a `CanonicalPayload` file must reference a
 * canonical writer, and the other two must not. So a surface that quietly starts composing, or one
 * that quietly stops routing, fails here.
 */
export const SURFACE_STANDINGS = Object.freeze(['CanonicalPayload', 'VerbatimForward', 'RuntimeInstruction']);

/** The modules that ARE the canonical writer, or compose exclusively through it. */
const CANONICAL_WRITERS = Object.freeze(['SyntheticToml', 'ForkChildPayload', 'RuntimeNudge', 'BloggerToml']);

const TYPED_PAYLOAD_ROUTES = Object.freeze({
  'BloggerDeltaChunk.Toml': Object.freeze({
    pattern: /\belse\s+chunk\.Toml\b/,
    description: 'the normal Blogger branch must forward chunk.Toml without a prose wrapper',
  }),
});

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
 *
 * `composerFiles` is that same fact made checkable. The standing↔code check below must read the file
 * that COMPOSES, and for the two nudge surfaces that is not the send site: `HostSessionNudge.fs`
 * only sends, while `TurnCompletionProgram.fs` and `HostReviewGuard.fs` decide the text. Reading the
 * send site alone would report those two as unmigrated forever, and the tempting response would be to
 * relabel them rather than to look at where the prose actually lives.
 *
 * Omitted when the composer IS the send-site file, so the common case stays quiet.
 */
const SURFACES = new Map([
  [
    'src/Wanxiangshu.Next/Infrastructure/OpenCode/Tools/OneShotAgentTool.fs#SendAgentOwnerRoot',
    {
      class: 'RuntimeSyntheticToml',
      standing: 'VerbatimForward',
      surface: 'one-shot agent assignment',
      composer: 'promptFrom: the caller s own prompt args, joined; the runtime adds nothing',
    },
  ],
  [
    'src/Wanxiangshu.Next/Session/CompanionHostBlogger.fs#SendAgentOwnerRoot',
    {
      class: 'RuntimeSyntheticToml',
      standing: 'CanonicalPayload',
      surface: 'Blogger normal delta prompt',
      composer:
        'BloggerDelta.fs:33-34 renders CompanionPrompt.NormalInstruction plus typed ' +
        'BloggerDeltaChunk.Toml; CompanionHostBlogger.fs:69-76 forwards chunk.Toml on the normal path',
      composerFiles: ['src/Wanxiangshu.Next/Domain/BloggerDelta.fs', 'src/Wanxiangshu.Next/Session/CompanionHostBlogger.fs'],
      typedPayload: 'BloggerDeltaChunk.Toml',
    },
  ],
  [
    'src/Wanxiangshu.Next/Session/HostForkRunLifecycle.fs#SendAgentOwnerRoot',
    {
      class: 'RuntimeSyntheticToml',
      standing: 'VerbatimForward',
      surface: 'forked child prompt (lifecycle path)',
      composer: 'caller-supplied assignment, unwrapped',
    },
  ],
  [
    'src/Wanxiangshu.Next/Session/HostForkAgentOwner.fs#SendAgentOwnerRoot',
    {
      class: 'RuntimeSyntheticToml',
      standing: 'VerbatimForward',
      surface: 'forked child prompt (shared helper)',
      composer: 'caller-supplied text, unwrapped; the fork path composes before calling this',
    },
  ],
  [
    'src/Wanxiangshu.Next/Session/HostForkRuntimeFork.fs#sendFirstPrompt',
    {
      class: 'RuntimeSyntheticToml',
      standing: 'CanonicalPayload',
      surface: 'forked child first prompt',
      composer: 'ForkChildPayload.render — N3 replaced two conditional envelopes',
    },
  ],
  [
    'src/Wanxiangshu.Next/Infrastructure/OpenCode/Host/HostSessionNudge.fs#SendContinuation',
    {
      class: 'RuntimeSyntheticToml',
      standing: 'CanonicalPayload',
      surface: 'continuation nudge (provider retry, manager and reviewer review guards)',
      composer: 'RuntimeNudge.providerRetry / managerReviewGuard / reviewerVerdictGuard',
      composerFiles: ['src/Wanxiangshu.Next/Application/Reconciliation/TurnCompletionProgram.fs', 'src/Wanxiangshu.Next/Infrastructure/OpenCode/Host/HostReviewGuard.fs'],
    },
  ],
  [
    'src/Wanxiangshu.Next/Session/HostForkBusyNudge.fs#SendContinuation',
    {
      class: 'RuntimeSyntheticToml',
      standing: 'VerbatimForward',
      surface: 'busy-agent fire-and-forget nudge (EXEC-002)',
      composer: 'the same assignment the caller supplied, re-delivered; nothing added',
    },
  ],
  [
    'src/Wanxiangshu.Next/Infrastructure/OpenCode/Host/HostSessionNudge.fs#SendInteractionRepair',
    {
      class: 'RuntimeSyntheticToml',
      standing: 'CanonicalPayload',
      surface: 'interaction repair (missing final report)',
      composer: 'RuntimeNudge.missingFinalReport',
      composerFiles: ['src/Wanxiangshu.Next/Application/Reconciliation/TurnCompletionProgram.fs'],
    },
  ],
]);

/** Production `.fs` files, absolute paths. */
const productionFiles = () => walk(`${REPO_ROOT}src/Wanxiangshu.Next`, ['.fs']);

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
 * Whether a send-site file references a canonical writer, in CODE.
 *
 * Comment lines are skipped for the same reason `systemPromptLeaks` skips them: a doc comment saying
 * "this forwards verbatim, so it does not route through `SyntheticToml`" must not be punished, and
 * that sentence is exactly what a reader needs.
 */
const routesThroughCanonicalWriter = (file) =>
  readFileSync(`${REPO_ROOT}${file}`, 'utf8')
    .split('\n')
    .some((text) => {
      const trimmed = text.trim();
      if (trimmed.startsWith('///') || trimmed.startsWith('//')) return false;
      return CANONICAL_WRITERS.some((writer) => text.includes(`${writer}.`));
    });

const routesThroughTypedPayload = (payload, files) => {
  const contract = TYPED_PAYLOAD_ROUTES[payload];
  if (!contract) return false;

  return files.some((file) => contract.pattern.test(readFileSync(`${REPO_ROOT}${file}`, 'utf8')));
};

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
    if (!SURFACE_STANDINGS.includes(entry.standing)) {
      violations.push(
        `${key} has standing '${entry.standing}', which is not one of ${SURFACE_STANDINGS.join(', ')}`,
      );
    }
  }

  // The standing↔code agreement, in both directions. This is what keeps the N5 worklist honest: a
  // surface cannot be relabelled migrated without routing, and a migrated one cannot silently stop.
  for (const [key, entry] of SURFACES) {
    const composerFiles = entry.composerFiles ?? [key.split('#')[0]];
    const typedRoute = routesThroughTypedPayload(entry.typedPayload, composerFiles);
    const routes = composerFiles.some(routesThroughCanonicalWriter) || typedRoute;

    if (entry.typedPayload && !TYPED_PAYLOAD_ROUTES[entry.typedPayload]) {
      violations.push(`${key} names unknown typed payload '${entry.typedPayload}'`);
    }

    if (entry.typedPayload && !typedRoute) {
      const contract = TYPED_PAYLOAD_ROUTES[entry.typedPayload];
      violations.push(
        `${key} claims ${entry.typedPayload} but none of ${composerFiles.join(', ')} satisfies ` +
          `${contract?.description ?? 'its typed payload contract'}`,
      );
    }

    if (entry.standing === 'CanonicalPayload' && !routes) {
      violations.push(
        `${key} is marked CanonicalPayload but none of ${composerFiles.join(', ')} references a ` +
          `canonical writer (${CANONICAL_WRITERS.join(', ')}); the label claims a migration that ` +
          'did not happen',
      );
    }

    if (entry.standing !== 'CanonicalPayload' && routes) {
      violations.push(
        `${key} is marked ${entry.standing} but ${composerFiles.join(', ')} references a canonical ` +
          'writer; either it composes a payload and the standing is wrong, or the reference is a ' +
          'second dialect',
      );
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

  const byStanding = (standing) => inventory().filter((entry) => entry.standing === standing).length;

  console.log(
    `surface-inventory: OK — ${SURFACES.size} surface(s): ` +
      `${byStanding('CanonicalPayload')} canonical, ` +
      `${byStanding('VerbatimForward')} verbatim-forward, ` +
      `${byStanding('RuntimeInstruction')} awaiting N5; ` +
      'system prompt and human raw structurally excluded',
  );
};

if (process.argv[1] && import.meta.url === `file://${process.argv[1]}`) main();
