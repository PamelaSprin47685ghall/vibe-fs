namespace Wanxiangshu.Sphinx.V2.Hosts

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Runtime

/// The OpenCode Host adapter.
///
/// WHAT[sphinx-v2-034]: every receipt comes from the real Host. The physical reference
/// is the session id the Host itself created, never an identity the Runtime invented; a
/// cancel the Host has not confirmed is an error, so the caller keeps the inquiry in
/// cancelling rather than reporting a drain that did not happen.
///
/// WHAT[sphinx-v2-003]: this adapter owns no inquiry state. It executes work that was
/// already persisted and reports what it observed.
///
/// The OpenCode session port exposes no way to read a session's messages or tool
/// results, so ReadResult and Reconcile report that limitation instead of answering.
type OpenCodeHostPort(sessions: ISessionHostPort) =

    /// The prompt options this adapter uses. The Work spec carries its own constraints;
    /// the adapter adds none.
    let promptOptions: OpenCodePromptOptions =
        { Model = None
          Agent = None
          Directory = None
          Metadata = None
          Tools = None
          DetachedListener = None }

    let createSession (ownerSessionId: SessionId) (label: string) : Task<Result<SessionId, string>> =
        task {
            let options =
                { Title = Some label
                  Agent = None
                  Directory = None }

            match! sessions.CreateSiblingSession(ownerSessionId, None, options) with
            | Ok child -> return Ok child
            | Error reason -> return Error(sprintf "host session creation failed: %s" reason)
        }

    /// Translates one Host outcome into either a receipt or a failure. Only an outcome
    /// that carries a receipt may produce one; everything else is a failure, because a
    /// fabricated receipt would let the Runtime dispatch twice.
    let sendOutcomeToDispatch
        (session: SessionId)
        (work: WorkSpec)
        (inquiryId: InquiryId)
        (outcome: Outcome.SendOutcome)
        : Result<DispatchReceipt, string> =
        let receipt carrier =
            Ok
                { DispatchIntentId = Recovery.intentKey inquiryId work.Id work.Attempt
                  PhysicalRef = SessionId.value session
                  Receipt = carrier }

        match outcome with
        | Outcome.SendOutcome.AdmittedWithReceipt transportReceipt -> receipt (TransportReceipt.value transportReceipt)
        | Outcome.SendOutcome.AdmittedWithPhysicalMessage message -> receipt (PhysicalUserMessageId.value message)
        | Outcome.SendOutcome.Retryable reason -> Error(sprintf "host prompt retryable: %s" reason)
        | Outcome.SendOutcome.AcceptanceUnknown reason -> Error(sprintf "host prompt acceptance unknown: %s" reason)
        | Outcome.SendOutcome.Fatal reason -> Error(sprintf "host prompt fatal: %s" reason)

    member _.Capabilities() : string list = [ "dispatch"; "request-cancel" ]

    /// Dispatches already-persisted work to a real session. The receipt is whatever the
    /// Host returned, so a later reconcile can find the same physical session.
    member _.Dispatch
        (inquiryId: InquiryId)
        (work: WorkSpec)
        (publicEnvelope: JsonEnvelope)
        (privateTicket: JsonEnvelope)
        : Task<Result<DispatchReceipt, string>> =
        task {
            let owner = SessionId.create (InquiryId.value inquiryId)

            match! createSession owner ("sphinx-" + WorkId.value work.Id) with
            | Error reason -> return Error reason
            | Ok session ->
                let prompt = publicEnvelope.CanonicalPayload + "\n" + privateTicket.CanonicalPayload

                // An outcome that carries a receipt is the only one that may report a
                // dispatch. A retryable or fatal outcome must not become a fake receipt.
                let! hostOutcome = sessions.SendPrompt(session, prompt, promptOptions)
                return sendOutcomeToDispatch session work inquiryId hostOutcome
        }

    /// Reads the physical status. The session port exposes no status query, and a Host
    /// idle would be a notification rather than evidence a result exists, so the only
    /// honest answer here is that the status is not established.
    member _.ReadStatus
        (inquiryId: InquiryId)
        (workId: WorkId)
        (attempt: Attempt)
        (physicalRef: string)
        : Task<PhysicalStatus> =
        task { return PhysicalStatus.Unknown }

    /// Reads the actual result. The OpenCode session port carries no message or
    /// tool-result read, so a result cannot be observed at this boundary; reporting
    /// "pending" instead would be indistinguishable from a work that is still running.
    member _.ReadResult
        (inquiryId: InquiryId)
        (workId: WorkId)
        (attempt: Attempt)
        (physicalRef: string)
        : Task<Result<string option, string>> =
        task {
            return
                Error(
                    sprintf
                        "the OpenCode session port exposes no message or tool-result read, so the answer for work %s cannot be observed here"
                        (WorkId.value workId)
                )
        }

    /// Requests a cancel. An unconfirmed abort is an error so the caller keeps the
    /// inquiry in cancelling instead of declaring it cancelled.
    member _.RequestCancel
        (inquiryId: InquiryId)
        (workId: WorkId)
        (attempt: Attempt)
        (physicalRef: string)
        : Task<Result<Unit, string>> =
        task {
            let session = SessionId.create physicalRef

            match! sessions.AbortSession session with
            | Ok() -> return Ok()
            | Error reason -> return Error(sprintf "host cancel unconfirmed: %s" reason)
        }

    /// Reconciles a dispatch intent against the Host. Without a Host-side lookup for a
    /// dispatch, "not found" cannot be distinguished from "not observable here", so
    /// this reports the latter rather than inviting a second dispatch.
    member _.Reconcile (inquiryId: InquiryId) (dispatchIntentId: string) : Task<Result<string option, string>> =
        task {
            return
                Error(
                    sprintf
                        "the OpenCode session port exposes no dispatch lookup, so intent %s cannot be reconciled here"
                        dispatchIntentId
                )
        }
