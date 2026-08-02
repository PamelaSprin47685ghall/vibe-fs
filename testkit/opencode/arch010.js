/**
 * arch010.js — validate a rendered payload against ARCH-010's document rules.
 *
 * The clause's gate section lists what must be checked: instruction not encoded as a field, data not
 * emitted as a top-level comment, instruction always first, no top-level comment once the data body
 * begins, no `"""`, the closing delimiter alone on its line, and the whole thing parseable with the
 * value equal to the original plus one trailing newline.
 *
 * ── why this is separate from the writer's own tests ────────────────────────
 *
 * `synthetic-toml.test.mjs` proves `SyntheticToml` behaves. That is a different claim from "every
 * payload reaching a model obeys ARCH-010", and only the second is what the clause asks for. A
 * producer that hand-assembles a document, or calls the writer and then appends to the result, passes
 * every writer test and violates the clause.
 *
 * So this validates OUTPUT, with no knowledge of who produced it. `gate-arch010-cases.mjs` runs it
 * over real renderings from real production producers, which is what makes it a gate rather than
 * another unit test.
 *
 * ── it parses, and that is the point ────────────────────────────────────────
 *
 * ARCH-010 forbids BUSINESS logic that reads this text back. A validator is not business logic: it
 * asserts a property, it does not recover a domain object, and nothing downstream depends on its
 * output. Parseability is the only mechanically checkable property the notation has, so a checker that
 * declined to parse could verify almost nothing — it would be reduced to grepping for `#`, which
 * cannot tell an instruction from a `#` inside a tool's stdout.
 */

import { parse as parseToml } from 'smol-toml';

/** Field names that would be carrying an instruction, which ARCH-010 §5.1 forbids outright. */
const INSTRUCTION_FIELD_NAMES = Object.freeze([
  'instruction',
  'instructions',
  'action',
  'directive',
  'command',
  'task',
  'prompt',
  'guidance',
  'rule',
  'rules',
]);

/**
 * Split a document into structural lines and literal-string content.
 *
 * Everything downstream needs this distinction, and getting it wrong is the failure mode that would
 * make this checker worse than nothing. A `'''` body may legitimately contain `#`, `[[table]]`, and
 * `key = value` lines — that is precisely the containment ARCH-010 requires — so a naive scan would
 * report the renderer violating the rule it is in fact enforcing.
 *
 * Tracks literal blocks by structure, not by counting quotes: a line ending in `= '''` opens one, and
 * a line that is exactly `'''` closes it.
 *
 * `misplaced` is the third outcome and the one the old pre-ARCH-010 layout produces: a line that ENDS
 * in `'''` while carrying content closes the block in the wrong position. That document parses, so
 * nothing else would catch it, which is why it is collected rather than merely tolerated.
 */
