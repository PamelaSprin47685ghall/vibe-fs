namespace Wanxiangshu.Resources

open Wanxiangshu.Change
open Wanxiangshu.Git
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.Persistence

open System
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

/// Bilingual provider assets: `resources/provider/<semantic>/<en.md|zh-CN.md>` (PROMPT-017 §4.7.8).
[<RequireQualifiedAccess>]
module ProviderResources =

    let relativePath (lang: ProviderLanguage) (semanticPath: string) : string =
        let trimmed =
            if String.IsNullOrWhiteSpace semanticPath then
                ""
            else
                semanticPath.Trim().TrimStart('/').TrimStart('\\')

        sprintf "provider/%s/%s" trimmed (ProviderLanguage.resourceFileName lang)

    let exists (lang: ProviderLanguage) (semanticPath: string) : bool =
        PackageResources.exists (relativePath lang semanticPath)

    let readText (lang: ProviderLanguage) (semanticPath: string) : string =
        PackageResources.readText(relativePath lang semanticPath).Trim()

    let tryReadText (lang: ProviderLanguage) (semanticPath: string) : string option =
        if exists lang semanticPath then
            Some(readText lang semanticPath)
        else
            None

    /// ARCH-016 Gate C hook: both locale leaves must exist for a semantic path.
    let requireLanguagePair (semanticPath: string) : unit =
        for lang in [ ProviderLanguage.English; ProviderLanguage.SimplifiedChinese ] do
            if not (exists lang semanticPath) then
                raise (
                    InvalidOperationException(
                        sprintf
                            "provider resource missing for %s: %s (HOST-026 / ARCH-016 Gate C)"
                            (ProviderLanguage.label lang)
                            semanticPath
                    )
                )

    /// Layout smoke: provider tree root present.
    let languageRootsPresent () : bool = PackageResources.exists "provider"
