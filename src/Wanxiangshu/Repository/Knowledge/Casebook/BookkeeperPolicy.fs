namespace Wanxiangshu.Repository.Knowledge.Casebook

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider

/// HNI-09: the product decisions behind the `todowrite-for-bookkeeper` tool.
///
/// The tool is one JavaScript program that atomically reshapes the staged
/// question/answer Case. Everything the provider is allowed to see and do
/// lives here: the prose vocabulary, the DSL the program extends, the
/// write-once rule of the two setters, and how a settled call is rendered.
/// The OpenCode binding (`JsBookkeeperTool`) keeps only the sandbox call and
/// the host tool shape, and asks this module for the decisions.
[<RequireQualifiedAccess>]
module BookkeeperPolicy =

    /// The semantic prose paths of the tool.
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

    /// DSL-state-combination: physical — one invocation's ephemeral staged
    /// mutation buffer; it is committed atomically only after the program
    /// succeeds.
    type StagedMutation =
        { mutable Question: string option
          mutable Answer: string option
          mutable QuestionWasSet: bool
          mutable AnswerWasSet: bool }

    /// The empty staged buffer of one invocation.
    let newStaged () : StagedMutation =
        { Question = None
          Answer = None
          QuestionWasSet = false
          AnswerWasSet = false }

    /// The language a call's prose is rendered in. The live global preference
    /// is the only authority (HOST-026), so a call reads it when it runs.
    let callLanguage () : ProviderLanguage = GlobalProviderLanguage.current ()

    /// The language the tool description is rendered in. The description is
    /// built once, when the tool is registered, so it reads the observed
    /// preference directly.
    let descriptionLanguage () : ProviderLanguage =
        ProviderLanguageBinding.readGlobalPreference ()

    /// The prose of one semantic path.
    let prose (lang: ProviderLanguage) (path: string) : string =
        ProviderProse.render lang path Map.empty

    [<Emit("typeof($0)==='string'")>]
    let private isString (value: obj) : bool = jsNative

    let private failureObject (reason: string) : obj =
        createObj [ "ok" ==> false; "code" ==> "PROGRAM_FAILED"; "reason" ==> reason ]

    let private successObject () : obj = createObj [ "ok" ==> true ]

    /// The API the program sees. The two setters are write-once: a program
    /// that sets the question twice is refused by name, not silently
    /// overwritten, because the staged Case is one atomic rewrite.
    let createApi (lang: ProviderLanguage) (question: string) (answer: string) (staged: StagedMutation) : obj =
        let setQuestion (value: obj) =
            if staged.QuestionWasSet then
                failureObject (prose lang Path.SetQuestionOnce)
            elif not (isString value) then
                failureObject (prose lang Path.SetQuestionString)
            else
                staged.QuestionWasSet <- true
                staged.Question <- Some(string value)
                successObject ()

        let setAnswer (value: obj) =
            if staged.AnswerWasSet then
                failureObject (prose lang Path.SetAnswerOnce)
            elif not (isString value) then
                failureObject (prose lang Path.SetAnswerString)
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

    /// The DSL the program extends at runtime: the anchor view over the
    /// staged question and answer, and the two write-once setters.
    let runtimeBaseClass =
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

    /// The DSL as the provider reads it: the members a program may call.
    let publicBaseClass =
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

    /// The worked example that shows how a program reshapes a Case.
    let ultraExample =
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

    /// The tool description: the prose, the DSL and the worked example.
    let assembleDescription (lang: ProviderLanguage) : string =
        String.concat
            "\n\n"
            [ prose lang Path.Description
              "```js\n" + publicBaseClass + "\n```"
              prose lang Path.UltraFraming
              "```js\n" + ultraExample + "\n```" ]

    /// The rendered result of a call that changed nothing.
    let failed (lang: ProviderLanguage) (reason: string) : string =
        LlmFacing.instructions [ prose lang Path.Unchanged; reason ] |> LlmFacing.render

    /// The rendered result of a settled call.
    let succeeded (lang: ProviderLanguage) (value: obj) : string =
        LlmFacing.instruction (prose lang Path.Accepted)
        |> LlmFacing.withData (LlmFacing.Data.structuredValue (LlmFacing.Data.ofJs value))
        |> LlmFacing.render
