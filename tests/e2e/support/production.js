/**
 * production.js — the harness's facade onto production renderers.
 *
 * `tests/unit/domain.mjs` exists because crossing the Fable boundary by hand is a silent-failure
 * machine, and the harness needs the same protection for the same reason. It cannot import `domain.mjs`
 * itself: that facade is the contract surface for layer 1–3 tests and is shaped around them.
 *
 * ── the trap this file exists to make unreachable ───────────────────────────
 *
 * Measured while writing the N4 gate. `ForkChildPayload.render` takes an F# list; handed a plain JS
 * array it does not throw — it reads the array as an EMPTY list:
 *
 *   render(new ForkChildAssignment('Do X.', undefined, ['Ship it.']))
 *     → the requirement is silently absent, and the payload is byte-identical to the no-requirement
 *       shape
 *
 * `gate-runtime-key-cases.mjs` had exactly this bug: its reviewer case passed `['Ship it.']`, received
 * a payload with no requirements, and still went green — the declaration it asserts is
 * `[anchor, assignment]`, and both fragments are present in every shape. So the case proved the
 * matcher works while never exercising the path it was written for. Right outcome, wrong reason, zero
 * coverage — the same shape as W4's disconnected verdict feed.
 *
 * The fix is not "remember to call `ofArray`". It is that no caller here touches a raw production
 * function, so the conversion cannot be forgotten. Every list-taking parameter is converted on the way
 * in, and `gate-arch010-cases.mjs` asserts that a plain array reaches the payload.
 *
 * The fable-library directory carries its version, so it is resolved by scanning rather than named.
 * A hardcoded `fable-library-js.4.30.0` was the first draft of this file and would have broken on the
 * next Fable upgrade — or, worse, on this tree, which is at 5.13.0.
 */

import { readdirSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const BUILD_ROOT = fileURLToPath(new URL('../../../dist/', import.meta.url));
const FABLE_MODULES = join(BUILD_ROOT, 'fable_modules');

const fableLibraryDir = (() => {
  const candidates = readdirSync(FABLE_MODULES).filter((entry) => entry.startsWith('fable-library-js.'));

  if (candidates.length !== 1) {
    throw new Error(
      `expected exactly one fable-library-js.* in ${FABLE_MODULES}, found: ${candidates.join(', ') || '(none)'}`,
    );
  }

  return join(FABLE_MODULES, candidates[0]);
})();

const { ofArray } = await import(join(fableLibraryDir, 'List.js'));

const [ForkModule, BloggerModule, SyntheticModule] = await Promise.all([
  import(join(BUILD_ROOT, 'Domain/ForkChildPayload.js')),
  import(join(BUILD_ROOT, 'Domain/BloggerToml.js')),
  import(join(BUILD_ROOT, 'Domain/SyntheticToml.js')),
]);

/** An F# list from an array, or an already-converted list left alone. */
const toList = (items) => (Array.isArray(items) ? ofArray(items) : items);

// ── SyntheticToml: the canonical writer ──────────────────────────────────────

export const renderString = (text) => SyntheticModule.renderString(text);
export const comment = (text) => SyntheticModule.comment(text);
export const field = (name, renderedValue) => SyntheticModule.field(name, renderedValue);
export const tableArrayEntry = (name, fields) => SyntheticModule.tableArrayEntry(name, toList(fields));
export const byteCount = (text) => SyntheticModule.byteCount(text);

/** `document` emits as `document$`: the plain name would collide with the DOM global. */
export const syntheticDocument = (instructions, body) =>
  SyntheticModule.document$(toList(instructions), toList(body));

// ── ForkChildPayload: the forked child's first prompt ─────────────────────────

/** The instruction lines every forked child receives, as a JS array. */
export const forkBaseInstructions = [...ForkModule.ForkChildPayload_BaseInstructions];

export const forkParentWorkRecordInstruction = ForkModule.ForkChildPayload_ParentWorkRecordInstruction;
export const forkRequirementsInstruction = ForkModule.ForkChildPayload_RequirementsInstruction;

/** Render a forked child's first prompt by field name. */
export const forkPayload = ({ assignment, parentWorkRecord, originalUserRequirements = [] }) =>
  ForkModule.ForkChildPayload_render(
    new ForkModule.ForkChildAssignment(assignment, parentWorkRecord, toList(originalUserRequirements)),
  );

export const forkRelay = (assignment, parentWorkRecord, requirements = []) =>
  ForkModule.ForkChildPayload_relay(assignment, parentWorkRecord, toList(requirements));

/** The anchor a scenario declaration uses: the first base instruction, as it appears rendered. */
export const forkAnchor = () => comment(forkBaseInstructions[0]);

// ── BloggerToml: the Companion delta ─────────────────────────────────────────

const part = (caseName, fields) => {
  const cases = BloggerModule.BloggerDeltaPart.prototype.cases();
  const tag = cases.indexOf(caseName);

  if (tag < 0) {
    throw new Error(`BloggerDeltaPart has no case '${caseName}'. Cases: ${cases.join(', ')}`);
  }

  return new BloggerModule.BloggerDeltaPart(tag, fields);
};

export const bloggerText = (text) => part('TextPart', [text]);
export const bloggerReasoning = (text) => part('ReasoningPart', [text]);
export const bloggerToolCall = (tool, args) => part('ToolCallPart', [tool, args]);
export const bloggerToolResult = (text) => part('ToolResultPart', [text]);
export const bloggerImageOmitted = (mediaType) => part('ImageOmitted', [mediaType]);
export const bloggerMediaOmitted = (mediaType) => part('MediaOmitted', [mediaType]);

export const bloggerItem = ({ role, part: itemPart, truncated = false }) =>
  new BloggerModule.BloggerDeltaItem(role, itemPart, truncated);

export const bloggerDocument = (items) => BloggerModule.BloggerToml_render(toList(items));

export const bloggerDocumentWith = (instructions, items) =>
  BloggerModule.BloggerToml_renderWith(toList(instructions), toList(items));
