namespace Wanxiangshu.Infrastructure.Resources

open System
open System.Text.RegularExpressions
open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

/// PROMPT-019: load + substitute provider prose. Layout stays in SyntheticToml.
/// Domain owns semantic paths and pure assembly; this module owns language binding.
[<RequireQualifiedAccess>]
module ProviderProse =

    let private placeholderRe = Regex(@"\{\{([A-Za-z][A-Za-z0-9_]*)\}\}", RegexOptions.Compiled)

    /// Bound session only. Unbound → fail closed (not silent English).
    let languageOf (sessionId: SessionId) : ProviderLanguage =
        match SessionProviderLanguage.tryGet sessionId with
        | Some lang -> lang
        | None ->
            raise (
                InvalidOperationException(
                    sprintf
                        "SessionProviderLanguage unbound for %s; refusing provider prose load (PROMPT-019 / HOST-026)"
                        (SessionId.value sessionId)
                )
            )

    /// Replace `{{name}}` with values. Values are not translated. Leftover placeholders fail closed.
    let substitute (template: string) (subs: Map<string, string>) : string =
        let replaced =
            placeholderRe.Replace(
                template,
                MatchEvaluator(fun m ->
                    let key = m.Groups.[1].Value

                    match Map.tryFind key subs with
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

    let render (lang: ProviderLanguage) (semanticPath: string) (subs: Map<string, string>) : string =
        ProviderResources.requireLanguagePair semanticPath
        substitute (ProviderResources.readText lang semanticPath) subs

    /// Instruction lines for SyntheticToml.document: preserve blank lines as "".
    let instructionLines (lang: ProviderLanguage) (semanticPath: string) (subs: Map<string, string>) : string list =
        render lang semanticPath subs
        |> fun text -> text.Replace("\r\n", "\n").TrimEnd('\n')
        |> fun text -> text.Split([| '\n' |], StringSplitOptions.None)
        |> Array.toList

    let document (lang: ProviderLanguage) (semanticPath: string) (subs: Map<string, string>) : string =
        SyntheticToml.document (instructionLines lang semanticPath subs) []

    let documentFor (sessionId: SessionId) (semanticPath: string) (subs: Map<string, string>) : string =
        document (languageOf sessionId) semanticPath subs
