namespace Wanxiangshu.Infrastructure

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.OpenCode
open Wanxiangshu.Process

/// Bookkeeper provider verb: one JavaScript program atomically reshapes the
/// staged question/answer Case. The sandbox has no filesystem capability.
module JsBookkeeperTool =

    /// DSL-state-combination: physical — one js-bookkeeper invocation's ephemeral staged mutation buffer; it is committed atomically only after the program succeeds.
    type private StagedMutation =
        { mutable Question: string option
          mutable Answer: string option
          mutable QuestionWasSet: bool
          mutable AnswerWasSet: bool }

    [<Emit("typeof $0 === 'string'")>]
    let private isString (value: obj) : bool = jsNative

    let private failureObject (reason: string) : obj =
        createObj [ "ok" ==> false; "code" ==> "PROGRAM_FAILED"; "reason" ==> reason ]

    let private successObject () : obj = createObj [ "ok" ==> true ]

    let private createApi (question: string) (answer: string) (staged: StagedMutation) : obj =
        let setQuestion (value: obj) =
            if staged.QuestionWasSet then
                failureObject "setQuestion may be called at most once in one js-bookkeeper program"
            elif not (isString value) then
                failureObject "setQuestion(newText) requires a string"
            else
                staged.QuestionWasSet <- true
                staged.Question <- Some(string value)
                successObject ()

        let setAnswer (value: obj) =
            if staged.AnswerWasSet then
                failureObject "setAnswer may be called at most once in one js-bookkeeper program"
            elif not (isString value) then
                failureObject "setAnswer(newText) requires a string"
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
    throw new Error("Js.run() must be implemented.");
  }
}"""

    let private publicBaseClass =
        """class JsProgram {
  question(matches = []) {
    // Returns an immutable view of the frozen question.
  }

  answer(matches = []) {
    // Returns an immutable view of the frozen answer.
  }

  setQuestion(newText) {
    // Stage the complete next question. May be called at most once.
  }

  setAnswer(newText) {
    // Stage the complete next answer. May be called at most once.
  }

  async run() {
    throw new Error("Js.run() must be implemented.");
  }
}"""

    let private ultraExample =
        """class Js extends JsProgram {
  async run() {
    const question = this.question([
      ["constraints", "afterConstraints", "## Constraints"],
    ]);
    const answer = this.answer([
      ["answer", "afterAnswer", "## Answer"],
      ["evidence", "afterEvidence", "## Evidence"],
    ]);

    const asksForCompatibility = /backward compatibility/i.test(question.text());
    const claimsCompatibility = /backward compatible|no breaking change/i.test(answer.text());
    const hasEvidence = /compatibility test|legacy client/i.test(answer.text("evidence", "$"));

    if (!claimsCompatibility || !asksForCompatibility || hasEvidence)
      return { changed: false };

    this.setQuestion(
      question.text("^", "constraints")
        + "## Constraints\n"
        + question.text("afterConstraints", "$")
        + "\n\nClarify whether backward compatibility is required.\n"
    );
    this.setAnswer(
      "## Answer\nThe implementation result is established, but backward compatibility is not established by the supplied evidence.\n"
        + answer.text("afterAnswer", "$")
    );

    return { changed: true };
  }
}"""

    let private description =
        String.concat
            "\n\n"
            [ "Program the next form of the staged Case with one atomic JavaScript transformation."
              "The Case is already frozen for this transaction. question(matches = []) and answer(matches = []) return immutable text views with ordered anchors. view.text(from = \"^\", to = \"$\") slices exact text; anchor names may use clipped +N/-N shifts."
              "setQuestion(newText) and setAnswer(newText) each stage the complete next side and may each be called at most once. Zero mutation is legal. A thrown program or invalid mutation changes neither side."
              "The program has no outside-world capability. Decide what the Case should mean before the call; use this program only to carry out the coherent mechanical reshaping already justified by that decision."
              "```js\n" + publicBaseClass + "\n```"
              "Ultra Example — Rewrite the Case, Not Just a String\n\nMechanical branches belong inside the program. Semantic judgment belongs before or between programs.\n\n```js\n"
              + ultraExample
              + "\n```" ]

    let private failed (reason: string) =
        ToolHostCodec.tomlObjectWithInstructions [ "The staged case was not changed."; reason ] []

    let private succeeded (value: SyntheticToml.DataValue) =
        SyntheticToml.document [ "The staged case accepted this transformation." ] (SyntheticToml.encodeData value)

    let execute (args: HostToolArguments) (context: HostToolContext) =
        task {
            match BookkeeperRuntime.tryTxId context.SessionId with
            | None -> return failed "There is no Bookkeeper transaction for this session."
            | Some txId ->
                match args.OptionalText "program" with
                | None -> return failed "A js-bookkeeper program is required."
                | Some program ->
                    match BookkeeperStaging.snapshot txId with
                    | Error reason -> return failed reason
                    | Ok(question, answer) ->
                        let staged =
                            { Question = None
                              Answer = None
                              QuestionWasSet = false
                              AnswerWasSet = false }

                        let api = createApi question answer staged

                        let! sandboxResult =
                            JsSandbox.runSurface
                                runtimeBaseClass
                                program
                                api
                                10000
                                (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10000L)
                                (1 <<< 20)

                        match sandboxResult with
                        | Error failure -> return failed (JsFailure.reason failure)
                        | Ok json ->
                            match JsToolsData.parse json with
                            | Error failure -> return failed (JsFailure.reason failure)
                            | Ok value ->
                                match BookkeeperStaging.apply txId staged.Question staged.Answer with
                                | Error reason -> return failed reason
                                | Ok() -> return succeeded value
        }

    let spec (factory: HostToolFactory) : ToolSpec =
        { Name = "js-bookkeeper"
          Description = description
          Arguments =
            [ "program",
              ToolHostCodec.stringSchemaDescribed
                  "Exactly one class named Js that extends JsProgram and implements async run()."
                  factory ]
          Execute = execute }
