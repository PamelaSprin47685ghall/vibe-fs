namespace Wanxiangshu.Repository.Programming.Js

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

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
        let Footer = "tool/js-program/footer"

        [<Literal>]
        let Contract = "tool/js-program/contract"

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
        let ArgProgram = "tool/js-program/arg-program"

        [<Literal>]
        let MissingProgram = "tool/js-program/missing-program"

        [<Literal>]
        let HookNotVisible = "tool/js-program/hook-not-visible"

    type Prose =
        { Header: string
          Footer: string
          Contract: string
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
          ReasonRunUnimplemented: string }

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

    let private coderUltra =
        """class Js extends JsProgram {
  async run() {
    const refs = await this.grep(/\boldApi\b/, "{src,tests}/**/*.{js,ts}");
    const paths = [...new Set(refs.matches.map(x => x.path))];
    const core = await this.file("src/api.js", [
      ["definition", "afterDefinition", /const oldApi = buildApi\(\);/],
      ["export", "afterExport", /export \{ oldApi \};/],
      ["registration", "afterRegistration", /registry\.register\("oldApi", oldApi\);/],
    ]);
    const consumers = await Promise.all(
      paths.filter(path => path !== "src/api.js").map(async path => [path, await this.file(path)])
    );
    this.rewrite(
      "src/api.js",
      core.text("^", "definition") + `const newApi = buildApi();`
        + core.text("afterDefinition", "export") + `registry.register("newApi", newApi);`
        + core.text("afterExport", "registration") + `export { newApi };`
        + core.text("afterRegistration", "$")
    );
    for (const [path, file] of consumers) {
      const before = file.text();
      const after = before.replace(/\boldApi\b/g, "newApi");
      if (after !== before) this.rewrite(path, after);
    }
    return { migrated: `oldApi → newApi`, referencesObserved: refs.matches.length };
  }
}"""

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

    let private reviewerUltra =
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

    let ultraExample (roleName: string) (capabilities: Set<JsCapability>) : JsExample option =
        let candidate =
            match roleName.Trim().ToLowerInvariant() with
            | "coder" -> Some(set [ JsCapability.Read; JsCapability.Grep; JsCapability.Edit ], coderUltra)
            | "inspector" -> Some(set [ JsCapability.Read; JsCapability.Grep ], inspectorUltra)
            | "reviewer" -> Some(set [ JsCapability.Read; JsCapability.Grep ], reviewerUltra)
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
            match ultraExample roleName capabilities with
            | Some example -> prose.UltraFraming + "\n\n```js\n" + example.Source + "\n```"
            | None -> prose.UltraUnavailable

        String.concat
            "\n\n"
            [ prose.Header
              contract prose toolName capabilities
              "```js\n" + publicBaseClass prose capabilities + "\n```"
              rules prose capabilities
              prose.MechanicalSemantic
              ultraBlock
              prose.Footer ]
