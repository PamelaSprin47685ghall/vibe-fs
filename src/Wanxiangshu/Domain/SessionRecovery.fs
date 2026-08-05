namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel.Identity

/// P0-RECOVERY-FAMILY: closed recovery program + private family permit (FLOW-002/007).
///
/// Recovery is not a background flag. A parent resumes only with a
/// `FamilyRecoveryPermit` produced by this program.
module SessionRecovery =

    /// Non-empty list without inventing a shared collection type.
    type NonEmpty<'a> = { Head: 'a; Tail: 'a list }

    module NonEmpty =
        let one (value: 'a) : NonEmpty<'a> = { Head = value; Tail = [] }

        let ofList (values: 'a list) : NonEmpty<'a> option =
            match values with
            | [] -> None
            | head :: tail -> Some { Head = head; Tail = tail }

        let toList (values: NonEmpty<'a>) : 'a list = values.Head :: values.Tail

        let map (f: 'a -> 'b) (values: NonEmpty<'a>) : NonEmpty<'b> =
            { Head = f values.Head
              Tail = List.map f values.Tail }

    [<RequireQualifiedAccess>]
    type RecoveryBlock =
        | SnapshotUnreadable of SessionId * reason: string
        | MissingSession of SessionId
        | LinkageConflict of parent: SessionId * child: SessionId
        | RecoveryCycle of NonEmpty<SessionId>
        | PendingClaimUnknown of SessionId * PromptKey
        | ChildRecoveryFailed of SessionId * reason: string
        /// Family recovery ports never attached (composition root gap).
        | RecoveryCoordinatorUnavailable of SessionId

    [<RequireQualifiedAccess>]
    type RecoveryNode =
        | WorkSession of SessionId
        | AgentChild of parent: SessionId * child: SessionId * AgentHandleId
        | Companion of main: SessionId * companion: SessionId
        | Blogger of main: SessionId * blogger: SessionId
        | ManagerJob of ManagerJobId * manager: SessionId
        | Reviewer of ManagerJobId * reviewer: SessionId

    type RecoveryReceipt =
        private
            { SessionId: SessionId
              JournalSequence: int64
              SnapshotDigest: string option
              ResolvedClaims: PromptKey list
              RestoredHandles: AgentHandleId list }

    module RecoveryReceipt =
        let create
            (sessionId: SessionId)
            (journalSequence: int64)
            (snapshotDigest: string option)
            (resolvedClaims: PromptKey list)
            (restoredHandles: AgentHandleId list)
            : RecoveryReceipt =
            { SessionId = sessionId
              JournalSequence = journalSequence
              SnapshotDigest = snapshotDigest
              ResolvedClaims = resolvedClaims
              RestoredHandles = restoredHandles }

        let sessionId (receipt: RecoveryReceipt) = receipt.SessionId
        let journalSequence (receipt: RecoveryReceipt) = receipt.JournalSequence
        let snapshotDigest (receipt: RecoveryReceipt) = receipt.SnapshotDigest
        let resolvedClaims (receipt: RecoveryReceipt) = receipt.ResolvedClaims
        let restoredHandles (receipt: RecoveryReceipt) = receipt.RestoredHandles

    [<RequireQualifiedAccess>]
    type SessionRecovery =
        | NoRecoveryRequired of RecoveryReceipt
        | Recovered of RecoveryReceipt
        | Blocked of NonEmpty<RecoveryBlock>

    /// Private: only authorizeFamilyResume may construct.
    type FamilyRecoveryPermit =
        private | FamilyRecoveryPermit of root: SessionId * journalSequence: int64 * closureDigest: string

    module FamilyRecoveryPermit =
        let root (FamilyRecoveryPermit(root, _, _)) = root
        let journalSequence (FamilyRecoveryPermit(_, sequence, _)) = sequence
        let closureDigest (FamilyRecoveryPermit(_, _, digest)) = digest

    [<RequireQualifiedAccess>]
    type FamilyRecovery =
        | FamilyReady of FamilyRecoveryPermit
        | FamilyBlocked of NonEmpty<RecoveryBlock>

    type RecoveryClosure =
        {
            Root: SessionId
            /// Child-first: dependents before ancestors; siblings by SessionId.
            Nodes: RecoveryNode list
            Digest: string
            JournalSequence: int64
        }

    type ValidatedClosure = private ValidatedClosure of RecoveryClosure

    module ValidatedClosure =
        let value (ValidatedClosure closure) = closure

    type RecoveredClosure =
        { Closure: RecoveryClosure
          Results: Map<SessionId, SessionRecovery> }

    type ClaimRecovery =
        { SessionId: SessionId
          Outcome: SessionRecovery }

    type BloggerRecovery =
        { SessionId: SessionId
          Outcome: SessionRecovery }

    type HandleRecovery =
        { SessionId: SessionId
          Outcome: SessionRecovery }

    type JobRecovery =
        { JobId: ManagerJobId
          Outcome: SessionRecovery }

    /// Closed AST (FLOW-002). No arbitrary Task injection.
    type SessionRecoveryProgram<'result> =
        | Return of 'result
        | DiscoverClosure of SessionId * (RecoveryClosure -> SessionRecoveryProgram<'result>)
        | ReadSessionSnapshot of SessionId * (unit -> SessionRecoveryProgram<'result>)
        | RecoverPromptClaims of SessionId * (ClaimRecovery -> SessionRecoveryProgram<'result>)
        | RecoverBloggerWindow of SessionId * (BloggerRecovery -> SessionRecoveryProgram<'result>)
        | RestoreLinkedHandles of SessionId * (HandleRecovery -> SessionRecoveryProgram<'result>)
        | RecoverManagerJob of ManagerJobId * (JobRecovery -> SessionRecoveryProgram<'result>)
        | ValidateClosure of RecoveryClosure * (ValidatedClosure -> SessionRecoveryProgram<'result>)
        | AuthorizeResume of RecoveredClosure * (FamilyRecoveryPermit -> SessionRecoveryProgram<'result>)
        | Block of NonEmpty<RecoveryBlock>

    type SessionRecoveryBuilder() =
        member _.Return(value: 'result) : SessionRecoveryProgram<'result> = Return value
        member _.ReturnFrom(program: SessionRecoveryProgram<'result>) = program
        member _.Zero() : SessionRecoveryProgram<unit> = Return()

        member _.Delay(f: unit -> SessionRecoveryProgram<'result>) : SessionRecoveryProgram<'result> = f ()

        member _.Bind
            (program: SessionRecoveryProgram<'a>, cont: 'a -> SessionRecoveryProgram<'b>)
            : SessionRecoveryProgram<'b> =
            let rec bind current =
                match current with
                | Return value -> cont value
                | DiscoverClosure(sessionId, next) -> DiscoverClosure(sessionId, (fun closure -> bind (next closure)))
                | ReadSessionSnapshot(sessionId, next) -> ReadSessionSnapshot(sessionId, (fun () -> bind (next ())))
                | RecoverPromptClaims(sessionId, next) ->
                    RecoverPromptClaims(sessionId, (fun recovery -> bind (next recovery)))
                | RecoverBloggerWindow(sessionId, next) ->
                    RecoverBloggerWindow(sessionId, (fun recovery -> bind (next recovery)))
                | RestoreLinkedHandles(sessionId, next) ->
                    RestoreLinkedHandles(sessionId, (fun recovery -> bind (next recovery)))
                | RecoverManagerJob(jobId, next) -> RecoverManagerJob(jobId, (fun recovery -> bind (next recovery)))
                | ValidateClosure(closure, next) -> ValidateClosure(closure, (fun validated -> bind (next validated)))
                | AuthorizeResume(recovered, next) -> AuthorizeResume(recovered, (fun permit -> bind (next permit)))
                | Block blocks -> Block blocks

            bind program

    let sessionRecovery = SessionRecoveryBuilder()

    let discoverRecoveryClosure (sessionId: SessionId) : SessionRecoveryProgram<RecoveryClosure> =
        DiscoverClosure(sessionId, Return)

    let validateClosure (closure: RecoveryClosure) : SessionRecoveryProgram<ValidatedClosure> =
        ValidateClosure(closure, Return)

    let recoverPromptClaims (sessionId: SessionId) : SessionRecoveryProgram<ClaimRecovery> =
        RecoverPromptClaims(sessionId, Return)

    let recoverBloggerWindow (sessionId: SessionId) : SessionRecoveryProgram<BloggerRecovery> =
        RecoverBloggerWindow(sessionId, Return)

    let restoreLinkedHandles (sessionId: SessionId) : SessionRecoveryProgram<HandleRecovery> =
        RestoreLinkedHandles(sessionId, Return)

    let recoverManagerJob (jobId: ManagerJobId) : SessionRecoveryProgram<JobRecovery> = RecoverManagerJob(jobId, Return)

    let authorizeResume (recovered: RecoveredClosure) : SessionRecoveryProgram<FamilyRecoveryPermit> =
        AuthorizeResume(recovered, Return)

    let block (blocks: NonEmpty<RecoveryBlock>) : SessionRecoveryProgram<'result> = Block blocks

    let private sessionOfNode =
        function
        | RecoveryNode.WorkSession id
        | RecoveryNode.AgentChild(_, id, _)
        | RecoveryNode.Companion(_, id)
        | RecoveryNode.Blogger(_, id)
        | RecoveryNode.ManagerJob(_, id)
        | RecoveryNode.Reviewer(_, id) -> id

    /// Pure: duplicate session in ordered nodes → cycle block; else wrap.
    let validateClosurePure (closure: RecoveryClosure) : Result<ValidatedClosure, NonEmpty<RecoveryBlock>> =
        let rec check seen =
            function
            | [] -> Ok(ValidatedClosure closure)
            | node :: rest ->
                let sessionId = sessionOfNode node

                if Set.contains sessionId seen then
                    Error(NonEmpty.one (RecoveryBlock.RecoveryCycle(NonEmpty.one sessionId)))
                else
                    check (Set.add sessionId seen) rest

        check Set.empty closure.Nodes

    /// Pure authorize: any Blocked child → FamilyBlocked; else private permit.
    let authorizeFamilyResume
        (root: SessionId)
        (journalSequence: int64)
        (recovered: RecoveredClosure)
        : FamilyRecovery =
        let blocks =
            recovered.Results
            |> Map.toList
            |> List.collect (fun (_, outcome) ->
                match outcome with
                | SessionRecovery.Blocked bs -> NonEmpty.toList bs
                | SessionRecovery.NoRecoveryRequired _
                | SessionRecovery.Recovered _ -> [])

        match NonEmpty.ofList blocks with
        | Some nonEmpty -> FamilyRecovery.FamilyBlocked nonEmpty
        | None -> FamilyRecovery.FamilyReady(FamilyRecoveryPermit(root, journalSequence, recovered.Closure.Digest))

    let private mergeOutcomes (outcomes: SessionRecovery list) : SessionRecovery =
        match
            outcomes
            |> List.tryPick (function
                | SessionRecovery.Blocked bs -> Some(SessionRecovery.Blocked bs)
                | _ -> None)
        with
        | Some blocked -> blocked
        | None ->
            match
                outcomes
                |> List.tryPick (function
                    | SessionRecovery.Recovered r -> Some(SessionRecovery.Recovered r)
                    | _ -> None)
            with
            | Some recovered -> recovered
            | None ->
                match outcomes with
                | head :: _ -> head
                | [] -> SessionRecovery.NoRecoveryRequired(RecoveryReceipt.create (SessionId.create "") 0L None [] [])

    /// Child-first program for one parent family (RECOVERY-FAMILY-001/002).
    let recoverFamily (parentSession: SessionId) : SessionRecoveryProgram<FamilyRecovery> =
        sessionRecovery {
            let! closure = discoverRecoveryClosure parentSession

            match validateClosurePure closure with
            | Error blocks -> return FamilyRecovery.FamilyBlocked blocks
            | Ok validated ->
                let ordered = (ValidatedClosure.value validated).Nodes

                let rec recoverNodes
                    (nodes: RecoveryNode list)
                    (acc: Map<SessionId, SessionRecovery>)
                    : SessionRecoveryProgram<Map<SessionId, SessionRecovery>> =
                    match nodes with
                    | [] -> Return acc
                    | node :: rest ->
                        let sessionId, maybeJob =
                            match node with
                            | RecoveryNode.WorkSession id -> id, None
                            | RecoveryNode.AgentChild(_, id, _) -> id, None
                            | RecoveryNode.Companion(_, id) -> id, None
                            | RecoveryNode.Blogger(_, id) -> id, None
                            | RecoveryNode.ManagerJob(jobId, id) -> id, Some jobId
                            | RecoveryNode.Reviewer(jobId, id) -> id, Some jobId

                        sessionRecovery {
                            let! claims = recoverPromptClaims sessionId
                            let! blogger = recoverBloggerWindow sessionId
                            let! handles = restoreLinkedHandles sessionId

                            let! jobParts =
                                match maybeJob with
                                | None -> Return []
                                | Some jobId ->
                                    sessionRecovery {
                                        let! job = recoverManagerJob jobId
                                        return [ job.Outcome ]
                                    }

                            let merged =
                                mergeOutcomes (claims.Outcome :: blogger.Outcome :: handles.Outcome :: jobParts)

                            return! recoverNodes rest (Map.add sessionId merged acc)
                        }

                let! results = recoverNodes ordered Map.empty

                let closed = ValidatedClosure.value validated

                let recovered = { Closure = closed; Results = results }

                // Module-private permit construction (AuthorizeResume / authorizeFamilyResume).
                return authorizeFamilyResume parentSession closed.JournalSequence recovered
        }

    [<RequireQualifiedAccess>]
    type RecoveryTrace =
        | DiscoverClosure of SessionId
        | RecoverPromptClaims of SessionId
        | RecoverBloggerWindow of SessionId
        | RestoreLinkedHandles of SessionId
        | RecoverManagerJob of ManagerJobId
        | ValidateClosure of digest: string
        | FamilyReadyIssued of root: SessionId * digest: string
        | FamilyBlocked of count: int
        | BusinessOperation of name: string

    /// Trace interpreter (FLOW-003): pure walk, no effects.
    let rec trace (program: SessionRecoveryProgram<'result>) : RecoveryTrace list =
        match program with
        | Return _ -> []
        | DiscoverClosure(sessionId, next) ->
            RecoveryTrace.DiscoverClosure sessionId
            :: trace (
                next
                    { Root = sessionId
                      Nodes = []
                      Digest = ""
                      JournalSequence = 0L }
            )
        | ReadSessionSnapshot(sessionId, next) -> RecoveryTrace.DiscoverClosure sessionId :: trace (next ())
        | RecoverPromptClaims(sessionId, next) ->
            let receipt = RecoveryReceipt.create sessionId 0L None [] []

            RecoveryTrace.RecoverPromptClaims sessionId
            :: trace (
                next
                    { SessionId = sessionId
                      Outcome = SessionRecovery.NoRecoveryRequired receipt }
            )
        | RecoverBloggerWindow(sessionId, next) ->
            let receipt = RecoveryReceipt.create sessionId 0L None [] []

            RecoveryTrace.RecoverBloggerWindow sessionId
            :: trace (
                next
                    { SessionId = sessionId
                      Outcome = SessionRecovery.NoRecoveryRequired receipt }
            )
        | RestoreLinkedHandles(sessionId, next) ->
            let receipt = RecoveryReceipt.create sessionId 0L None [] []

            RecoveryTrace.RestoreLinkedHandles sessionId
            :: trace (
                next
                    { SessionId = sessionId
                      Outcome = SessionRecovery.NoRecoveryRequired receipt }
            )
        | RecoverManagerJob(jobId, next) ->
            let receipt = RecoveryReceipt.create (SessionId.create "") 0L None [] []

            RecoveryTrace.RecoverManagerJob jobId
            :: trace (
                next
                    { JobId = jobId
                      Outcome = SessionRecovery.NoRecoveryRequired receipt }
            )
        | ValidateClosure(closure, next) ->
            RecoveryTrace.ValidateClosure closure.Digest
            :: trace (next (ValidatedClosure closure))
        | AuthorizeResume(recovered, next) ->
            match authorizeFamilyResume recovered.Closure.Root 0L recovered with
            | FamilyRecovery.FamilyReady permit ->
                RecoveryTrace.FamilyReadyIssued(
                    FamilyRecoveryPermit.root permit,
                    FamilyRecoveryPermit.closureDigest permit
                )
                :: trace (next permit)
            | FamilyRecovery.FamilyBlocked blocks ->
                [ RecoveryTrace.FamilyBlocked(List.length (NonEmpty.toList blocks)) ]
        | Block blocks -> [ RecoveryTrace.FamilyBlocked(List.length (NonEmpty.toList blocks)) ]

    /// Trace property: no BusinessOperation before FamilyReadyIssued for same root.
    let familyReadyBeforeBusiness (traces: RecoveryTrace list) : bool =
        let rec walk seenReady =
            function
            | [] -> true
            | RecoveryTrace.FamilyReadyIssued _ :: rest -> walk true rest
            | RecoveryTrace.BusinessOperation _ :: _ when not seenReady -> false
            | _ :: rest -> walk seenReady rest

        walk false traces
