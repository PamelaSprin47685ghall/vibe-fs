namespace Wanxiangshu.Participant.Provider

open System
open System.Text.RegularExpressions
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// PROMPT-019: load + substitute provider prose. LLM-facing layout stays in LlmFacing.
/// Domain owns semantic paths and pure assembly; this module owns language binding.
[<RequireQualifiedAccess>]
module ProviderProse =

    let private placeholderRe =
        Regex(@"\{\{([A-Za-z][A-Za-z0-9_]*)\}\}", RegexOptions.Compiled)

    /// Bound session → that language. Unbound → English (HOST-026 first-touch).
    let languageOf (sessionId: SessionId) : ProviderLanguage =
        SessionProviderLanguage.languageOf sessionId

    /// Replace `{{name}}` with values. Values are not translated. Leftover placeholders fail closed.
    let substitute (template: string) (substitutions: Map<string, string>) : string =
        let replaced =
            placeholderRe.Replace(
                template,
                MatchEvaluator(fun matched ->
                    let key = matched.Groups.[1].Value

                    match Map.tryFind key substitutions with
                    | Some value -> value
                    | None ->
                        raise (
                            InvalidOperationException(
                                sprintf "provider prose missing substitution {{%s}} (PROMPT-019)" key
                            )
                        ))
            )

        if placeholderRe.IsMatch replaced then
            raise (InvalidOperationException("provider prose retained unsubstituted placeholders (PROMPT-019)"))

        replaced

    let render (language: ProviderLanguage) (semanticPath: string) (substitutions: Map<string, string>) : string =
        ProviderResources.requireLanguagePair semanticPath
        substitute (ProviderResources.readText language semanticPath) substitutions

    /// Instruction lines for LlmFacing: preserve blank lines as "".
    let instructionLines
        (language: ProviderLanguage)
        (semanticPath: string)
        (substitutions: Map<string, string>)
        : string list =
        render language semanticPath substitutions
        |> fun text -> text.Replace("\r\n", "\n").TrimEnd('\n')
        |> fun text -> text.Split([| '\n' |], StringSplitOptions.None)
        |> Array.toList

    let document (language: ProviderLanguage) (semanticPath: string) (substitutions: Map<string, string>) : string =
        LlmFacing.renderInstructions (instructionLines language semanticPath substitutions)

    let documentFor (sessionId: SessionId) (semanticPath: string) (substitutions: Map<string, string>) : string =
        document (SessionProviderLanguage.languageOf sessionId) semanticPath substitutions
