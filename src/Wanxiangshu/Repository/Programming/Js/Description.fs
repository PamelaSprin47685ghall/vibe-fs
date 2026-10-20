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
        let VerbCreate = "tool/js-program/verb-create"

        [<Literal>]
        let ReadRules = "tool/js-program/rules-read"

        [<Literal>]
        let Recipes = "tool/js-program/recipes"

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
        let ReasonRunUnimplemented = "tool/js-program/reason-run-unimplemented"

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
        [<Literal>]
        let BaseClass = "tool/js-program/base-class"
        [<Literal>]
        let BaseClassCallable = "tool/js-program/base-class-callable"
        [<Literal>]
        let BaseClassRead = "tool/js-program/base-class-read"
        [<Literal>]
        let BaseClassGlob = "tool/js-program/base-class-glob"
        [<Literal>]
        let BaseClassGrep = "tool/js-program/base-class-grep"
        [<Literal>]
        let BaseClassWrite = "tool/js-program/base-class-write"
        [<Literal>]
        let BaseClassRemove = "tool/js-program/base-class-remove"
        [<Literal>]
        let BaseClassMove = "tool/js-program/base-class-move"
        [<Literal>]
        let BaseClassExec = "tool/js-program/base-class-exec"
        [<Literal>]
        let BaseClassFork = "tool/js-program/base-class-fork"
        [<Literal>]
        let BaseClassJoin = "tool/js-program/base-class-join"
        [<Literal>]
        let BaseClassFission = "tool/js-program/base-class-fission"
        [<Literal>]
        let BaseClassHorizon = "tool/js-program/base-class-horizon"
        [<Literal>]
        let BaseClassAdvisor = "tool/js-program/base-class-advisor"
        [<Literal>]
        let BaseClassAsk = "tool/js-program/base-class-ask"
        [<Literal>]
        let BaseClassLifecycle = "tool/js-program/base-class-lifecycle"

    type Prose =
        { Header: string

          Footer: string
          Contract: string

          ContractParallelEdits: string
          ContractParallelReads: string
          VerbRead: string

          VerbCreate: string
          ReadRules: string
          Recipes: string

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

          ReasonRunUnimplemented: string

          Park: string
          ParkedPayload: string
          BaseClass: string
          BaseClassCallable: string
          BaseClassRead: string
          BaseClassGlob: string
          BaseClassGrep: string
          BaseClassWrite: string
          BaseClassRemove: string
          BaseClassMove: string
          BaseClassExec: string
          BaseClassFork: string
          BaseClassJoin: string
          BaseClassFission: string
          BaseClassHorizon: string
          BaseClassAdvisor: string
          BaseClassAsk: string
          BaseClassLifecycle: string }

    let private fill (template: string) (pairs: (string * string) list) =
        let replaced =
            (template, pairs)
            ||> List.fold (fun acc (key, value) -> acc.Replace("{{" + key + "}}", value))

        if replaced.Contains("{{") then
            invalidOp "PROMPT-019:unsubstituted-placeholder"

        replaced

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

    /// `Callable` exists exactly when the Exec capability is projected; the
    /// base class hands it to the program as `this.Callable`.
    let private callableOpen (capabilities: Set<JsCapability>) =
        if has capabilities JsCapability.Exec then
            "class Callable {}\n\n"
        else
            ""

    let publicBaseClass (prose: Prose) (capabilities: Set<JsCapability>) : string =
        fill
            prose.BaseClass
            [
              "baseClassCallable", if has capabilities JsCapability.Exec then prose.BaseClassCallable else ""
              "baseClassRead", if has capabilities JsCapability.Read then prose.BaseClassRead else ""
              "baseClassGlob", if has capabilities JsCapability.Glob then prose.BaseClassGlob else ""
              "baseClassGrep", if has capabilities JsCapability.Grep then prose.BaseClassGrep else ""
              "baseClassWrite", if has capabilities JsCapability.Write then prose.BaseClassWrite else ""
              "baseClassRemove", if has capabilities JsCapability.Remove then prose.BaseClassRemove else ""
              "baseClassMove", if has capabilities JsCapability.Move then prose.BaseClassMove else ""
              "baseClassExec", if has capabilities JsCapability.Exec then prose.BaseClassExec else ""
              "baseClassFork", if has capabilities JsCapability.Fork then prose.BaseClassFork else ""
              "baseClassJoin", if has capabilities JsCapability.Join then prose.BaseClassJoin else ""
              "baseClassFission", if has capabilities JsCapability.Fission then prose.BaseClassFission else ""
              "baseClassHorizon", if has capabilities JsCapability.Horizon then prose.BaseClassHorizon else ""
              "baseClassAdvisor", if has capabilities JsCapability.Advisor then prose.BaseClassAdvisor else ""
              "baseClassAsk", if has capabilities JsCapability.Ask then prose.BaseClassAsk else ""
              "baseClassLifecycle", prose.BaseClassLifecycle
            ]

    let private rethrowHost =
        """if (result && result.ok === false) {
      const err = new Error(result.reason || result.code);
      err.__jsFailure = { code: result.code, reason: result.reason || result.code };
      throw err;
    }"""

    let runtimeBaseClass (prose: Prose) (capabilities: Set<JsCapability>) : string =
        let methods =
            [ """
    constructor(api) { this._api = api; }"""
                  .TrimStart()
              if has capabilities JsCapability.Exec then
                  "  Callable = Callable;"
              if has capabilities JsCapability.Read then
                  fill
                      """  async cat(path) {
    const result = await this._api.cat(path);
    {{rethrow_host}}
    return result.text;
  }"""
                      [ "rethrow_host", rethrowHost ]

                  fill
                      """  async stat(path) {
    const result = await this._api.stat(path);
    {{rethrow_host}}
    return result.stat;
  }"""
                      [ "rethrow_host", rethrowHost ]

                  fill
                      """  whereAll(content, needle, from = 0) {
    const result = this._api.whereAll(content, needle, from);
    {{rethrow_host}}
    return result.spans;
  }"""
                      [ "rethrow_host", rethrowHost ]

                  fill
                      """  async where(content, needle, from = 0) {
    const stack = new Error().stack;
    const result = await this._api.where(content, needle, from, stack);
    {{rethrow_host}}
    return result && result.span !== undefined ? result.span : result;
  }"""
                      [ "rethrow_host", rethrowHost ]
              if has capabilities JsCapability.Glob then
                  fill
                      """  async ls(path) {
    const result = await this._api.ls(path);
    {{rethrow_host}}
    return result.entries;
  }"""
                      [ "rethrow_host", rethrowHost ]
              if has capabilities JsCapability.Grep then
                  fill
                      """  async rg(argv) {
    const result = await this._api.rg(argv);
    {{rethrow_host}}
    return result.paths !== undefined
      ? { exitCode: result.exitCode, paths: result.paths }
      : { exitCode: result.exitCode, messages: result.messages };
  }"""
                      [ "rethrow_host", rethrowHost ]
              if has capabilities JsCapability.Write then
                  fill
                      """  write(path, content) {
    const result = this._api.write(path, content);
    {{rethrow_host}}
  }"""
                      [ "rethrow_host", rethrowHost ]

                  fill
                      """  mkdir(path) {
    const result = this._api.mkdir(path);
    {{rethrow_host}}
    return { ok: true, path };
  }"""
                      [ "rethrow_host", rethrowHost ]
              if has capabilities JsCapability.Remove then
                  fill
                      """  rm(path) {
    const result = this._api.rm(path);
    {{rethrow_host}}
    return { removed: result.removed };
  }"""
                      [ "rethrow_host", rethrowHost ]
              if has capabilities JsCapability.Move then
                  fill
                      """  mv(from, to) {
    const result = this._api.mv(from, to);
    {{rethrow_host}}
    return { moved: result.moved, destination: result.destination };
  }"""
                      [ "rethrow_host", rethrowHost ]
              if has capabilities JsCapability.Exec then
                  fill
                      """  async call(callableClass) {
    return await this._api.call(callableClass);
  }"""
                      []
              if has capabilities JsCapability.Ask then
                  fill
                      """  async ask(question) {
    const stack = new Error().stack;
    const result = await this._api.ask(question, stack);
    {{rethrow_host}}
    return result;
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
    return await this._api.fork(spec);
  }"""
                      []
              if has capabilities JsCapability.Join then
                  fill
                      """  async join() {
    return await this._api.join();
  }"""
                      []
              if has capabilities JsCapability.Horizon then
                  fill
                      """  async horizon() {
    return await this._api.horizon();
  }"""
                      []
              if has capabilities JsCapability.Advisor then
                  fill
                      """  async advisor(intent, view) {
    return await this._api.advisor(intent, view);
  }"""
                      []
              if has capabilities JsCapability.Fission then
                  fill
                      """  async fission(payload) {
    return await this._api.fission(payload);
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
        if (record) lines.push("--- " + dep + " ---\\n" + (record.view || ""));
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
              // park is a lifecycle capability every js-* surface carries; it
              // is not a filesystem permission projection, so it is always
              // present in the runtime base class.
              fill
                  """  async park(payload) {
    return await this._api.park(payload);
  }"""
                  []
              fill
                  """  async deliver(value, view) {
    const err = new Error("delivered");
    err.__jsDelivered = { value: value, view: view };
    throw err;
  }"""
                  []
              runStub prose ]

        String.concat "\n\n" ((callableOpen capabilities + classOpen) :: methods @ [ "}" ])

    let private planCapabilities = set [ JsCapability.Read; JsCapability.Write; JsCapability.Ask ]

    let runtimePlanBaseClass (prose: Prose) : string =
        "const PLAN_PATH = \"plan.md\";\n" + runtimeBaseClass prose planCapabilities

    let publicPlanBaseClass (prose: Prose) : string =
        "const PLAN_PATH = \"plan.md\";\n" + publicBaseClass prose planCapabilities

    let contract (prose: Prose) (toolName: string) (capabilities: Set<JsCapability>) =
        let verbs =
            [ if has capabilities JsCapability.Read then
                  yield! [ prose.VerbRead; "cat"; "stat"; "where"; "whereAll" ]
              if has capabilities JsCapability.Glob then
                  yield "ls"
              if has capabilities JsCapability.Grep then
                  yield "rg"
              if has capabilities JsCapability.Write then
                  yield! [ prose.VerbCreate; "mkdir" ]
              if has capabilities JsCapability.Exec then
                  yield "call" ]
            |> String.concat ", "

        let parallelTemplate =
            if has capabilities JsCapability.Write then prose.ContractParallelEdits
            else prose.ContractParallelReads

        fill prose.Contract
            [ "toolName", toolName
              "verbs", verbs
              "parallelLine", fill parallelTemplate [ "toolName", toolName ] ]

    let rules (prose: Prose) (capabilities: Set<JsCapability>) : string =
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
        |> String.concat "\n\n"

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
              rules prose capabilities
              prose.Park
              prose.Deliver
              prose.MechanicalSemantic
              ultraBlock
              prose.Footer ]

        String.concat "\n\n" blocks

    let render (prose: Prose) (roleName: string) (toolName: string) (capabilities: Set<JsCapability>) : string =
        renderFor prose roleName toolName capabilities (publicBaseClass prose capabilities)

    let renderPlan (prose: Prose) (scope: string) : string =

        let blocks =
            [ scope
              renderFor prose "plan" "todowrite-for-plan" planCapabilities (publicPlanBaseClass prose) ]

        String.concat "\n\n" blocks
