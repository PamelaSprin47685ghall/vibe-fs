namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode.ProviderWireDecode
open Wanxiangshu.OpenCode.ProviderWireCapture
open Wanxiangshu.Persistence.Journal

/// The provider-step boundary of one managed chat execution.
///
/// This module owns no identity. Which session is managed, which participant it
/// runs as, and which model it may use all come from durable `Accepted` evidence
/// and the `ModelRouting` lease for the exact `(SessionId, PhysicalUserMessageId)`
/// the Host request carries. There is deliberately no session-current binding
/// cache here: a cache that can disagree with the durable record is how a
/// legitimate execution gets rejected for "no accepted execution binding".
module SessionExecutionBinding =

    /// The durable pre-provider execution for one exact key, if this process has
    /// ever accepted it. `None` means the Host request answers a message the
    /// plugin never admitted (a Host-internal or otherwise unmanaged request).
    let private acceptedExecution (durable: AgentJournal option) (key: ChatExecutionKey) =
        durable
        |> Option.bind (fun journal ->
            (AgentJournal.snapshot journal).AgentProjections.ChatExecutions
            |> ChatExecutionProjection.byKey key
            |> Option.map (fun execution -> journal, execution))

    /// host-boundary-008: is this exact key a managed execution this process
    /// admitted? Answered from durable evidence, never from a session-local map.
    let isManagedExecution (durable: AgentJournal option) (key: ChatExecutionKey) : bool =
        acceptedExecution durable key |> Option.isSome

    /// The exact provider-step key for this transform request: this request's
    /// trailing physical user message plus the provider runs its history already
    /// shows. Distinct provider steps of one physical execution derive distinct
    /// keys, so repeated notification of the same step is idempotent while the
    /// next step is a new admission.
    let private deriveTransformRequestKey
        (sessionId: SessionId)
        (physical: PhysicalUserMessageId)
        (visibleRuns: Set<ProviderRunIdentity>)
        =
        let sortedRuns =
            visibleRuns
            |> Seq.map ProviderRunIdentity.value
            |> Seq.sort
            |> String.concat ","

        sprintf "%s:%s:%s" (SessionId.value sessionId) (PhysicalUserMessageId.value physical) sortedRuns

    /// EMR-010 / host-boundary-008: enter the provider step of the physical
    /// message this request answers. A message with no durable `Accepted` is not
    /// a managed execution and is left alone; one that was accepted must hold a
    /// committed lease for that exact key or fail closed.
    let private enterBoundProviderStep
        (durable: AgentJournal option)
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (rawMessages: obj list)
        (requestKey: string option)
        : Task =
        let key: ChatExecutionKey =
            { SessionId = sessionId
              PhysicalUserMessageId = physicalUserMessageId }

        let enterCommittedStep () =
            let visibleRuns = ProviderWireCapture.visibleProviderRuns rawMessages

            ModelRouting.enterProviderStep sessionId physicalUserMessageId visibleRuns requestKey

        match isManagedExecution durable key, ModelRouting.tryReadExecution key with
        | false, _ -> Task.FromResult(())
        | true, Some _ -> enterCommittedStep ()
        | true, None ->
            raise (
                InvalidOperationException(
                    sprintf
                        "EMR-010: managed provider step for physical user message %s has no committed model-routing lease"
                        (PhysicalUserMessageId.value physicalUserMessageId)
                )
            )

    /// HOST-004: the transform boundary of one physical provider attempt.
    ///
    /// The exact key comes from this request's real messages. Quiescence begins
    /// here; provider-step capacity is entered only for a durable accepted
    /// execution. Nothing is remembered per session.
    let beginPhysicalProviderAttemptForTransform
        (journal: AgentJournal option)
        (beginQuiescence: SessionId -> unit)
        (projectionSessionIdOpt: string option)
        (outObj: obj)
        : Task<unit> =
        let attemptOfSession (sessionId: SessionId) (rawMessages: obj list) : Task =
            beginQuiescence sessionId

            let stepOf physical =
                let visibleRuns = ProviderWireCapture.visibleProviderRuns rawMessages

                enterBoundProviderStep
                    journal
                    sessionId
                    physical
                    rawMessages
                    (Some(deriveTransformRequestKey sessionId physical visibleRuns))

            match ProviderWireCapture.lastUserMessageId rawMessages with
            | None -> Task.FromResult()
            | Some physical -> stepOf physical

        task {
            match projectionSessionIdOpt with
            | None -> return ()
            | Some sid ->
                let sessionId = SessionId.create sid

                let rawMessages =
                    ProviderWireDecode.rawArray (ProviderWireDecode.readField outObj "messages")

                do! attemptOfSession sessionId rawMessages
        }
