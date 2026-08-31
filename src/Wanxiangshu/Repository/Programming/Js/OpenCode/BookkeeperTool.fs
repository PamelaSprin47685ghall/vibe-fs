namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
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
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Process

/// Bookkeeper provider verb: one JavaScript program atomically reshapes the
/// staged question/answer Case. The sandbox has no filesystem capability.
module JsBookkeeperTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/js-bookkeeper/description"

        [<Literal>]
        let UltraFraming = "tool/js-bookkeeper/ultra-framing"

        [<Literal>]
        let ArgProgram = "tool/js-bookkeeper/arg-program"

        [<Literal>]
        let Unchanged = "tool/js-bookkeeper/unchanged"

        [<Literal>]
        let Accepted = "tool/js-bookkeeper/accepted"

        [<Literal>]
        let NoTransaction = "tool/js-bookkeeper/no-transaction"

        [<Literal>]
        let ProgramRequired = "tool/js-bookkeeper/program-required"

        [<Literal>]
        let SetQuestionOnce = "tool/js-bookkeeper/set-question-once"

        [<Literal>]
        let SetQuestionString = "tool/js-bookkeeper/set-question-string"

        [<Literal>]
        let SetAnswerOnce = "tool/js-bookkeeper/set-answer-once"

        [<Literal>]
        let SetAnswerString = "tool/js-bookkeeper/set-answer-string"

    /// DSL-state-combination: physical — one js-bookkeeper invocation's ephemeral staged mutation buffer; it is committed atomically only after the program succeeds.
    type private StagedMutation =
        { mutable Question: string option
          mutable Answer: string option
          mutable QuestionWasSet: bool
          mutable AnswerWasSet: bool }

    [<Emit("typeof($0)==='string'")>]
    let private isString (value: obj) : bool = jsNative

    let private lang (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private prose language path =
        ProviderProse.render language path Map.empty

    let private failureObject (reason: string) : obj =
        createObj [ "ok" ==> false; "code" ==> "PROGRAM_FAILED"; "reason" ==> reason ]

    let private successObject () : obj = createObj [ "ok" ==> true ]

    let private createApi
        (language: ProviderLanguage)
        (question: string)
        (answer: string)
        (staged: StagedMutation)
        : obj =
        let setQuestion (value: obj) =
            if staged.QuestionWasSet then
                failureObject (prose language Path.SetQuestionOnce)
            elif not (isString value) then
                failureObject (prose language Path.SetQuestionString)
            else
                staged.QuestionWasSet <- true
                staged.Question <- Some(string value)
                successObject ()

        let setAnswer (value: obj) =
            if staged.AnswerWasSet then
                failureObject (prose language Path.SetAnswerOnce)
            elif not (isString value) then
                failureObject (prose language Path.SetAnswerString)
            else
                staged.AnswerWasSet <- true
                staged.Answer <- Some(string value)
                successObject ()

        createObj
            [ "bookkeeper"
              ==> createObj
                      [ "question" ==> fun () -> question
                        "answer" ==> fun () -> answer
                        "setQuestion" ==> setQuestion
                        "setAnswer" ==> setAnswer ] ]

    let private runtimeBaseClass =
        """class JsProgram {
  constructor(api) {
    this._api = api;
  }

  _view(source, matches, label) {
    const fail = reason => {
      const err = new Error(reason);
      err.__jsFailure = { code: "PROGRAM_FAILED", reason };
      throw err;
    };

    const anchors = new Map([["^", 0], ["$", source.length]]);
    let cursor = 0;

    const locate = pattern => {
      if (typeof pattern === "string") {
        if (pattern.length === 0) fail(`${label}: anchor strings must be non-empty`);
        const start = source.indexOf(pattern, cursor);
        return start < 0 ? null : { start, end: start + pattern.length };
      }

      if (pattern instanceof RegExp) {
        const flags = [...new Set(pattern.flags.replace(/[gy]/g, "") + "g")].join("");
        const regexp = new RegExp(pattern.source, flags);
        regexp.lastIndex = cursor;
        const match = regexp.exec(source);
        return match ? { start: match.index, end: match.index + match[0].length } : null;
      }

      fail(`${label}: anchor pattern must be a string or RegExp`);
    };

    for (const declaration of matches) {
      if (!Array.isArray(declaration) || declaration.length !== 3)
        fail(`${label}: each anchor declaration must be [begin, end, pattern]`);

      const [begin, end, pattern] = declaration;
      if (!begin || !end || begin === end || begin === "^" || begin === "$" || end === "^" || end === "$")
        fail(`${label}: invalid anchor names`);
      if (anchors.has(begin) || anchors.has(end))
        fail(`${label}: anchor names must be unique`);

      const match = locate(pattern);
      if (!match) fail(`${label}: anchor ${begin}/${end} was not found`);
      anchors.set(begin, match.start);
      anchors.set(end, match.end);
      cursor = match.end;
    }

    const clip = value => Math.min(Math.max(0, value), source.length);
    const offset = name => {
      if (anchors.has(name)) return anchors.get(name);
      const shifted = /^(.*)([+-]\d+)$/.exec(name);
      if (!shifted || shifted[1].length === 0) fail(`${label}: unknown anchor ${name}`);
      return clip(offset(shifted[1]) + Number(shifted[2]));
    };

    return Object.freeze({
      text(from = "^", to = "$") {
        const start = offset(from);
        const end = offset(to);
        if (start > end) fail(`${label}: ${from} is after ${to}`);
        return source.slice(start, end);
      },
    });
  }

  question(matches = []) {
    return this._view(this._api.bookkeeper.question(), matches, "question");
  }

  answer(matches = []) {
    return this._view(this._api.bookkeeper.answer(), matches, "answer");
  }

  setQuestion(newText) {
    const result = this._api.bookkeeper.setQuestion(newText);
    if (result && result.ok === false) {
      const err = new Error(result.reason);
      err.__jsFailure = { code: result.code, reason: result.reason };
      throw err;
    }
  }

  setAnswer(newText) {
    const result = this._api.bookkeeper.setAnswer(newText);
    if (result && result.ok === false) {
      const err = new Error(result.reason);
      err.__jsFailure = { code: result.code, reason: result.reason };
      throw err;
    }
  }

  async run() {
    throw new Error("JS_RUN_NOT_IMPLEMENTED");
  }
}"""

    let private publicBaseClass =
        """class JsProgram {
  question(matches = []) {
  }

  answer(matches = []) {
  }

  setQuestion(newText) {
  }

  setAnswer(newText) {
  }

  async run() {
    throw new Error("JS_RUN_NOT_IMPLEMENTED");
  }
}"""

    let private ultraExample =
        """class Js extends JsProgram {
  async run() {
    const question = this.question([
      ['constraints', 'afterConstraints', '## Constraints'],
    ]);
    const answer = this.answer([
      ['answer', 'afterAnswer', '## Answer'],
      ['evidence', 'afterEvidence', '## Evidence'],
    ]);

    const asksForCompatibility = /backward compatibility/i.test(question.text());
    const claimsCompatibility = /backward compatible|no breaking change/i.test(answer.text());
    const hasEvidence = /compatibility test|legacy client/i.test(answer.text('evidence', '$'));

    if (!claimsCompatibility || !asksForCompatibility || hasEvidence)
      return { changed: false };

    this.setQuestion(
      question.text('^', 'constraints')
        + '## Constraints\n'
        + question.text('afterConstraints', '$')
        + '\n\nClarify whether backward compatibility is required.\n'
    );
    this.setAnswer(
      '## Answer\nThe implementation result is established, but backward compatibility is not established by the supplied evidence.\n'
        + answer.text('afterAnswer', '$')
    );

    return { changed: true };
  }
}"""

    let private assembleDescription (language: ProviderLanguage) =
        String.concat
            "\n\n"
            [ prose language Path.Description
              "```js\n" + publicBaseClass + "\n```"
              prose language Path.UltraFraming
              "```js\n" + ultraExample + "\n```" ]

    let private failed language (reason: string) =
        ToolHostCodec.tomlObjectWithInstructions [ prose language Path.Unchanged; reason ] []

    let private succeeded language (value: LlmFacing.Data.Value) =
        LlmFacing.instruction (prose language Path.Accepted)
        |> LlmFacing.withData (LlmFacing.Data.structuredValue value)
        |> LlmFacing.render

    let execute (args: HostToolArguments) (context: HostToolContext) =
        task {
            let language = lang context

            let! outcome =
                taskResult {
                    let! txId =
                        BookkeeperRuntime.tryTxId context.SessionId
                        |> Result.requireSome (prose language Path.NoTransaction)

                    let! program =
                        args.OptionalText "program"
                        |> Result.requireSome (prose language Path.ProgramRequired)

                    let! question, answer = BookkeeperStaging.snapshot txId

                    let staged =
                        { Question = None
                          Answer = None
                          QuestionWasSet = false
                          AnswerWasSet = false }

                    let api = createApi language question answer staged

                    let! json =
                        JsSandbox.runSurface
                            runtimeBaseClass
                            program
                            api
                            10000
                            (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10000L)
                            (1 <<< 20)
                        |> TaskResult.mapError JsFailure.reason

                    let! value = JsToolsData.parse json |> Result.mapError JsFailure.reason
                    do! BookkeeperStaging.apply txId staged.Question staged.Answer
                    return value
                }

            match outcome with
            | Error reason -> return failed language reason
            | Ok value -> return succeeded language value
        }

    /// KNOWLEDGE-REUSE-006 / ENF-006: the Bookkeeper is an internal leaf whose
    /// prompt is HostInternal, so its session never holds a public office Role.
    /// Its authority is the owner-held transaction attachment itself.
    let admission: ToolAdmission =
        ToolAdmission.PrivateAttachment(fun ctx ->
            not (String.IsNullOrWhiteSpace ctx.SessionId)
            && BookkeeperRuntime.isAttached ctx.SessionId)

    let spec (factory: HostToolFactory) : ToolSpec =
        let language = ProviderLanguageBinding.readGlobalPreference ()

        { Name = "js-bookkeeper"
          Description = assembleDescription language
          Arguments = [ "program", ToolHostCodec.stringSchemaDescribed (prose language Path.ArgProgram) factory ]
          Admission = admission
          Execute = execute }
