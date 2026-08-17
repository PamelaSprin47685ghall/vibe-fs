namespace Wanxiangshu.Resources

open Wanxiangshu.Change
open Wanxiangshu.Git
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.Persistence

open System
open System.Text.RegularExpressions
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
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Resources
open Wanxiangshu.Foundation.Identity

/// PROMPT-019: load + substitute provider prose. Layout stays in SyntheticToml.
/// Domain owns semantic paths and pure assembly; this module owns language binding.
[<RequireQualifiedAccess>]
module ProviderProse =

    let private placeholderRe =
        Regex(@"\{\{([A-Za-z][A-Za-z0-9_]*)\}\}", RegexOptions.Compiled)

    /// Bound session → that language. Unbound → English (HOST-026 first-touch).
    /// Does not bind: session-create still
    /// owns the real preference write. Bound + missing resource still fail closed.
    let languageOf (sessionId: SessionId) : ProviderLanguage =
        match SessionProviderLanguage.tryGet sessionId with
        | Some lang -> lang
        | None -> ProviderLanguage.English

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
