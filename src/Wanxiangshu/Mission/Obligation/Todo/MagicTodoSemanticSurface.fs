namespace Wanxiangshu.Mission.Obligation.Todo

open System
open Fable.Core.JsInterop
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoAdmission
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoAfter
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoPrefixEpoch
open Wanxiangshu.Resources

/// JS-native semantic entry points for Magic Todo's pure owner modules.
/// Domain values are decoded here and only JSON-shaped observations leave.
[<RequireQualifiedAccess>]
module MagicTodoSemanticSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private stringOption (value: obj) =
        if isNull value then None else Some(text value)

    let private int64Value (value: obj) : int64 = int64 (text value)

    let private prefixSnapshotOf (value: obj) : PrefixSnapshot =
        { FrozenRecordPrefixRef = BlobRef.create (text value?ref)
          FrozenRecordPrefixDigest = BlobDigest.create (text value?frozenDigest)
          CutoffExclusive = int (text value?cutoff)
          CoveredPrefixDigest = text value?prefixDigest
          SealRoot = text value?sealRoot
          SyntheticMessageId = text value?syntheticId }

    let private obligationOf (value: obj) : Obligation =
        { Name = text (value?name)
          Work = text (value?work) }

    let private obligationsOf (value: obj) : ObligationList =
        if isNull value then
            []
        else
            value |> unbox<obj array> |> Array.toList |> List.map obligationOf

    let private obligationsToJs (items: ObligationList) : obj array =
        items
        |> List.map (fun item -> box {| name = item.Name; work = item.Work |})
        |> List.toArray

    let private rejectCode reject =
        match reject with
        | MagicTodoReject.MultipleTodowriteInMessage callIds ->
            box
                {| code = "MultipleTodowriteInMessage"
                   callIds = List.toArray callIds |}
        | MagicTodoReject.EmptyObligationName ordinal ->
            box
                {| code = "EmptyObligationName"
                   ordinal = ordinal |}
        | MagicTodoReject.DuplicateObligationName name ->
            box
                {| code = "DuplicateObligationName"
                   name = name |}
        | MagicTodoReject.IdentityCorruption field ->
            box
                {| code = "IdentityCorruption"
                   field = field |}
        | MagicTodoReject.AwaitingConsumableReview pending ->
            box
                {| code = "AwaitingConsumableReview"
                   pendingTodoWriteId = pending |}
        | MagicTodoReject.FirstSuicideWithoutCheckpoint -> box {| code = "FirstSuicideWithoutCheckpoint" |}

    let private resultToJs mapValue value =
        match value with
        | Ok result -> box {| ok = true; value = mapValue result |}
        | Error error ->
            box
                {| ok = false
                   error = rejectCode error |}

    let private cursor (sequence: int) : XTraceCursor = { Sequence = int64 sequence }

    let private traceAnchorOf (value: obj) : TracePartAnchor =
        { Cursor = cursor (unbox<int> (value?sequence))
          Kind = text (value?kind)
          ToolCallId = stringOption (value?toolCallId) |> Option.map ToolCallId.create }

    let private traceAnchorsOf (values: obj array) =
        if isNull values then
            []
        else
            values |> Array.toList |> List.map traceAnchorOf

    let canonicalObligationListWire (items: obj array) : string =
        MagicTodo.canonicalObligationListWire (obligationsOf (box items))

    let obligationListDigest (sha256: string -> string) (items: obj array) : string =
        MagicTodo.obligationListDigest sha256 (obligationsOf (box items))

    let validateObligations (items: obj array) : obj =
        MagicTodo.validateObligations (obligationsOf (box items))
        |> resultToJs (fun values -> box (obligationsToJs values))

    let admitTodowriteBatch (callIds: string array) : obj =
        let ids =
            if isNull callIds then
                []
            else
                callIds |> Array.toList |> List.map ToolCallId.create

        MagicTodo.admitTodowriteBatch ids |> resultToJs (fun () -> null)

    let checkPreparedReplay (expected: obj) (observed: obj) : obj =
        let identityOf value : PreparedIdentity =
            { ManagerLifeId = ManagerLifeId.create (text (value?managerLifeId))
              ProviderInputDigest = text (value?providerInputDigest)
              BaseTodoDigest = text (value?baseTodoDigest)
              ToolPartOrdinal = unbox<int> (value?toolPartOrdinal) }

        MagicTodo.checkPreparedReplay (identityOf expected) (identityOf observed)
        |> resultToJs (fun () -> null)

    let admitObligations
        (sha256: string -> string)
        (lifeId: string)
        (current: obj array)
        (mayProceed: obj)
        (existing: obj)
        (localized: obj)
        (submitted: obj array)
        : obj =
        let currentItems = obligationsOf (box current)
        let submittedItems = obligationsOf (box submitted)

        let mayProceedResult: Result<unit, MagicTodoReject> =
            if isNull mayProceed || not (unbox<bool> (mayProceed?ok)) then
                Error(MagicTodoReject.AwaitingConsumableReview "pending-review")
            else
                Ok()

        let existingPrepared: ExistingPrepared option =
            if isNull existing then
                None
            else
                let identity =
                    { ManagerLifeId = ManagerLifeId.create (text (existing?managerLifeId))
                      ProviderInputDigest = text (existing?providerInputDigest)
                      BaseTodoDigest = text (existing?baseTodoDigest)
                      ToolPartOrdinal = unbox<int> (existing?toolPartOrdinal) }

                Some
                    { Identity = identity
                      TodoWriteId = TodoWriteId.create (text (existing?todoWriteId))
                      Accepted =
                        if isNull (existing?accepted) then
                            false
                        else
                            unbox<bool> (existing?accepted) }

        let localizedCall: AdmissionLocalizedToolCall =
            { ToolCallId = ToolCallId.create (text (localized?toolCallId))
              ToolPartOrdinal = unbox<int> (localized?toolPartOrdinal)
              TodowriteCallIdsInMessage =
                if isNull (localized?todowriteCallIds) then
                    []
                else
                    (localized?todowriteCallIds)
                    |> unbox<string array>
                    |> Array.toList
                    |> List.map ToolCallId.create
              ReviewFrontier = cursor (unbox<int> (localized?reviewFrontier))
              ProviderInputDigest = text (localized?providerInputDigest) }

        let outcome =
            MagicTodoAdmission.admitObligations
                sha256
                (ManagerLifeId.create lifeId)
                currentItems
                mayProceedResult
                existingPrepared
                localizedCall
                submittedItems

        match outcome with
        | MagicTodoAdmission.AdmissionOutcome.FreshPrepare prepared ->
            box
                {| kind = "FreshPrepare"
                   value =
                    box
                        {| todoWriteId = TodoWriteId.value prepared.TodoWriteId
                           baseObligations = obligationsToJs prepared.Base
                           proposed = obligationsToJs prepared.Proposed
                           baseDigest = prepared.BaseDigest
                           proposedDigest = prepared.ProposedDigest
                           toolPartOrdinal = prepared.ToolPartOrdinal
                           providerInputDigest = prepared.ProviderInputDigest |} |}
        | MagicTodoAdmission.AdmissionOutcome.IdempotentReplay writeId ->
            box
                {| kind = "IdempotentReplay"
                   todoWriteId = TodoWriteId.value writeId |}
        | MagicTodoAdmission.AdmissionOutcome.AwaitingConsumableReview pending ->
            box
                {| kind = "AwaitingConsumableReview"
                   pendingTodoWriteId = pending |}
        | MagicTodoAdmission.AdmissionOutcome.Rejected rejection ->
            box
                {| kind = "Rejected"
                   error = rejectCode rejection |}

    let todoWriteId (sha256: string -> string) (lifeId: string) (toolCallId: string) : string =
        MagicTodo.todoWriteId sha256 (ManagerLifeId.create lifeId) (ToolCallId.create toolCallId)
        |> TodoWriteId.value

    let todoReviewId (sha256: string -> string) (lifeId: string) (todoWriteId: string) : string =
        MagicTodo.todoReviewId sha256 (ManagerLifeId.create lifeId) (TodoWriteId.create todoWriteId)
        |> TodoReviewId.value

    let dedicatedReviewerId (sha256: string -> string) (lifeId: string) : string =
        MagicTodo.dedicatedReviewerId sha256 (ManagerLifeId.create lifeId)
        |> DedicatedReviewerId.value

    let todoWriteIdValue (value: string) = value

    let desiredLag1Cutoff (acceptedInOrder: string array) : string option =
        let ids =
            if isNull acceptedInOrder then
                []
            else
                acceptedInOrder |> Array.toList |> List.map TodoWriteId.create

        MagicTodo.desiredLag1Cutoff ids |> Option.map TodoWriteId.value

    let workRecordStart (openingSequence: int) : int =
        MagicTodo.workRecordStart (cursor openingSequence)
        |> fun value -> int value.Sequence

    let managerCheckpointLwrStart (openingSequence: int) (latestConcludedSequence: obj) : int =
        let latest =
            if isNull latestConcludedSequence then
                None
            else
                Some(cursor (unbox<int> latestConcludedSequence))

        MagicTodo.managerCheckpointLwrStart (cursor openingSequence) latest
        |> fun value -> int value.Sequence

    let blindPlanOpeningBoundary
        (openingSequence: int)
        (t1CallSequence: int)
        (t1ToolCallId: string)
        (parts: obj array)
        : int =
        MagicTodo.blindPlanOpeningBoundary
            (cursor openingSequence)
            (cursor t1CallSequence)
            (ToolCallId.create t1ToolCallId)
            (traceAnchorsOf parts)
        |> fun value -> int value.Sequence

    let effectiveOpeningFloor
        (hasOpenLife: bool)
        (planCommitted: bool)
        (openingSequence: int)
        (t1CallSequence: obj)
        (t1ToolCallId: obj)
        (xTraceHeadSequence: int)
        (parts: obj array)
        : obj =
        let callSequence =
            if isNull t1CallSequence then
                None
            else
                Some(cursor (unbox<int> t1CallSequence))

        let callId = stringOption t1ToolCallId |> Option.map ToolCallId.create

        MagicTodo.effectiveOpeningFloor
            hasOpenLife
            planCommitted
            (cursor openingSequence)
            callSequence
            callId
            (int64 xTraceHeadSequence)
            (traceAnchorsOf parts)
        |> Option.map (fun value -> box (int value.Sequence))
        |> Option.toObj

    let bloggerEffectiveStart (ingestedThrough: int) (workRecordStartSequence: int) : int =
        MagicTodo.bloggerEffectiveStart { IngestedThrough = cursor ingestedThrough } (cursor workRecordStartSequence)
        |> fun value -> int value.Sequence

    let requirePlanCommitmentBeforeFirstSuicide (planCommitted: bool) : obj =
        MagicTodo.requirePlanCommitmentBeforeFirstSuicide planCommitted
        |> resultToJs (fun () -> null)

    let assignmentDelivery (hasActiveProfile: bool) : string =
        match MagicTodoAfter.assignmentDelivery hasActiveProfile with
        | AssignmentDelivery.OwnerRoot -> "OwnerRoot"
        | AssignmentDelivery.Continuation -> "Continuation"

    let todoCheckpointEvidence (trigger: string) (previousCommitted: obj) : obj =
        let previous = stringOption previousCommitted |> Option.map TodoWriteId.create

        match MagicTodoPrefixEpoch.todoCheckpointEvidence (TodoWriteId.create trigger) previous with
        | PrefixEvidenceKind.TodoCheckpoint(triggerId, covered) ->
            box
                {| kind = "TodoCheckpoint"
                   triggerTodoWriteId = TodoWriteId.value triggerId
                   coveredBeforeTodoWriteId = covered |> Option.map TodoWriteId.value |> Option.toObj |}
        | PrefixEvidenceKind.Probe probe -> box {| kind = "Probe"; probeId = probe |}

    let buildTodoCheckpointCommit (value: obj) : obj =
        let commit =
            MagicTodoPrefixEpoch.buildTodoCheckpointCommit
                (SessionId.create (text value?sessionId))
                (ManagerLifeId.create (text value?managerLifeId))
                (PrefixEpochId.create (int64Value value?previousEpoch))
                (prefixSnapshotOf value?snapshot)
                (stringOption value?previousCommitted |> Option.map TodoWriteId.create)
                (TodoWriteId.create (text value?trigger))
                (BlobRef.create (text value?yBundleRef))
                (BlobDigest.create (text value?yBundleDigest))
                (text value?providerPrefixDigest)

        let evidence =
            match commit.EvidenceKind with
            | PrefixEvidenceKind.Probe probe -> box {| kind = "Probe"; probeId = probe |}
            | PrefixEvidenceKind.TodoCheckpoint(triggerId, coveredBefore) ->
                box
                    {| kind = "TodoCheckpoint"
                       triggerTodoWriteId = TodoWriteId.value triggerId
                       coveredBeforeTodoWriteId = coveredBefore |> Option.map TodoWriteId.value |> Option.toObj |}

        box
            {| sessionId = SessionId.value commit.SessionId
               managerLifeId = commit.ManagerLifeId |> Option.map ManagerLifeId.value |> Option.toObj
               previousEpoch = PrefixEpochId.value commit.PreviousEpochId
               nextEpoch = PrefixEpochId.value commit.NextEpochId
               evidenceKind = evidence
               frozenRecordPrefixRef = BlobRef.value commit.FrozenRecordPrefixRef
               frozenRecordPrefixDigest = BlobDigest.value commit.FrozenRecordPrefixDigest
               cutoffExclusive = commit.CutoffExclusive
               coveredPrefixDigest = commit.CoveredPrefixDigest
               sealRoot = commit.SealRoot
               syntheticMessageId = commit.SyntheticMessageId
               yBundleRef = commit.YBundleRef |> Option.map BlobRef.value |> Option.toObj
               yBundleDigest = commit.YBundleDigest |> Option.map BlobDigest.value |> Option.toObj
               providerPrefixDigest = commit.ProviderPrefixDigest |> Option.toObj
               solvingProviderRun =
                commit.SolvingProviderRun
                |> Option.map ProviderRunIdentity.value
                |> Option.toObj |}

    let requiresLag1Rebase (previousCommitted: obj) : bool =
        MagicTodoPrefixEpoch.requiresLag1Rebase (stringOption previousCommitted |> Option.map TodoWriteId.create)

    let wrapT1AcceptedResult (sessionId: string) (body: string) : string =
        let revelation =
            ProviderProse.documentFor (SessionId.create sessionId) ManagerNarrative.Path.T1Revelation Map.empty

        ManagerNarrative.wrapT1AcceptedResult revelation body
