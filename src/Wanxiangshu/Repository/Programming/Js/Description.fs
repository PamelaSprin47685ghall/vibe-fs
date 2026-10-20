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
        let Recipes = "tool/js-program/recipes"


        [<Literal>]
        let EditRules = "tool/js-program/rules-edit"

        [<Literal>]
        let WriteRules = "tool/js-program/rules-write"

        [<Literal>]
        let MutationRules = "tool/js-program/rules-mutation"

        [<Literal>]
        let Rm = "tool/js-program/rm"

        [<Literal>]
        let Mv = "tool/js-program/mv"

        [<Literal>]
        let RunRules = "tool/js-program/run-rules"

        [<Literal>]
        let ViewDeclarationExtra = "tool/js-program/view-declaration-extra"

        [<Literal>]
        let Deliver = "tool/js-program/deliver"

        [<Literal>]
        let DelegationRules = "tool/js-program/delegation-rules"

        [<Literal>]
        let UltraFraming = "tool/js-program/ultra-framing"

        [<Literal>]
        let UltraUnavailable = "tool/js-program/ultra-unavailable"

        [<Literal>]
        let UltraEngineering = "tool/js-program/ultra-engineering"

        [<Literal>]
        let UltraInvestigation = "tool/js-program/ultra-investigation"

        [<Literal>]
        let UltraDevOps = "tool/js-program/ultra-devops"

        [<Literal>]
        let UltraManagerInvestigation = "tool/js-program/ultra-manager-investigation"

        [<Literal>]
        let UltraPlan = "tool/js-program/ultra-plan"

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

        [<Literal>]
        let Park = "tool/js-program/park"

        [<Literal>]
        let ParkedPayload = "tool/js-program/parked-payload"

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
          Recipes: string
          EditRules: string
          WriteRules: string
          MutationRules: string
          Rm: string
          Mv: string
          RunRules: string
          Deliver: string
          DelegationRules: string
          UltraFraming: string
          UltraUnavailable: string
          UltraEngineering: string
          UltraInvestigation: string
          UltraDevOps: string
          UltraManagerInvestigation: string
          UltraPlan: string
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
          ReasonEditOverlaps: string
          Park: string
          ParkedPayload: string }

    let private fill (template: string) (pairs: (string * string) list) =
        let replaced =
            (template, pairs)
            ||> List.fold (fun acc (key, value) -> acc.Replace("{{" + key + "}}", value))

        if replaced.Contains("{{") then
            invalidOp "PROMPT-019:unsubstituted-placeholder"

        replaced

    let private writeStub (prose: Prose) =
        fill
            """  write(path, content) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private catStub (prose: Prose) =
        fill
            """  async cat(path) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private lsStub (prose: Prose) =
        fill
            """  async ls(path) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private statStub (prose: Prose) =
        fill
            """  async stat(path) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private mkdirStub (prose: Prose) =
        fill
            """  mkdir(path) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private whereAllStub (prose: Prose) =
        fill
            """  whereAll(content, needle, from = 0) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private whereStub (prose: Prose) =
        fill
            """  async where(content, needle, from = 0) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private rmStub (prose: Prose) =
        fill
            """  rm(path) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private mvStub (prose: Prose) =
        fill
            """  mv(from, to) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private parkStub (prose: Prose) =
        fill
            """  async park(payload) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private deliverStub (prose: Prose) =
        fill
            """  async deliver(value) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private forkStub (prose: Prose) =
        fill
            """  async fork(payload) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private joinStub (prose: Prose) =
        fill
            """  async join() {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private horizonStub (prose: Prose) =
        fill
            """  async horizon() {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private advisorStub (prose: Prose) =
        fill
            """  async advisor(intent, view) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private fissionStub (prose: Prose) =
        fill
            """  async fission(payload) {
    // {{comment_host_capability}}
  }"""
            [ "comment_host_capability", prose.CommentHostCapability ]

    let private dagStub (prose: Prose) =
        fill
            """  async dag(spec) {
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

    /// `js.Callable` exists exactly when the Exec capability is projected; the
    /// wrapper binds the class onto the global `js` namespace before the model
    /// source runs.
    let private runnableOpen (capabilities: Set<JsCapability>) =
        if has capabilities JsCapability.Exec then
            "class JsCallable {}\n\n"
        else
            ""

    let private publicBaseClassCore (planOnly: bool) (prose: Prose) (capabilities: Set<JsCapability>) : string =
        let methods =
            [ if has capabilities JsCapability.Read then
                  catStub prose
                  statStub prose
                  whereAllStub prose
                  whereStub prose
              if has capabilities JsCapability.Glob then
                  lsStub prose
              if has capabilities JsCapability.Grep then
                  """  async rg(argv) {
    // Search the workspace with an explicit pattern and path.
  }"""
              if has capabilities JsCapability.Write then
                  writeStub prose
              if has capabilities JsCapability.Write && not planOnly then
                  mkdirStub prose
              if has capabilities JsCapability.Remove then
                  rmStub prose
              if has capabilities JsCapability.Move then
                  mvStub prose

              if has capabilities JsCapability.Fork then
                  forkStub prose
                  dagStub prose
              if has capabilities JsCapability.Join then
                  joinStub prose
              if has capabilities JsCapability.Horizon then
                  horizonStub prose
              if has capabilities JsCapability.Advisor then
                  advisorStub prose
              if has capabilities JsCapability.Fission then
                  fissionStub prose
              if planOnly then
                  "  async ask(question) {\n    // Ask through the host user channel.\n  }"
              // park is a lifecycle capability every js-* surface carries; it
              // is not a filesystem permission projection, so it is always
              // present in the base class (PLAN S1 B.17).
              parkStub prose
              deliverStub prose
              runStub prose ]

        String.concat "\n\n" ((runnableOpen capabilities + classOpen) :: methods @ [ "}" ])

    let publicBaseClass prose capabilities =
        publicBaseClassCore false prose capabilities

    let private rethrowHost =
        """if (result && result.ok === false) {
      const err = new Error(result.reason || result.code);
      err.__jsFailure = { code: result.code, reason: result.reason || result.code };
      throw err;
    }"""

    let publicPlanBaseClass (prose: Prose) : string =
        "const PLAN_PATH = \"plan.md\";\n\n"
        + publicBaseClassCore true prose (set [ JsCapability.Read; JsCapability.Write ])

    let private runtimeBaseClassCore (planOnly: bool) (prose: Prose) (capabilities: Set<JsCapability>) : string =
        let methods =
            [ """
  constructor(api) { this._api = api; }"""
                  .TrimStart()
              if has capabilities JsCapability.Grep then
                  fill
                      """  async rg(argv) {
    const result = await this._api.js.rg(argv);
    {{rethrow_host}}
    return result;
  }"""
                      [ "rethrow_host", rethrowHost ]
              if has capabilities JsCapability.Read then
                  fill
                      """  async cat(path) {
    const result = await this._api.js.cat(path);
    {{rethrow_host}}
    return result.text;
  }"""
                      [ "rethrow_host", rethrowHost ]

                  fill
                      """  async stat(path) {
    const result = await this._api.js.stat(path);
    {{rethrow_host}}
    return result.stat;
  }"""
                      [ "rethrow_host", rethrowHost ]

                  fill
                      """  whereAll(content, needle, from = 0) {
    const result = this._api.js.whereAll(content, needle, from);
    {{rethrow_host}}
    return result.spans;
  }"""
                      [ "rethrow_host", rethrowHost ]

                  fill
                      """  async where(content, needle, from = 0) {
    const stack = new Error().stack;
    const result = await this._api.js.where(content, needle, from, stack);
    {{rethrow_host}}
    return result && result.span !== undefined ? result.span : result;
  }"""
                      [ "rethrow_host", rethrowHost ]
              if has capabilities JsCapability.Glob then
                  fill
                      """  async ls(path) {
    const result = await this._api.js.ls(path);
    {{rethrow_host}}
    return result.entries;
  }"""
                      [ "rethrow_host", rethrowHost ]
              if has capabilities JsCapability.Write then
                  fill
                      """  write(path, content) {
    const result = this._api.js.write(path, content);
    {{rethrow_host}}
  }"""
                      [ "rethrow_host", rethrowHost ]

              if has capabilities JsCapability.Write && not planOnly then
                  fill
                      """  mkdir(path) {
    const result = this._api.js.mkdir(path);
    {{rethrow_host}}
    return { ok: true, path };
  }"""
                      [ "rethrow_host", rethrowHost ]
              if has capabilities JsCapability.Remove then
                  fill
                      """  rm(path) {
    const result = this._api.js.rm(path);
    {{rethrow_host}}
    return { removed: result.removed };
  }"""
                      [ "rethrow_host", rethrowHost ]
              if has capabilities JsCapability.Move then
                  fill
                      """  mv(from, to) {
    const result = this._api.js.mv(from, to);
    {{rethrow_host}}
    return { moved: result.moved, destination: result.destination };
  }"""
                      [ "rethrow_host", rethrowHost ]
              if has capabilities JsCapability.Fork then
                  fill
                      """  async fork(payload) {
    const spec = typeof payload === "string" ? { charge: payload } : Object.assign({}, payload);
    if (!spec.name) {
      const slug = String(spec.charge || "").toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "").slice(0, 24);
      spec.name = slug || "child";
    }
    return await this._api.js.fork(spec);
  }"""
                      []
              if has capabilities JsCapability.Join then
                  fill
                      """  async join() {
    return await this._api.js.join();
  }"""
                      []
              if has capabilities JsCapability.Horizon then
                  fill
                      """  async horizon() {
    return await this._api.js.horizon();
  }"""
                      []
              if has capabilities JsCapability.Advisor then
                  fill
                      """  async advisor(intent, view) {
    return await this._api.js.advisor(intent, view);
  }"""
                      []
              if has capabilities JsCapability.Fission then
                  fill
                      """  async fission(payload) {
    return await this._api.js.fission(payload);
  }"""
                      []
              if has capabilities JsCapability.Fork then
                  fill
                      """  async dag(spec) {
    const nodes = (spec && spec.nodes) || [];
    if (nodes.length === 0) return { ok: true, results: {}, skipped: [] };
    const byId = {};
    for (const node of nodes) byId[node.id] = node;
    const results = {};
    const skipped = [];
    let pending = nodes.map((node) => node.id);
    const agentToNode = {};
    const prompt = (node) => {
      const lines = [node.charge];
      for (const dep of node.deps || []) {
        const record = results[dep];
        if (record) lines.push("--- " + dep + " ---\\n" + (record.workRecord || ""));
      }
      return lines.join("\\n");
    };
    const decide = async (node, code, extra) => {
      const park = Object.assign({ dag: spec, node: node, code: code, deps: node.deps || [], done: Object.keys(results), pending: pending.slice() }, extra || {});
      return await this.park(park);
    };
    const abortWith = (code, reason) => {
      const err = new Error(reason);
      err.__jsFailure = { code: code, reason: reason };
      throw err;
    };
    while (pending.length > 0) {
      const ready = nodes.filter((node) => pending.indexOf(node.id) >= 0 && (node.deps || []).every((dep) => dep in results || skipped.indexOf(dep) >= 0));
      if (ready.length === 0) {
        abortWith("DAG_CYCLE", "dag has a cycle or an unknown dependency");
      }
      const receipts = await Promise.all(ready.map((node) => this.fork({ charge: prompt(node), name: node.id })));
      let waveRetry = false;
      for (let index = 0; index < ready.length; index += 1) {
        const node = ready[index];
        const receipt = receipts[index];
        if (receipt && receipt.ok === true) {
          agentToNode[receipt.agentId] = node.id;
        } else {
          const decision = await decide(node, (receipt && receipt.code) || "fork-failed");
          if (decision === "retry") {
            waveRetry = true;
            break;
          } else if (decision === "skip") {
            results[node.id] = null;
            skipped.push(node.id);
            pending = pending.filter((id) => id !== node.id);
          } else if (decision && typeof decision === "object" && decision.abort) {
            abortWith("DAG_ABORTED", String(decision.abort));
          } else {
            results[node.id] = decision;
            pending = pending.filter((id) => id !== node.id);
          }
        }
      }
      if (waveRetry) continue;
      const joined = await this.join();
      if (!joined || joined.ok !== true) {
        const decision = await decide(ready[0], (joined && joined.code) || "join-failed");
        if (decision === "retry") continue;
        if (decision === "skip") {
          for (const node of ready) {
            if (pending.indexOf(node.id) >= 0) {
              results[node.id] = null;
              skipped.push(node.id);
              pending = pending.filter((id) => id !== node.id);
            }
          }
          continue;
        }
        abortWith("DAG_JOIN_REFUSED", String((joined && joined.code) || "unknown"));
      }
      let recordRetry = false;
      for (const record of joined.records || []) {
        const id = agentToNode[record.agentId];
        if (id === undefined || pending.indexOf(id) < 0) continue;
        if (record.status === "completed") {
          results[id] = record;
          pending = pending.filter((entry) => entry !== id);
        } else {
          const decision = await decide(byId[id], "node-" + record.status, { record: record });
          if (decision === "retry") {
            recordRetry = true;
            break;
          } else if (decision === "skip") {
            results[id] = null;
            skipped.push(id);
            pending = pending.filter((entry) => entry !== id);
          } else if (decision && typeof decision === "object" && decision.abort) {
            abortWith("DAG_ABORTED", String(decision.abort));
          } else {
            results[id] = decision;
            pending = pending.filter((entry) => entry !== id);
          }
        }
      }
      if (recordRetry) continue;
      for (const id of ready.map((node) => node.id)) {
        if (pending.indexOf(id) >= 0 && !(id in results) && skipped.indexOf(id) < 0) {
          abortWith("DAG_RECORD_MISSING", "the child of node " + id + " returned no record");
        }
      }
    }
    return { ok: true, results: results, skipped: skipped };
  }"""
                      []
              if planOnly then
                  fill
                      """  async ask(question) {
    const result = await this._api.js.ask(question, new Error().stack);
    {{rethrow_host}}
    return result;
  }"""
                      [ "rethrow_host", rethrowHost ]
              // park is a lifecycle capability every js-* surface carries; it
              // is not a filesystem permission projection, so it is always
              // present in the runtime base class.
              fill
                  """  async park(payload) {
    return await this._api.js.park(payload);
  }"""
                  []
              fill
                  """  async deliver(value) {
    const err = new Error("delivered");
    err.__jsDelivered = { value: value };
    throw err;
  }"""
                  []
              runStub prose ]

        String.concat "\n\n" ((runnableOpen capabilities + classOpen) :: methods @ [ "}" ])

    let runtimeBaseClass prose capabilities =
        runtimeBaseClassCore false prose capabilities

    let runtimePlanBaseClass prose =
        "const PLAN_PATH = \"plan.md\";\n\n"
        + runtimeBaseClassCore true prose (set [ JsCapability.Read; JsCapability.Write ])

    let contract (prose: Prose) (toolName: string) (capabilities: Set<JsCapability>) =
        let verbs =
            List.concat
                [ if has capabilities JsCapability.Read then
                      [ prose.VerbRead; "cat"; "stat"; "where"; "whereAll" ]
                  else
                      []
                  if has capabilities JsCapability.Glob then [ "ls" ] else []
                  if has capabilities JsCapability.Grep then [ "rg" ] else []
                  if has capabilities JsCapability.Write then
                      [ prose.VerbCreate; "mkdir" ]
                  else
                      []
                  if has capabilities JsCapability.Exec then
                      [ "call" ]
                  else
                      [] ]
            |> String.concat ", "

        let parallelTemplate =
            if has capabilities JsCapability.Write then
                prose.ContractParallelEdits
            else
                prose.ContractParallelReads

        fill
            prose.Contract
            [ "toolName", toolName
              "verbs", verbs
              "editRecommendation", ""
              "parallelLine", fill parallelTemplate [ "toolName", toolName ] ]

    let rules (prose: Prose) (capabilities: Set<JsCapability>) : string =
        let blocks =
            [ if has capabilities JsCapability.Read then
                  prose.ReadRules
              if has capabilities JsCapability.Glob then
                  prose.Recipes
              if has capabilities JsCapability.Write then
                  prose.WriteRules
              if has capabilities JsCapability.Remove then
                  prose.Rm
              if has capabilities JsCapability.Move then
                  prose.Mv
              if has capabilities JsCapability.Exec then
                  prose.RunRules
              if has capabilities JsCapability.Fork || has capabilities JsCapability.Join then
                  prose.DelegationRules
              if has capabilities JsCapability.Write then
                  prose.MutationRules ]

        String.concat "\n\n" blocks

    let ultraExample (prose: Prose) (roleName: string) (capabilities: Set<JsCapability>) : JsExample option =
        let candidate =
            match roleName.Trim().ToLowerInvariant() with
            | "engineer" when has capabilities JsCapability.Write ->
                Some(set [ JsCapability.Read; JsCapability.Write ], prose.UltraEngineering)
            | "engineer" -> Some(set [ JsCapability.Read ], prose.UltraInvestigation)
            | "devops" -> Some(set [ JsCapability.Read; JsCapability.Exec ], prose.UltraDevOps)
            | "plan" -> Some(set [ JsCapability.Read ], prose.UltraPlan)
            | "manager" when
                Set.isSubset
                    capabilities
                    (set
                        [ JsCapability.Read
                          JsCapability.Glob
                          JsCapability.Grep
                          JsCapability.Fork
                          JsCapability.Join
                          JsCapability.Horizon
                          JsCapability.Advisor ])
                && not (Set.isEmpty capabilities)
                ->
                Some(set [ JsCapability.Read; JsCapability.Glob ], prose.UltraManagerInvestigation)
            | _ -> None

        candidate
        |> Option.bind (fun (requires, source) ->
            if Set.isSubset requires capabilities then
                Some { Requires = requires; Source = source }
            else
                None)

    let private renderFor
        (prose: Prose)
        (roleName: string)
        (toolName: string)
        (capabilities: Set<JsCapability>)
        (baseClass: string)
        : string =
        let ultraBlock =
            match ultraExample prose roleName capabilities with
            | Some example -> prose.UltraFraming + "\n\n```js\n" + example.Source + "\n```"
            | None -> prose.UltraUnavailable

        let blocks =
            [ prose.Header
              contract prose toolName capabilities
              "```js\n" + baseClass + "\n```"
              (if roleName.Trim().ToLowerInvariant() = "plan" then
                   rules prose (Set.remove JsCapability.Write capabilities)
               else
                   rules prose capabilities)
              prose.Park
              prose.Deliver
              prose.MechanicalSemantic
              ultraBlock
              prose.Footer ]

        String.concat "\n\n" blocks

    let render (prose: Prose) (roleName: string) (toolName: string) (capabilities: Set<JsCapability>) : string =
        renderFor prose roleName toolName capabilities (publicBaseClass prose capabilities)

    let renderPlan (prose: Prose) (scope: string) : string =
        let planCapabilities = set [ JsCapability.Read; JsCapability.Write ]

        let blocks =
            [ scope
              renderFor prose "plan" "todowrite-for-plan" planCapabilities (publicPlanBaseClass prose) ]

        String.concat "\n\n" blocks
