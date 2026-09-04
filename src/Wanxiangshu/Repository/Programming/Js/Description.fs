namespace Wanxiangshu.Repository.Programming.Js

/// Canonical example with capability requirements (proposal §44).
type JsExample =
    { Requires: Set<JsCapability>
      Source: string }

/// Proposal §12/§16/§31–§47: LLM-visible description is header + exact public
/// base class + capability rules + one Ultra Example + footer. Runtime proxy
/// class stays out of the description. Prose is already-localized (PROMPT-019).
module JsCanonicalDescription =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Header = "tool/js-program/header"

        [<Literal>]
        let EditPrelude = "tool/js-program/edit-prelude"

        [<Literal>]
        let EditStructuralGuidance = "tool/js-program/edit-structural-guidance"

        [<Literal>]
        let Footer = "tool/js-program/footer"

        [<Literal>]
        let Contract = "tool/js-program/contract"

        [<Literal>]
        let ContractEditRecommendation = "tool/js-program/contract-edit-recommendation"

        [<Literal>]
        let ContractParallelEdits = "tool/js-program/contract-parallel-edits"

        [<Literal>]
        let ContractParallelReads = "tool/js-program/contract-parallel-reads"

        [<Literal>]
        let VerbRead = "tool/js-program/verb-read"

        [<Literal>]
        let VerbSearch = "tool/js-program/verb-search"

        [<Literal>]
        let VerbTransform = "tool/js-program/verb-transform"

        [<Literal>]
        let VerbRewrite = "tool/js-program/verb-rewrite"

        [<Literal>]
        let VerbCreate = "tool/js-program/verb-create"

        [<Literal>]
        let ReadRules = "tool/js-program/rules-read"

        [<Literal>]
        let GlobRules = "tool/js-program/rules-glob"

        [<Literal>]
        let GrepRules = "tool/js-program/rules-grep"

        [<Literal>]
        let EditRules = "tool/js-program/rules-edit"

        [<Literal>]
        let WriteRules = "tool/js-program/rules-write"

        [<Literal>]
        let MutationRules = "tool/js-program/rules-mutation"

        [<Literal>]
        let UltraFraming = "tool/js-program/ultra-framing"

        [<Literal>]
        let UltraUnavailable = "tool/js-program/ultra-unavailable"

        [<Literal>]
        let UltraCoder = "tool/js-program/ultra-coder"

        [<Literal>]
        let MechanicalSemantic = "tool/js-program/mechanical-semantic"

        [<Literal>]
        let CommentAnchorOwnSearch = "tool/js-program/comment-anchor-own-search"

        [<Literal>]
        let CommentIgnoreGy = "tool/js-program/comment-ignore-gy"

        [<Literal>]
        let CommentHostCapability = "tool/js-program/comment-host-capability"

        [<Literal>]
        let ReasonEmptyStringPattern = "tool/js-program/reason-empty-string-pattern"

        [<Literal>]
        let ReasonInvalidRegexp = "tool/js-program/reason-invalid-regexp"

        [<Literal>]
        let ReasonPatternType = "tool/js-program/reason-pattern-type"

        [<Literal>]
        let ReasonAnchorEmptyNames = "tool/js-program/reason-anchor-empty-names"

        [<Literal>]
        let ReasonAnchorReserved = "tool/js-program/reason-anchor-reserved"

        [<Literal>]
        let ReasonAnchorNamesDiffer = "tool/js-program/reason-anchor-names-differ"

        [<Literal>]
        let ReasonAnchorNamesUnique = "tool/js-program/reason-anchor-names-unique"

        [<Literal>]
        let ReasonAnchorNotFound = "tool/js-program/reason-anchor-not-found"

        [<Literal>]
        let ReasonUnknownAnchor = "tool/js-program/reason-unknown-anchor"

        [<Literal>]
        let ReasonInvalidSlice = "tool/js-program/reason-invalid-slice"

        [<Literal>]
        let ReasonFileReadFailed = "tool/js-program/reason-file-read-failed"

        [<Literal>]
        let ReasonRunUnimplemented = "tool/js-program/reason-run-unimplemented"

        [<Literal>]
        let ReasonEditInvalid = "tool/js-program/reason-edit-invalid"

        [<Literal>]
        let ReasonEditNotFound = "tool/js-program/reason-edit-not-found"

        [<Literal>]
        let ReasonEditAmbiguous = "tool/js-program/reason-edit-ambiguous"

        [<Literal>]
        let ReasonEditOverlap = "tool/js-program/reason-edit-overlap"

        [<Literal>]
        let ReasonEditAtomicity = "tool/js-program/reason-edit-atomicity"

        [<Literal>]
        let ReasonEditPreview = "tool/js-program/reason-edit-preview"

        [<Literal>]
        let ReasonEditCopyReady = "tool/js-program/reason-edit-copy-ready"

        [<Literal>]
        let ReasonEditAttempted = "tool/js-program/reason-edit-attempted"

        [<Literal>]
        let ReasonEditUnknownFields = "tool/js-program/reason-edit-unknown-fields"

        [<Literal>]
        let ReasonEditCandidate = "tool/js-program/reason-edit-candidate"

        [<Literal>]
        let ReasonEditLine = "tool/js-program/reason-edit-line"

        [<Literal>]
        let ReasonEditOverlaps = "tool/js-program/reason-edit-overlaps"

        [<Literal>]
        let ArgProgram = "tool/js-program/arg-program"

        [<Literal>]
        let MissingProgram = "tool/js-program/missing-program"

        [<Literal>]
        let HookNotVisible = "tool/js-program/hook-not-visible"

    type Prose =
        { Header: string
          EditPrelude: string
          EditStructuralGuidance: string
          Footer: string
          Contract: string
          ContractEditRecommendation: string
          ContractParallelEdits: string
          ContractParallelReads: string
          VerbRead: string
          VerbSearch: string
          VerbTransform: string
          VerbRewrite: string
          VerbCreate: string
          ReadRules: string
          GlobRules: string
          GrepRules: string
          EditRules: string
          WriteRules: string
          MutationRules: string
          UltraFraming: string
          UltraUnavailable: string
          UltraCoder: string
          MechanicalSemantic: string
          CommentAnchorOwnSearch: string
          CommentIgnoreGy: string
          CommentHostCapability: string
          ReasonEmptyStringPattern: string
          ReasonInvalidRegexp: string
          ReasonPatternType: string
          ReasonAnchorEmptyNames: string
          ReasonAnchorReserved: string
          ReasonAnchorNamesDiffer: string
          ReasonAnchorNamesUnique: string
          ReasonAnchorNotFound: string
          ReasonUnknownAnchor: string
          ReasonInvalidSlice: string
          ReasonFileReadFailed: string
          ReasonRunUnimplemented: string
          ReasonEditInvalid: string
          ReasonEditNotFound: string
          ReasonEditAmbiguous: string
          ReasonEditOverlap: string
          ReasonEditAtomicity: string
          ReasonEditPreview: string
          ReasonEditCopyReady: string
          ReasonEditAttempted: string
          ReasonEditUnknownFields: string
          ReasonEditCandidate: string
          ReasonEditLine: string
          ReasonEditOverlaps: string }

    let private fill (template: string) (pairs: (string * string) list) =
        let replaced =
            (template, pairs)
            ||> List.fold (fun acc (key, value) -> acc.Replace("{{" + key + "}}", value))

        if replaced.Contains("{{") then
            invalidOp "PROMPT-019:unsubstituted-placeholder"

        replaced

    let private fileReasons (prose: Prose) =
        [ "reason_empty_string_pattern", prose.ReasonEmptyStringPattern
          "reason_invalid_regexp", prose.ReasonInvalidRegexp
          "reason_pattern_type", prose.ReasonPatternType
          "reason_anchor_empty_names", prose.ReasonAnchorEmptyNames
          "reason_anchor_reserved", prose.ReasonAnchorReserved
          "reason_anchor_names_differ", prose.ReasonAnchorNamesDiffer
          "reason_anchor_names_unique", prose.ReasonAnchorNamesUnique
          "reason_anchor_not_found", prose.ReasonAnchorNotFound
          "reason_unknown_anchor", prose.ReasonUnknownAnchor
          "reason_invalid_slice", prose.ReasonInvalidSlice
          "comment_anchor_own_search", prose.CommentAnchorOwnSearch
          "comment_ignore_gy", prose.CommentIgnoreGy ]

    /// Proposal §16 canonical file() body (description uses HOST_READ).
    /// `raiseFailure` closes over `code` and `reason`.
    let fileAlgorithm (prose: Prose) (readSourceLine: string) (raiseFailure: string) =
        fill
            """  async file(path, matches = []) {
    const fail = (code, reason) => {
      {{raise_failure}}
    };
    {{read_source}}

    const anchors = new Map([
      ["^", 0],
      ["$", source.length],
    ]);

    let cursor = 0;

    const preview = pattern => {
      if (typeof pattern === "string") {
        return pattern.length > 80
          ? JSON.stringify(pattern.slice(0, 80)) + "…"
          : JSON.stringify(pattern);
      }

      if (pattern instanceof RegExp) {
        const shown = "/" + pattern.source + "/";
        return shown.length > 80 ? shown.slice(0, 80) + "…" : shown;
      }

      return String(pattern);
    };

    const locatePattern = pattern => {
      if (typeof pattern === "string") {
        if (pattern.length === 0)
          fail("EMPTY_ANCHOR_CONTENT", "{{reason_empty_string_pattern}}");

        const start = source.indexOf(pattern, cursor);

        if (start < 0)
          return null;

        return {
          start,
          end: start + pattern.length,
        };
      }

      if (pattern instanceof RegExp) {
        // {{comment_anchor_own_search}}
        // {{comment_ignore_gy}}
        const flags =
          [...new Set(
            pattern.flags.replace(/[gy]/g, "") + "g"
          )].join("");

        let regexp;
        try {
          regexp = new RegExp(pattern.source, flags);
        } catch {
          fail("INVALID_ANCHOR_PATTERN", "{{reason_invalid_regexp}}");
        }

        regexp.lastIndex = cursor;

        const match = regexp.exec(source);

        if (!match)
          return null;

        return {
          start: match.index,
          end: match.index + match[0].length,
        };
      }

      fail("INVALID_ANCHOR_PATTERN", "{{reason_pattern_type}}");
    };

    let declaration = 0;

    for (const [begin, end, pattern] of matches) {
      declaration += 1;

      if (!begin || !end)
        fail("EMPTY_ANCHOR_CONTENT", `{{reason_anchor_empty_names}}`);

      if (
        begin === "^" || begin === "$" ||
        end === "^" || end === "$"
      )
        fail("INVALID_ANCHOR_PATTERN", `{{reason_anchor_reserved}}`);

      if (begin === end)
        fail(
          "INVALID_ANCHOR_PATTERN",
          `{{reason_anchor_names_differ}}`
        );

      if (anchors.has(begin) || anchors.has(end))
        fail("INVALID_ANCHOR_PATTERN", `{{reason_anchor_names_unique}}`);

      const match = locatePattern(pattern);

      if (!match)
        fail(
          "ANCHOR_NOT_FOUND",
          `{{reason_anchor_not_found}}`
        );

      anchors.set(begin, match.start);
      anchors.set(end, match.end);

      cursor = match.end;
    }

    const clip = n => Math.min(Math.max(0, n), source.length);

    const offset = name => {
      if (anchors.has(name))
        return anchors.get(name);

      const shifted = /^(.*)([+-]\d+)$/.exec(name);

      if (!shifted || shifted[1].length === 0)
        fail("ANCHOR_NOT_FOUND", `{{reason_unknown_anchor}}`);

      return clip(offset(shifted[1]) + Number(shifted[2]));
    };

    return Object.freeze({
      text(from = "^", to = "$") {
        const start = offset(from);
        const end = offset(to);

        if (start > end)
          fail("INVALID_ANCHOR_PATTERN", `{{reason_invalid_slice}}`);

        return source.slice(start, end);
      },
    });
  }"""
            (("raise_failure", raiseFailure)
             :: ("read_source", readSourceLine)
             :: fileReasons prose)

    let private globStub (prose: Prose) =
        fill
            """  async glob(pattern) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private grepStub (prose: Prose) =
        fill
            """
  async grep(needle, pattern = "**/*") {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]
        |> fun s -> s.TrimStart('\n')

    let private rewriteStub (prose: Prose) =
        fill
            """  rewrite(path, newText) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private editStub (prose: Prose) =
        fill
            """  edit(path, changes) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private writeStub (prose: Prose) =
        fill
            """  write(path, newText) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private runStub (prose: Prose) =
        fill
            """  async run() {
    throw new Error("{{reason_run_unimplemented}}");
  }"""
            [ "reason_run_unimplemented", prose.ReasonRunUnimplemented ]

    let has (capabilities: Set<JsCapability>) (capability: JsCapability) = Set.contains capability capabilities

    let private classOpen =
        """
