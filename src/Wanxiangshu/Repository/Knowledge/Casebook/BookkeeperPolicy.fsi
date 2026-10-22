namespace Wanxiangshu.Repository.Knowledge.Casebook

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
        val Description: string = "tool/js-bookkeeper/description"

        [<Literal>]
        val UltraFraming: string = "tool/js-bookkeeper/ultra-framing"

        [<Literal>]
        val ArgProgram: string = "tool/js-bookkeeper/arg-program"

        [<Literal>]
        val Unchanged: string = "tool/js-bookkeeper/unchanged"

        [<Literal>]
        val Accepted: string = "tool/js-bookkeeper/accepted"

        [<Literal>]
        val NoTransaction: string = "tool/js-bookkeeper/no-transaction"

        [<Literal>]
        val ProgramRequired: string = "tool/js-bookkeeper/program-required"

        [<Literal>]
        val SetQuestionOnce: string = "tool/js-bookkeeper/set-question-once"

        [<Literal>]
        val SetQuestionString: string = "tool/js-bookkeeper/set-question-string"

        [<Literal>]
        val SetAnswerOnce: string = "tool/js-bookkeeper/set-answer-once"

        [<Literal>]
        val SetAnswerString: string = "tool/js-bookkeeper/set-answer-string"

    /// One invocation's ephemeral staged mutation buffer; it is committed
    /// atomically only after the program succeeds.
    type StagedMutation =
        { mutable Question: string option
          mutable Answer: string option
          mutable QuestionWasSet: bool
          mutable AnswerWasSet: bool }

    /// The empty staged buffer of one invocation.
    val newStaged: unit -> StagedMutation

    /// The language a call's prose is rendered in. The live global preference
    /// is the only authority (HOST-026), so a call reads it when it runs.
    val callLanguage: unit -> ProviderLanguage

    /// The language the tool description is rendered in. The description is
    /// built once, when the tool is registered, so it reads the observed
    /// preference directly.
    val descriptionLanguage: unit -> ProviderLanguage

    /// The prose of one semantic path.
    val prose: lang: ProviderLanguage -> path: string -> string

    /// The API the program sees. The two setters are write-once: a program
    /// that sets the question twice is refused by name, not silently
    /// overwritten, because the staged Case is one atomic rewrite.
    val createApi: lang: ProviderLanguage -> question: string -> answer: string -> staged: StagedMutation -> obj

    /// The DSL the program extends at runtime.
    val runtimeBaseClass: string

    /// The DSL as the provider reads it: the members a program may call.
    val publicBaseClass: string

    /// The worked example that shows how a program reshapes a Case.
    val ultraExample: string

    /// The tool description: the prose, the DSL and the worked example.
    val assembleDescription: lang: ProviderLanguage -> string

    /// The rendered result of a call that changed nothing.
    val failed: lang: ProviderLanguage -> reason: string -> string

    /// The rendered result of a settled call.
    val succeeded: lang: ProviderLanguage -> value: obj -> string