export function splitDocument(document) {
  const syntax = [];
  const content = [];
  const misplaced = [];
  let inLiteral = false;

  document.split('\n').forEach((text, index) => {
    const line = { text, line: index + 1 };

    if (inLiteral) {
      if (text === "'''") {
        inLiteral = false;
        syntax.push(line);
        return;
      }

      if (text.endsWith("'''")) {
        inLiteral = false;
        misplaced.push(line);
        content.push(line);
        return;
      }

      content.push(line);
      return;
    }

    if (/=\s*'''$/.test(text)) {
      inLiteral = true;
      syntax.push(line);
      return;
    }

    syntax.push(line);
  });

  return { syntax, content, misplaced, unterminated: inLiteral };
}

/**
 * A structural line with its single-line string values blanked out.
 *
 * Required before any delimiter check, and the reason is measured: an injected payload containing
 * `'''` makes `renderString` fall back to a single-line basic string, so the document legitimately
 * holds `assignment = "… text = ''' …"` on ONE line. Reading that as a stray delimiter reported three
 * violations against a payload that was exactly right — and the tempting "fix" would have been to
 * drop the delimiter rule rather than to teach it what a value is.
 */
const withoutStringValues = (text) => text.replace(/"(?:[^"\\]|\\.)*"/g, '""');

/**
 * Check one rendered payload. Returns violation strings; empty means it conforms.
 *
 * Takes the document alone. A signature that also took the inputs would let a caller check "does this
 * match what I meant", which is a producer's own test, not this.
 */
export function auditPayload(document, { origin = 'payload' } = {}) {
  const violations = [];
  const at = (line, message) => violations.push(`${origin}:${line} ${message}`);

  if (document === '') return violations;

  // Parse first. Every rule below reads the document as TOML, so a parse failure makes the rest
  // meaningless rather than merely unchecked — reporting twenty derived violations for one malformed
  // string would bury the cause.
  try {
    parseToml(document);
  } catch (error) {
    violations.push(`${origin} does not parse as TOML: ${error.message}`);
    return violations;
  }

  const { syntax, content, misplaced, unterminated } = splitDocument(document);

  // `unterminated` is deliberately NOT branched on here, and the reason is measured rather than
  // assumed: an unterminated literal never parses, so the guard above has already returned. Every
  // shape tried — no closing delimiter, EOF mid-body, trailing text after the delimiter — fails parse
  // and reports the parse error. A branch for it would be a predicate nothing can reach, which is
  // indistinguishable from one that is wrong.
  //
  // It stays on `splitDocument`'s result because that IS a structural observation, and a future caller
  // reading the document without parsing first would need it.
  void unterminated;

  for (const line of misplaced) {
    at(line.line, "closes a ''' literal on a line carrying content; the closing delimiter must be alone on its line");
  }

  // ── the layout: instruction header, one blank line, data body ────────────

  const structural = syntax.filter(({ text }) => text !== '');
  const firstData = structural.findIndex(({ text }) => !text.startsWith('#'));
  const headerLines = firstData === -1 ? structural : structural.slice(0, firstData);
  const bodyLines = firstData === -1 ? [] : structural.slice(firstData);

  for (const line of bodyLines) {
    if (line.text.startsWith('#')) {
      at(
        line.line,
        'is a top-level comment after the data body began; ARCH-010 requires every instruction ' +
          'before the first field or table header',
      );
    }
  }

  if (headerLines.length > 0 && bodyLines.length > 0) {
    const lastHeader = headerLines[headerLines.length - 1].line;
    const firstBody = bodyLines[0].line;
    const between = document.split('\n').slice(lastHeader, firstBody - 1);

    if (between.length !== 1 || between[0] !== '') {
      at(
        firstBody,
        `is separated from the instruction header by ${between.length} line(s); ARCH-010 requires ` +
          'exactly one blank line',
      );
    }
  }

  // ── the delimiter rules ─────────────────────────────────────────────────
  //
  // Both scans read the line with its single-line string VALUES blanked out. Measured: an injected
  // payload containing `'''` makes `renderString` fall back to a single-line basic string, so the
  // document legitimately holds `assignment = "… text = ''' …"` on one line. Reading that as a stray
  // delimiter reported three violations against a payload that was exactly right, and the tempting
  // "fix" would have been to drop the rule rather than teach it what a value is.

  for (const line of syntax) {
    if (withoutStringValues(line.text).includes('"""')) {
      at(line.line, 'uses a """ multi-line string; ARCH-010 permits only \'\'\' (basic strings process escapes)');
    }
  }

  for (const line of syntax) {
    // A closing delimiter must be alone. `x = '''` opening a block is the only other legal position,
    // so anything else carrying `'''` outside a value is a delimiter sharing a line with data.
    // A comment line is exempt by construction: a closing delimiter is a bare `'''` line and an
    // opening is `x = '''`, neither of which starts with `#`. So `# '''` is an instruction
    // referencing the notation, which ARCH-010 permits — the same principle as the header-reference
    // case — not a delimiter sharing a line.
    const bare = withoutStringValues(line.text);

    if (!bare.includes("'''")) continue;
    if (line.text.startsWith('#')) continue;
    if (line.text === "'''") continue;
    if (/=\s*'''$/.test(bare)) continue;

    at(line.line, "carries ''' alongside other text; the closing delimiter must be alone on its line");
  }

  // ── instruction must not be a field ─────────────────────────────────────

  for (const line of bodyLines) {
    const name = /^([A-Za-z_][A-Za-z0-9_-]*)\s*=/.exec(line.text)?.[1];
    if (name === undefined) continue;

    if (INSTRUCTION_FIELD_NAMES.includes(name.toLowerCase())) {
      at(
        line.line,
        `assigns instruction-shaped field '${name}'; ARCH-010 requires instruction as a leading ` +
          'comment, and a field name however clear may not carry it',
      );
    }
  }

  // ── data must not be a top-level comment ────────────────────────────────
  //
  // Not mechanically decidable in general: whether a sentence is a rule or a record is semantic. What
  // IS decidable is the shape the clause's own counter-example takes — a comment holding a `key =
  // value` assertion is data wearing a comment's clothes.
  for (const line of headerLines) {
    const body = line.text.replace(/^#\s?/, '');
    if (/^[A-Za-z_][A-Za-z0-9_-]*\s*=\s*\S/.test(body)) {
      at(
        line.line,
        'is a comment containing a field assignment; ARCH-010 requires facts as fields, not as ' +
          'explanatory comments',
      );
    }
  }

  // Content lines are never checked for any of the above. They are values, and a value that looks
  // like syntax is the case containment exists to allow.
  void content;

  return violations;
}

/**
 * Round-trip one rendered string value: parse it back and compare.
 *
 * The expectation follows the renderer's own convention rather than predicting it — a multi-line
 * literal carries the newline before its closing delimiter, a single-line basic string carries none.
 * Which form an input takes is the writer's decision and is pinned by its own tests; this asserts only
 * that the value survived whichever was chosen.
 */
export function roundTripValue(rendered, original) {
  const parsed = parseToml(`x = ${rendered}`).x;
  const expected = rendered.startsWith("'''") ? `${original}\n` : original;

  return parsed === expected
    ? null
    : `round trip changed the value: expected ${JSON.stringify(expected)}, got ${JSON.stringify(parsed)}`;
}
