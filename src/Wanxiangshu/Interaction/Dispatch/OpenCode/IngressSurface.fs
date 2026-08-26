namespace Wanxiangshu.Interaction.Dispatch

open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Persistence.Journal

/// Dispatch.IngressSurface — the sole published contract for ingress & dispatch codec.
///
/// All Host -> Authority ingress (chat.message decoding, PromptKey metadata, and
/// physical-acceptance hook) must go through this surface. HostSignalBootstrap
/// and other composition roots consume only this surface; direct opens of
/// PromptIngress / PromptIngressCodec / PromptMetadataCodec / HostSessionNudge
/// are forbidden by the ingress ratchet.
///
/// Classification: KEEP (dispatch-protocol owner)
/// Publishes: Dispatch.IngressSurface
/// Consumes: DurableEvents.Contract
[<RequireQualifiedAccess>]
module IngressSurface =

    /// Re-export DecodedMessage so callers do not open PromptIngressCodec directly.
    type DecodedMessage = PromptIngressCodec.DecodedMessage

    /// F#-native decode (returns typed DecodedMessage) for HostSignalBootstrap and other F# callers.
    let decodeMessage (input: obj) (output: obj) : DecodedMessage = PromptIngressCodec.decode input output


    /// JS-friendly decode that boxes Options as nullable strings for the JS proof surface.
    let decode (input: obj) (output: obj) : obj =
        let decoded = PromptIngressCodec.decode input output

        box
            {| SessionId = decoded.SessionId |> Option.map SessionId.value |> Option.defaultValue null
               PhysicalUserMessageId =
                decoded.PhysicalUserMessageId
                |> Option.map PhysicalUserMessageId.value
                |> Option.defaultValue null
               ExplicitAgent = decoded.ExplicitAgent |> Option.defaultValue null
               PromptKey = decoded.PromptKey |> Option.map PromptKey.value |> Option.defaultValue null
               IsHostCompaction = decoded.IsHostCompaction
               Text = decoded.Text |> Option.defaultValue null |}

    /// Create the metadata packet carried on Host prompt boundary (PROMPT-011).
    /// Re-exports PromptMetadataCodec.create so callers never spell the field name.
    let createMetadata (promptKey: string) (origin: string) (logicalRunId: string) : obj =
        let key = PromptKey.create promptKey

        let runOpt =
            if isNull logicalRunId then
                None
            else
                Some(LogicalRunId.create logicalRunId)

        PromptMetadataCodec.create key origin runOpt

    /// The field name is part of the contract — consumers must not hard-code it.
    let promptKeyField: string = PromptMetadataCodec.PromptKeyField

    /// F#-native hook factory used by HostSignalBootstrap (AgentJournal option).
    let createHook
        (journal: AgentJournal option)
        (bindUserMessage: string -> string -> unit)
        (bindContinuationMessage: string -> string -> unit)
        (registerOwned: string -> unit)
        (onAuthorityRoot: ((SessionId * AuthorityRootUserMessageId) -> unit) option)
        (onContinuationAccepted: ((SessionId * PhysicalUserMessageId * PromptAuthority.ContinuationKind) -> unit) option)
        =
        PromptIngress.createHook
            journal
            bindUserMessage
            bindContinuationMessage
            registerOwned
            onAuthorityRoot
            onContinuationAccepted

    /// JS-friendly hook factory for tests (handle obj, plain functions).
    /// Mirrors DispatchSurface pattern: handle may be null, callbacks are obj.
    let createHookForJs
        (handle: obj)
        (bindUserMessage: obj)
        (bindContinuationMessage: obj)
        (registerOwned: obj)
        (onAuthorityRoot: obj)
        (onContinuationAccepted: obj)
        : (obj -> obj -> Task<obj>) =
        let journalOpt =
            if isNull handle then
                None
            else
                try
                    let h = handle?Journal
                    if isNull h then None else Some(unbox<AgentJournal> h)
                with _ ->
                    None

        let toFSharp (f: obj) : (string -> string -> unit) =
            if isNull f then
                (fun _ _ -> ())
            else
                fun a b ->
                    let fn = unbox<string -> string -> unit> f
                    fn a b
                    // also support JS function (a,b) via interop
                    if not (isNull f) && not (isNull f?Invoke) then
                        emitJsExpr (f, a, b) "$0($1,$2)" |> ignore
        // For test we just need to return a function; delegate to real hook if possible
        let hook =
            PromptIngress.createHook
                journalOpt
                (toFSharp bindUserMessage)
                (toFSharp bindContinuationMessage)
                (fun _ -> ())
                None
                None

        fun (input: obj) (output: obj) ->
            task {
                do! hook input output
                return null
            }

    /// Re-export HostSessionNudge helpers so HostSignalBootstrap does not open that module directly.
    let tryActiveProfile (journal: AgentJournal option) (sessionId: SessionId) =
        HostSessionNudge.tryActiveProfile journal sessionId

    let sendContinuationResult
        (sessionPort: obj)
        (sessionId: SessionId)
        (prompt: string)
        (kind: PromptAuthority.ContinuationKind)
        (directory: string option)
        (journal: AgentJournal option)
        (awaitMode: PromptDispatcher.AwaitMode)
        (onAccepted: (PhysicalUserMessageId -> unit) option)
        : Task<Result<PromptKey, string>> =
        let port = sessionPort |> unbox<Wanxiangshu.OpenCode.ISessionHostPort>
        HostSessionNudge.sendContinuationResult port sessionId prompt kind directory journal awaitMode onAccepted

    let sendContinuation
        (sessionPort: obj)
        (sessionId: SessionId)
        (prompt: string)
        (kind: PromptAuthority.ContinuationKind)
        (directory: string option)
        (journal: AgentJournal option)
        : Task<Result<PromptKey, string>> =
        let port = sessionPort |> unbox<Wanxiangshu.OpenCode.ISessionHostPort>
        HostSessionNudge.sendContinuation port sessionId prompt kind directory journal
