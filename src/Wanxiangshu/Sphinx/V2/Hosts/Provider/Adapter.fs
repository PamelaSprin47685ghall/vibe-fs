namespace Wanxiangshu.Sphinx.V2.Hosts

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Plugins

module private Dispatch =

    /// Translates one plain lifecycle observation into the outcome the runtime records.
    ///
    /// A run the Host reports without usage is `UsageUnresolved`: the reservation stays
    /// booked, because writing zeros would claim an expensive call was free.
    let ofObservation (completion: obj) (runRef: string) : Wanxiangshu.Sphinx.V2.Plugins.ProviderOutcome =
        let usage = completion?usage

        let resolved () = (usage: obj) <> null

        let field name fallback =
            let value = usage?(name)

            match isNull value with
            | true -> fallback
            | false -> int64 (unbox<float> value)

        { Text = string completion?text
          InputTokens = field "inputTokens" 0L
          OutputTokens = field "outputTokens" 0L
          Calls = field "calls" 0L
          MoneyMinor = field "moneyMinor" 0L
          UsageUnresolved = not (resolved ())
          PhysicalRunRef = Some runRef }

    /// Prompts an already-created child and translates the Host outcome.
    ///
    /// WHAT[sphinx-v2-034]: a receipt-carrying outcome is the only one that may produce
    /// a run; everything else is a failure, because a synthesized run would let the
    /// Runtime bill a call that never happened.
    let child
        (sessions: ISessionHostPort)
        (child: SessionId)
        (promptText: string)
        : Task<Result<Wanxiangshu.Sphinx.V2.Plugins.ProviderOutcome, string>> =
        task {
            let promptOptions: OpenCodePromptOptions =
                { Model = None
                  Agent = None
                  Directory = None
                  Metadata = None
                  Tools = None
                  BindingIntent = Unchecked.defaultof<SessionBindingIntent>
                  DetachedListener = None }

            let admitted (run: string) =
                task {
                    let handle = HostForkRunLifecycleSurface.create (box run)
                    let! completion = HostForkRunLifecycleSurface.completion handle
                    return ofObservation completion run
                }

            let! sendOutcome = sessions.SendPrompt(child, promptText, promptOptions)

            match sendOutcome with
            | Outcome.SendOutcome.Retryable reason -> return Error(sprintf "provider prompt retryable: %s" reason)
            | Outcome.SendOutcome.AcceptanceUnknown reason ->
                return Error(sprintf "provider prompt acceptance unknown: %s" reason)
            | Outcome.SendOutcome.Fatal reason -> return Error(sprintf "provider prompt fatal: %s" reason)
            | Outcome.SendOutcome.AdmittedWithReceipt receipt ->
                let! outcome = admitted (TransportReceipt.value receipt)
                return Ok outcome
            | Outcome.SendOutcome.AdmittedWithPhysicalMessage message ->
                let! outcome = admitted (PhysicalUserMessageId.value message)
                return Ok outcome
        }

/// The provider adapter.
///
/// WHAT[sphinx-v2-021]: a provider call is a WorkItem. It never hides inside a loop, and
/// its real cost is charged to the ledger whether or not the result is later adopted.
///
/// WHAT[sphinx-v2-013]: this adapter owns no inquiry state. It executes a call that was
/// already persisted and returns the outcome the Host reported, including the case where
/// the Host reported no usage at all.
type ProviderAdapter(sessions: ISessionHostPort) =

    /// Runs one provider call and returns what the Host actually observed.
    ///
    /// The physical run reference is the Host's own identity for this run; a failure
    /// carries the reason, never a synthesized receipt.
    member _.Run
        (inquiryId: InquiryId)
        (promptText: string)
        (parentSession: SessionId)
        : Task<Result<Wanxiangshu.Sphinx.V2.Plugins.ProviderOutcome, string>> =
        task {
            let options =
                { OpenCodeChildOptions.Title = Some("sphinx-provider-" + SessionId.value parentSession)
                  Agent = None
                  Directory = None }

            match! sessions.CreateChildSession(parentSession, options) with
            | Error reason -> return Error(sprintf "provider session creation failed: %s" reason)
            | Ok child -> return! Dispatch.child sessions child promptText
        }

    /// Cancels one provider call. An unconfirmed cancel is reported as an error so the
    /// caller keeps the work in its cancelling state rather than declaring it done.
    member _.Cancel (inquiryId: InquiryId) (runRef: string) : Task<Result<Unit, string>> =
        task {
            let session = SessionId.create runRef

            match! sessions.AbortSession session with
            | Ok() -> return Ok()
            | Error reason -> return Error(sprintf "provider cancel unconfirmed: %s" reason)
        }