class JsProgram {"""
            .TrimStart()

    let private publicReadSource =
        """
    const source = await HOST_READ_IMMUTABLE_UTF8_SNAPSHOT(path);"""
            .Trim()

    let private publicRaiseFailure =
        """
      throw new Error(reason);"""
            .Trim()

    let publicBaseClass (prose: Prose) (capabilities: Set<JsCapability>) : string =
        let methods =
            [ if has capabilities JsCapability.Read then
                  fileAlgorithm prose publicReadSource publicRaiseFailure
              if has capabilities JsCapability.Glob then
                  globStub prose
              if has capabilities JsCapability.Grep then
                  grepStub prose
              if has capabilities JsCapability.Edit then
                  editStub prose
              if has capabilities JsCapability.Edit then
                  rewriteStub prose
              if has capabilities JsCapability.Write then
                  writeStub prose
              runStub prose ]

        String.concat "\n\n" (classOpen :: methods @ [ "}" ])

    let private rethrowHost =
        """if (result && result.ok === false) {
      const err = new Error(result.reason || result.code);
      err.__jsFailure = { code: result.code, reason: result.reason || result.code };
      throw err;
    }"""

    let private runtimeReadSource (prose: Prose) =
        fill
            """const snapshot = this._api.js.read(path);
    if (snapshot && snapshot.ok === false) fail(snapshot.code || "FILE_READ_FAILED", snapshot.reason || snapshot.code || "{{reason_file_read_failed}}");
    const source = snapshot.text;"""
            [ "reason_file_read_failed", prose.ReasonFileReadFailed ]

    let private runtimeRaiseFailure =
        """
const err = new Error(reason); err.__jsFailure = { code, reason }; throw err;"""
            .Trim()

    /// High-level Edit affordance. It deliberately lives in the generated SDK
    /// rather than a second Host mutation primitive: matching is pure and the
    /// only effect remains the existing guarded js.edit staging executor.
    /// Every change addresses the same immutable snapshot; approximate
    /// similarity is diagnostic-only and can never authorize a write.
    let private editAlgorithm (prose: Prose) (readSourceLine: string) (raiseFailure: string) =
        fill
            """  edit(path, changes) {
    const fail = (code, reason) => {
      {{raise_failure}}
    };
    const bounded = (value, limit = 240) => {
      let text;
      try {
        text = typeof value === "string" ? value : String(value);
      } catch {
        text = "<unprintable>";
      }
      return text.length > limit ? text.slice(0, limit) + "…" : text;
    };
    const shownPath =
      typeof path === "string" ? JSON.stringify(bounded(path)) : bounded(path);
    const atomicity = "{{reason_edit_atomicity}}";
    const failEdit = (code, message, operation, details = []) => {
      const location = `path=${shownPath}; change ${operation}.`;
      fail(code, [message, location, ...details, atomicity].join("\n"));
    };

    if (typeof path !== "string" || path.length === 0) {
      failEdit("INVALID_EDIT", "{{reason_edit_invalid}}", 1);
    }

    const declarations = Array.isArray(changes) ? changes : [changes];

    if (declarations.length === 0) {
      failEdit("INVALID_EDIT", "{{reason_edit_invalid}}", 1);
    }

    const own = (value, name) => Object.prototype.hasOwnProperty.call(value, name);
    const allowedFields = new Set([
      "find", "put", "all", "oldText", "newText", "search", "replace",
    ]);
    const describeFind = find => {
      if (typeof find === "string") return JSON.stringify(bounded(find, 320));
      if (find instanceof RegExp) {
        return `/${bounded(find.source, 300)}/${bounded(find.flags, 20)}`;
      }
      return bounded(find, 320);
    };
    const aliased = (change, names, operation) => {
      const present = names.filter(name => own(change, name));
      if (present.length === 0) {
        failEdit("INVALID_EDIT", "{{reason_edit_invalid}}", operation);
      }
      const value = change[present[0]];
      if (present.some(name => change[name] !== value)) {
        failEdit("INVALID_EDIT", "{{reason_edit_invalid}}", operation);
      }
      return value;
    };

    // Normalize the complete declaration before touching the filesystem. A
    // malformed change must not be masked by FILE_NOT_FOUND or create a read
    // observation that never contributed to a valid edit plan.
    const canonicalChanges = [];
    let declarationOrdinal = 0;
    for (const declaration of declarations) {
      declarationOrdinal += 1;
      if (!declaration || typeof declaration !== "object" || Array.isArray(declaration)) {
        failEdit("INVALID_EDIT", "{{reason_edit_invalid}}", declarationOrdinal);
      }
      const prototype = Object.getPrototypeOf(declaration);
      if (prototype !== Object.prototype && prototype !== null) {
        failEdit("INVALID_EDIT", "{{reason_edit_invalid}}", declarationOrdinal);
      }
      const unknownFields = Object.keys(declaration).filter(field => !allowedFields.has(field));
      if (unknownFields.length > 0) {
        const shownUnknownFields = unknownFields
          .slice(0, 8)
          .map(field => bounded(field, 80))
          .join(", ") + (unknownFields.length > 8 ? ", …" : "");
        failEdit(
          "INVALID_EDIT",
          "{{reason_edit_invalid}}",
          declarationOrdinal,
          [`{{reason_edit_unknown_fields}} ${shownUnknownFields}`]
        );
      }

      const find = aliased(declaration, ["find", "oldText", "search"], declarationOrdinal);
      const put = aliased(declaration, ["put", "newText", "replace"], declarationOrdinal);
      if (typeof put !== "string") {
        failEdit("INVALID_EDIT", "{{reason_edit_invalid}}", declarationOrdinal);
      }
      if (own(declaration, "all") && typeof declaration.all !== "boolean") {
        failEdit("INVALID_EDIT", "{{reason_edit_invalid}}", declarationOrdinal);
      }
      if (typeof find === "string") {
        if (find.length === 0) {
          failEdit("INVALID_EDIT", "{{reason_edit_invalid}}", declarationOrdinal);
        }
      } else if (!(find instanceof RegExp)) {
        failEdit("INVALID_EDIT", "{{reason_edit_invalid}}", declarationOrdinal);
      }

      canonicalChanges.push({
        find,
        put,
        all: declaration.all === true,
        operation: declarationOrdinal,
      });
    }

    {{read_source}}

    // A consistent CRLF file is matched in LF space and restored after the
    // plan is applied. Mixed-EOL files remain byte-for-byte exact.
    const consistentCrlf =
      source.includes("\r\n") && !source.replace(/\r\n/g, "").includes("\n");
    const working = consistentCrlf ? source.replace(/\r\n/g, "\n") : source;
    const normalizeAuthored = text =>
      consistentCrlf ? text.replace(/\r\n/g, "\n") : text;
    const restoreEol = text =>
      consistentCrlf ? text.replace(/\n/g, "\r\n") : text;

    const sourceLines = working.split("\n");
    const lineStarts = [0];
    for (let i = 0; i < working.length; i += 1) {
      if (working[i] === "\n") lineStarts.push(i + 1);
    }

    const lineIndexAt = offset => {
      let low = 0;
      let high = lineStarts.length - 1;
      while (low <= high) {
        const mid = (low + high) >> 1;
        if (lineStarts[mid] <= offset) low = mid + 1;
        else high = mid - 1;
      }
      return Math.max(0, high);
    };

    const clipLine = (line, column = 0) => {
      const limit = 240;
      if (line.length <= limit) return line;
      const start = Math.min(
        Math.max(0, column - 80),
        Math.max(0, line.length - limit)
      );
      const end = Math.min(line.length, start + limit);
      return (start > 0 ? "…" : "")
        + line.slice(start, end)
        + (end < line.length ? "…" : "");
    };

    const numberedWindow = (offset, radius = 2) => {
      const center = lineIndexAt(offset);
      const centerColumn = Math.max(0, offset - lineStarts[center]);
      const from = Math.max(0, center - radius);
      const to = Math.min(sourceLines.length, center + radius + 1);
      return sourceLines
        .slice(from, to)
        .map((line, index) => {
          const lineIndex = from + index;
          const column = lineIndex === center ? centerColumn : 0;
          return `${lineIndex + 1} | ${clipLine(line, column)}`;
        })
        .join("\n");
    };

    const compact = value =>
      value.slice(0, 480).replace(/\s+/g, " ").trim().slice(0, 240);

    // Bounded Dice similarity is cheap enough for diagnostics on large files.
    // It never participates in the mutation plan.
    const similarity = (left, right) => {
      const a = compact(left);
      const b = compact(right);
      if (a === b) return 1;
      if (a.length === 0 || b.length === 0) return 0;
      if (a.length === 1 || b.length === 1) return a === b ? 1 : 0;

      const grams = value => {
        const counts = new Map();
        for (let i = 0; i < value.length - 1; i += 1) {
          const gram = value.slice(i, i + 2);
          counts.set(gram, (counts.get(gram) || 0) + 1);
        }
        return counts;
      };

      const aa = grams(a);
      const bb = grams(b);
      let shared = 0;
      for (const [gram, count] of aa) {
        shared += Math.min(count, bb.get(gram) || 0);
      }
      return (2 * shared) / (Math.max(1, a.length - 1) + Math.max(1, b.length - 1));
    };

    const closestCandidate = authoredNeedle => {
      const needle = normalizeAuthored(authoredNeedle);
      if (compact(needle).length === 0 || working.length === 0) return null;

      // Diagnostics are bounded independently of file size. Stable tokens find
      // plausible locations; exact-length-neighbourhood scoring chooses an
      // existing substring. Neither score nor candidate ever enters the plan.
      const searchNeedle = needle.length <= 4000 ? needle : needle.slice(0, 4000);
      const tokenPattern = /[\p{L}\p{N}_$.-]{3,}/gu;
      const seenTokens = new Set();
      const tokens = [];
      let tokenMatch;
      while ((tokenMatch = tokenPattern.exec(searchNeedle)) !== null) {
        if (!seenTokens.has(tokenMatch[0])) {
          seenTokens.add(tokenMatch[0]);
          tokens.push({ text: tokenMatch[0], offset: tokenMatch.index });
        }
      }

      const occurrences = (token, limit = 64) => {
        const offsets = [];
        let cursor = 0;
        while (offsets.length < limit && cursor <= working.length - token.length) {
          const at = working.indexOf(token, cursor);
          if (at < 0) break;
          offsets.push(at);
          cursor = at + Math.max(1, token.length);
        }
        return offsets;
      };

      const anchors = tokens
        .sort((left, right) => right.text.length - left.text.length)
        .slice(0, 24)
        .map(token => ({ ...token, occurrences: occurrences(token.text) }))
        .filter(token => token.occurrences.length > 0)
        .sort((left, right) =>
          left.occurrences.length - right.occurrences.length
          || right.text.length - left.text.length
        );

      let bases;
      if (anchors.length > 0) {
        const anchor = anchors[0];
        bases = anchor.occurrences.map(offset => offset - anchor.offset);
      } else {
        const count = Math.min(96, lineStarts.length);
        bases = [];
        for (let i = 0; i < count; i += 1) {
          const index = count === 1
            ? 0
            : Math.floor((i * (lineStarts.length - 1)) / (count - 1));
          bases.push(lineStarts[index]);
        }
      }

      const delta = Math.min(24, Math.max(3, Math.ceil(searchNeedle.length * 0.12)));
      const shifts = [...new Set([0, -1, 1, -2, 2, -4, 4, -delta, delta])];
      const groups = [];
      for (const base of bases) {
        let groupBest = null;
        for (const shift of shifts) {
          const start = Math.min(Math.max(0, base + shift), Math.max(0, working.length - 1));
          const shortest = Math.max(1, searchNeedle.length - delta);
          const longest = Math.min(working.length - start, searchNeedle.length + delta);
          for (let length = shortest; length <= longest; length += 1) {
            const end = start + length;
            const candidatePrefix = working.slice(start, Math.min(end, start + 480));
            const score = similarity(searchNeedle, candidatePrefix);
            if (!groupBest || score > groupBest.score) {
              groupBest = { start, end, score };
            }
          }
        }
        if (groupBest) groups.push(groupBest);
      }

      groups.sort((left, right) => right.score - left.score);
      const best = groups[0];
      if (!best) return null;
      const secondScore = groups.length > 1 ? groups[1].score : -1;
      const confident = needle.length <= 480
        && best.score >= 0.72
        && (best.score >= 0.92 || best.score - secondScore >= 0.08);
      return {
        offset: best.start,
        corrected: working.slice(best.start, best.end),
        confident,
      };
    };

    const stringMatches = (needle, collectAll) => {
      const matches = [];
      let cursor = 0;
      while (cursor <= working.length - needle.length) {
        const start = working.indexOf(needle, cursor);
        if (start < 0) break;
        matches.push({ start, end: start + needle.length });
        if (!collectAll && matches.length === 2) break;
        cursor = start + needle.length;
      }
      return matches;
    };

    const regexpMatches = (pattern, operation, collectAll) => {
      // g is call-site multiplicity and is replaced by all; sticky y is a
      // positional constraint and must remain part of write authority.
      const flags = [...new Set(pattern.flags.replace(/g/g, "") + "g")].join("");
      let regexp;
      try {
        regexp = new RegExp(pattern.source, flags);
      } catch {
        failEdit("INVALID_EDIT", "{{reason_edit_invalid}}", operation);
      }

      const matches = [];
      let match;
      while ((match = regexp.exec(working)) !== null) {
        if (match[0].length === 0) {
          failEdit("INVALID_EDIT", "{{reason_edit_invalid}}", operation);
        }
        matches.push({ start: match.index, end: match.index + match[0].length });
        if (!collectAll && matches.length === 2) break;
      }
      return matches;
    };

    const planned = [];
    for (const declaration of canonicalChanges) {
      const { find, put, all, operation } = declaration;
      let matches;
      let normalizedFind = find;
      if (typeof find === "string") {
        normalizedFind = normalizeAuthored(find);
        matches = stringMatches(normalizedFind, all);
      } else if (find instanceof RegExp) {
        matches = regexpMatches(find, operation, all);
      }

      if (matches.length === 0) {
        const details = ["{{reason_edit_attempted}}", describeFind(find)];
        if (typeof normalizedFind === "string") {
          const closest = closestCandidate(normalizedFind);
          if (closest) {
            details.push("{{reason_edit_preview}}", numberedWindow(closest.offset));
            if (closest.confident && closest.corrected.length + put.length <= 3000) {
              const corrected = { find: closest.corrected, put };
              if (all) corrected.all = true;
              details.push("{{reason_edit_copy_ready}}", JSON.stringify(corrected, null, 2));
            }
          }
        }
        failEdit("EDIT_NOT_FOUND", "{{reason_edit_not_found}}", operation, details);
      }

      if (!all && matches.length !== 1) {
        const details = ["{{reason_edit_attempted}}", describeFind(find)];
        for (const [index, match] of matches.slice(0, 4).entries()) {
          details.push(
            `{{reason_edit_candidate}} ${index + 1}: {{reason_edit_line}} ${lineIndexAt(match.start) + 1}.`,
            numberedWindow(match.start, 1)
          );
        }
        failEdit("EDIT_AMBIGUOUS", "{{reason_edit_ambiguous}}", operation, details);
      }

      const selected = all ? matches : [matches[0]];
      const replacement = normalizeAuthored(put);
      for (const match of selected) {
        planned.push({ ...match, operation, put: replacement });
      }
    }

    const ascending = [...planned].sort(
      (left, right) => left.start - right.start || left.end - right.end
    );
    for (let i = 1; i < ascending.length; i += 1) {
      const previous = ascending[i - 1];
      const current = ascending[i];
      if (current.start < previous.end) {
        failEdit(
          "EDIT_OVERLAP",
          "{{reason_edit_overlap}}",
          current.operation,
          [`{{reason_edit_overlaps}} ${previous.operation}; {{reason_edit_line}} ${lineIndexAt(current.start) + 1}.`]
        );
      }
    }

    let edited = working;
    const descending = [...planned].sort(
      (left, right) => right.start - left.start || right.end - left.end
    );
    for (const change of descending) {
      edited = edited.slice(0, change.start) + change.put + edited.slice(change.end);
    }

    const finalText = restoreEol(edited);
    if (finalText === source) {
      return Object.freeze({
        path,
        changed: false,
        operations: declarations.length,
        replacements: planned.length,
      });
    }

    const result = this._api.js.edit(path, finalText);
    {{rethrow_host}}
    return Object.freeze({
      path,
      changed: true,
      operations: declarations.length,
      replacements: planned.length,
    });
  }"""
            [ "raise_failure", raiseFailure
              "read_source", readSourceLine
              "rethrow_host", rethrowHost
              "reason_edit_invalid", prose.ReasonEditInvalid
              "reason_edit_not_found", prose.ReasonEditNotFound
              "reason_edit_ambiguous", prose.ReasonEditAmbiguous
              "reason_edit_overlap", prose.ReasonEditOverlap
              "reason_edit_atomicity", prose.ReasonEditAtomicity
              "reason_edit_preview", prose.ReasonEditPreview
              "reason_edit_copy_ready", prose.ReasonEditCopyReady
              "reason_edit_attempted", prose.ReasonEditAttempted
              "reason_edit_unknown_fields", prose.ReasonEditUnknownFields
              "reason_edit_candidate", prose.ReasonEditCandidate
              "reason_edit_line", prose.ReasonEditLine
              "reason_edit_overlaps", prose.ReasonEditOverlaps ]

    let runtimeBaseClass (prose: Prose) (capabilities: Set<JsCapability>) : string =
        let methods =
            [ """
  constructor(api) { this._api = api; }"""
                  .TrimStart()
              if has capabilities JsCapability.Read then
                  fileAlgorithm prose (runtimeReadSource prose) runtimeRaiseFailure
              if has capabilities JsCapability.Glob then
                  fill
                      """  async glob(pattern) {
    const result = this._api.js.glob(pattern);
    {{rethrow_host}}
    return { paths: result.paths };
  }"""
                      [ "rethrow_host", rethrowHost ]
              if has capabilities JsCapability.Grep then
                  fill
                      """
  async grep(needle, pattern = "**/*") {
    const result = this._api.js.grep(needle, pattern);
    {{rethrow_host}}
    return { matches: result.matches };
  }"""
                      [ "rethrow_host", rethrowHost ]
                  |> fun s -> s.TrimStart('\n')
              if has capabilities JsCapability.Edit then
                  editAlgorithm prose (runtimeReadSource prose) runtimeRaiseFailure
              if has capabilities JsCapability.Edit then
                  fill
                      """  rewrite(path, newText) {
    const result = this._api.js.edit(path, newText);
    {{rethrow_host}}
  }"""
                      [ "rethrow_host", rethrowHost ]
              if has capabilities JsCapability.Write then
                  fill
                      """  write(path, newText) {
    const result = this._api.js.write(path, newText);
    {{rethrow_host}}
  }"""
                      [ "rethrow_host", rethrowHost ]
              runStub prose ]

        String.concat "\n\n" (classOpen :: methods @ [ "}" ])

    let contract (prose: Prose) (toolName: string) (capabilities: Set<JsCapability>) =
        let verbs =
            List.concat
                [ if has capabilities JsCapability.Read then
                      [ prose.VerbRead ]
                  else
                      []
                  if has capabilities JsCapability.Grep then
                      [ prose.VerbSearch ]
                  elif has capabilities JsCapability.Read && has capabilities JsCapability.Glob then
                      [ prose.VerbSearch ]
                  else
                      []
                  if has capabilities JsCapability.Edit then
                      [ prose.VerbTransform; prose.VerbRewrite ]
                  else
                      []
                  if has capabilities JsCapability.Write then
                      [ prose.VerbCreate ]
                  else
                      [] ]
            |> String.concat ", "

        let parallelTemplate =
            if has capabilities JsCapability.Edit || has capabilities JsCapability.Write then
                prose.ContractParallelEdits
            else
                prose.ContractParallelReads

        fill
            prose.Contract
            [ "toolName", toolName
              "verbs", verbs
              "editRecommendation",
              if has capabilities JsCapability.Edit then
                  prose.ContractEditRecommendation
              else
                  ""
              "parallelLine", fill parallelTemplate [ "toolName", toolName ] ]

    let rules (prose: Prose) (capabilities: Set<JsCapability>) : string =
        let blocks =
            [ if has capabilities JsCapability.Read then
                  prose.ReadRules
              if has capabilities JsCapability.Glob then
                  prose.GlobRules
              if has capabilities JsCapability.Grep then
                  prose.GrepRules
              if has capabilities JsCapability.Edit then
                  prose.EditRules
              if has capabilities JsCapability.Write then
                  prose.WriteRules
              if has capabilities JsCapability.Edit || has capabilities JsCapability.Write then
                  prose.MutationRules ]

        String.concat "\n\n" blocks

    let private inspectorUltra =
        """class Js extends JsProgram {
  async run() {
    const declarations = await this.grep(/\b(?:type|module)\s+RetryPolicy\b/, "src/**/*.fs");
    const paths = [...new Set(declarations.matches.map(x => x.path))];
    if (paths.length === 0) {
      const usages = await this.grep(/\bRetryPolicy\b/, "{src,tests}/**/*.fs");
      return { declarations: [], usages: usages.matches };
    }
    const evidence = await Promise.all(paths.map(async path => {
      try {
        const file = await this.file(path, [["hit", "afterHit", /\b(?:type|module)\s+RetryPolicy\b/]]);
        return { path, excerpt: file.text("hit-220", "hit+900"), anchorMatched: true };
      } catch {
        const file = await this.file(path);
        return { path, excerpt: file.text("^", "^+1100"), anchorMatched: false };
      }
    }));
    return { declarations: declarations.matches, evidence };
  }
}"""

    let private managerUltra =
        """class Js extends JsProgram {
  async run() {
    const stale = await this.grep(/\boldApi\b/, "src/**/*.{js,ts}");
    if (stale.matches.length > 0) {
      const paths = [...new Set(stale.matches.map(x => x.path))].slice(0, 6);
      const evidence = await Promise.all(paths.map(async path => {
        const file = await this.file(path);
        return { path, excerpt: file.text("^", "^+900") };
      }));
      return { staleReferences: stale.matches, evidence };
    }
    const migrated = await this.grep(/\bnewApi\b/, "src/**/*.{js,ts}");
    return { staleReferences: [], migratedReferences: migrated.matches };
  }
}"""

    let private devOpsUltra =
        """class Js extends JsProgram {
  async run() {
    const manifests = await this.glob("package.json");
    if (!manifests.paths.includes("package.json")) return { rootPackage: null };
    const pkg = JSON.parse((await this.file("package.json")).text());
    const testScript = pkg.scripts?.test ?? null;
    if (!testScript) return { rootPackage: "package.json", testScript: null, scripts: Object.keys(pkg.scripts || {}) };
    const tests = await this.glob("tests/**/*recovery*.{test,spec}.{js,ts,mjs}");
    if (tests.paths.length === 0) {
      const hits = await this.grep(/RecoveryClosure|recovery/i, "tests/**/*.{js,ts,mjs}");
      return { testScript, candidateTests: [...new Set(hits.matches.map(x => x.path))] };
    }
    return {
      packageManager: typeof pkg.packageManager === "string" ? pkg.packageManager : null,
      testScript,
      candidateTests: tests.paths,
    };
  }
}"""

    let private browserUltra =
        """class Js extends JsProgram {
  async run() {
    const hits = await this.grep(/\bWidgetOptions\b/, "artifacts/web/**/*.md");
    if (hits.matches.length === 0) {
      const indirect = await this.grep(/widget options|configuration object|deprecated/i, "artifacts/web/**/*.md");
      return { exact: [], indirect: indirect.matches };
    }
    const paths = [...new Set(hits.matches.map(x => x.path))];
    const sources = await Promise.all(paths.map(async path => {
      const file = await this.file(path);
      const text = file.text();
      const at = text.search(/\bWidgetOptions\b/);
      return {
        path,
        url: /^URL:\s*(.+)$/m.exec(text)?.[1]?.trim() ?? null,
        version: /^Version:\s*(.+)$/m.exec(text)?.[1]?.trim() ?? null,
        excerpt: text.slice(Math.max(0, at - 250), at + 1000),
      };
    }));
    return { sources };
  }
}"""

    let ultraExample (prose: Prose) (roleName: string) (capabilities: Set<JsCapability>) : JsExample option =
        let candidate =
            match roleName.Trim().ToLowerInvariant() with
            | "coder" -> Some(set [ JsCapability.Read; JsCapability.Grep; JsCapability.Edit ], prose.UltraCoder)
            | "inspector" -> Some(set [ JsCapability.Read; JsCapability.Grep ], inspectorUltra)
            | "manager" -> Some(set [ JsCapability.Read; JsCapability.Grep ], managerUltra)
            | "devops" -> Some(set [ JsCapability.Read; JsCapability.Glob; JsCapability.Grep ], devOpsUltra)
            | "browser" -> Some(set [ JsCapability.Read; JsCapability.Grep ], browserUltra)
            | _ -> None

        candidate
        |> Option.bind (fun (requires, source) ->
            if Set.isSubset requires capabilities then
                Some { Requires = requires; Source = source }
            else
                None)

    let render (prose: Prose) (roleName: string) (toolName: string) (capabilities: Set<JsCapability>) : string =
        let ultraBlock =
            match ultraExample prose roleName capabilities with
            | Some example -> prose.UltraFraming + "\n\n```js\n" + example.Source + "\n```"
            | None -> prose.UltraUnavailable

        let blocks =
            [ if has capabilities JsCapability.Edit then
                  prose.EditPrelude
              if has capabilities JsCapability.Edit && has capabilities JsCapability.Read then
                  prose.EditStructuralGuidance
              prose.Header
              contract prose toolName capabilities
              "```js\n" + publicBaseClass prose capabilities + "\n```"
              rules prose capabilities
              prose.MechanicalSemantic
              ultraBlock
              prose.Footer ]

        String.concat "\n\n" blocks
