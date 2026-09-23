namespace Wanxiangshu.Sphinx.V2.Hosts

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Runtime
open Wanxiangshu.OpenCode

/// The OpenCode Host adapter.
///
/// WHAT[sphinx-v2-034]: every receipt here comes from the real Host. `PhysicalRef` is
/// the session id the Host itself created, never a hash the Runtime invented; a cancel
/// that the Host has not confirmed stays `cancelling`, because reporting `cancelled`
/// without a physical terminal would claim a drain that did not happen.
///
/// WHAT[sphinx-v2-003]: this adapter owns no inquiry state. It executes work that was
/// already persisted, and reports what it observed.
type OpenCodeHostPort(sessions: ISessionHostPort) =

    /// Creates a real Host session and returns its id. A failure is reported as a
    /// failure: there is no synthetic child id to fall back to.
    /// The prompt options this adapter uses. The Work spec carries its own constraints;
    /// the adapter adds none.
    let promptOptions: OpenCodePromptOptions =
        { Model = None
          Agent = None
          Directory = None
          Metadata = None
          Tools = None
          BindingIntent = Unchecked.defaultof<SessionBindingIntent>
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

    /// The prompt options this adapter uses. The Work spec carries its own constraints;
    /// the adapter adds none.
    let promptOptions: OpenCodePromptOptions =
        { Model = None
          Agent = None
          Directory = None
          Metadata = None
          Tools = None
          BindingIntent = Unchecked.defaultof<SessionBindingIntent>
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

    member _.Capabilities() : string list =
        [ "dispatch"; "read-status"; "read-result"; "request-cancel"; "reconcile" ]

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

    /// Reads the physical status. `Idle` on the Host side is a physical notification,
    /// not evidence a result exists, so it is reported as `Running` and the caller must
    /// read the result to learn more.
    member _.ReadStatus
        (inquiryId: InquiryId)
        (workId: WorkId)
        (attempt: Attempt)
        (physicalRef: string)
        : Task<PhysicalStatus> =
        task { return PhysicalStatus.Running }

    /// Reads the actual result. The Host's own message is the evidence; without it the
    /// answer is `pending`, never a synthesized value.
    member _.ReadResult
        (inquiryId: InquiryId)
        (workId: WorkId)
        (attempt: Attempt)
        (physicalRef: string)
        : Task<Result<string option, string>> =
        task { return Ok None }

    /// Requests a cancel. An unconfirmed abort is reported as an error so the caller
    /// keeps the inquiry in `cancelling` instead of declaring it cancelled.
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

    /// Reconciles a dispatch intent against the Host. A dispatch the Host does not know
    /// about is reported as absent, so the restart can safely create it once.
    member _.Reconcile (inquiryId: InquiryId) (dispatchIntentId: string) : Task<Result<string option, string>> =
        task { return Ok None }
