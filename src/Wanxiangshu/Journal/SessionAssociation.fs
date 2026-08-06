namespace Wanxiangshu.Journal

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// HOST-008 / COMPANION-002: which kind of managed session this is.
///
/// This is what replaced Companion eligibility. The transform boundary no longer
/// asks "does this role deserve a Companion" — a policy whitelist over ten roles,
/// which COMPANION-001 deleted — but "is this session itself a Companion", which is
/// a structural fact with one answer.
///
/// `CompanionSession` carries its main session because the relation must be
/// answerable in both directions from one keyed lookup (PERSIST-008). Storing only
/// `WorkSession → BloggerSessionId` would force a scan of every session to discover
/// whether a given id is somebody's Y.
[<RequireQualifiedAccess>]
type ManagedSessionKind =
    /// A session that can issue ordinary provider requests. Has exactly one Y.
    | WorkSession
    /// A Companion Blogger session. A leaf: never has a Y of its own.
    | CompanionSession of mainSessionId: SessionId

/// HOST-008: the durable Work ↔ Companion relation for one session.
type SessionAssociation =
    {
        SessionId: SessionId
        Kind: ManagedSessionKind
        /// COMPANION-002: `Some` for a Work session once its Y exists, and always
        /// `None` for a Companion session. The invariant is enforced by
        /// `SessionAssociationProjection.link`, not left to callers.
        BloggerSessionId: SessionId option
        ParentSessionId: SessionId option
    }

/// Why an association write was refused.
[<RequireQualifiedAccess>]
type AssociationRejection =
    /// COMPANION-002: a Companion session may not have a Companion of its own.
    | CompanionWouldRecurse of companion: SessionId
    /// A session cannot be its own Companion.
    | SelfLink of session: SessionId
    /// COMPANION-002: this Work session already has a different Y. Creating a second
    /// one is what the lazy-creation rule exists to prevent, so the fold refuses it
    /// rather than silently repointing.
    | AlreadyLinkedToOther of existing: SessionId * proposed: SessionId
    /// The proposed Y is already the Companion of a different Work session.
    | CompanionClaimedByOther of owner: SessionId * proposed: SessionId

/// HOST-008. One map, keyed lookup, no scan (PERSIST-008).
///
/// Both sides of a link live in the same map as separate entries, written together
/// from one fact. That is what makes "is this a Companion" and "which Y does this X
/// have" both O(1) without a second index that could disagree.
module SessionAssociationProjection =

    let empty: Map<SessionId, SessionAssociation> = Map.empty

    let tryFind (sessionId: SessionId) (current: Map<SessionId, SessionAssociation>) = Map.tryFind sessionId current

    /// COMPANION-002: is this session a Companion.
    ///
    /// An unknown session answers `false`. COMPANION-001 gives every managed work
    /// session a Y, so "no record yet" means a work session whose Y has not been
    /// lazily created — the state the next transform resolves. It never means "a
    /// Companion we have not heard of": a Y's association is written before its first
    /// prompt, so a Y always has a record by the time a transform can fire for it.
    let isCompanion (sessionId: SessionId) (current: Map<SessionId, SessionAssociation>) =
        match tryFind sessionId current with
        | Some { Kind = ManagedSessionKind.CompanionSession _ } -> true
        | _ -> false

    /// COMPANION-002: the main session a Companion belongs to.
    let tryMainSessionOf (sessionId: SessionId) (current: Map<SessionId, SessionAssociation>) =
        match tryFind sessionId current with
        | Some { Kind = ManagedSessionKind.CompanionSession main } -> Some main
        | _ -> None

    /// COMPANION-003: the Y this Work session already has, so a restart reuses it.
    let tryBloggerOf (sessionId: SessionId) (current: Map<SessionId, SessionAssociation>) =
        tryFind sessionId current |> Option.bind (fun entry -> entry.BloggerSessionId)

    let private workSession sessionId parent blogger =
        { SessionId = sessionId
          Kind = ManagedSessionKind.WorkSession
          BloggerSessionId = blogger
          ParentSessionId = parent }

    /// HOST-008: record one Work ↔ Companion link, both directions at once.
    ///
    /// One function rather than two writes, because the two entries are one fact. A
    /// projection holding `X → Y` without `Y → CompanionSession X` would answer
    /// "is Y a Companion" with `false`, and the next transform on Y would give it a
    /// Y of its own — the recursion COMPANION-002 forbids.
    ///
    /// Idempotent for the same pair: re-linking X to the same Y is what restart
    /// recovery does, and refusing it would turn recovery into a startup failure.
    let link
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (parentOfMain: SessionId option)
        (current: Map<SessionId, SessionAssociation>)
        : Result<Map<SessionId, SessionAssociation>, AssociationRejection> =
        if mainSessionId = bloggerSessionId then
            Error(AssociationRejection.SelfLink mainSessionId)
        elif isCompanion mainSessionId current then
            Error(AssociationRejection.CompanionWouldRecurse mainSessionId)
        else
            let existingBlogger = tryBloggerOf mainSessionId current
            let existingOwner = tryMainSessionOf bloggerSessionId current

            match existingBlogger, existingOwner with
            | Some existing, _ when existing <> bloggerSessionId ->
                Error(AssociationRejection.AlreadyLinkedToOther(existing, bloggerSessionId))
            | _, Some owner when owner <> mainSessionId ->
                Error(AssociationRejection.CompanionClaimedByOther(owner, bloggerSessionId))
            | _ ->
                let parent =
                    parentOfMain
                    |> Option.orElse (tryFind mainSessionId current |> Option.bind (fun e -> e.ParentSessionId))

                Ok(
                    current
                    |> Map.add mainSessionId (workSession mainSessionId parent (Some bloggerSessionId))
                    |> Map.add
                        bloggerSessionId
                        { SessionId = bloggerSessionId
                          Kind = ManagedSessionKind.CompanionSession mainSessionId
                          // COMPANION-002: a leaf. Structurally `None`, not a value a
                          // caller supplies and could get wrong.
                          BloggerSessionId = None
                          ParentSessionId = Some mainSessionId }
                )

    /// The Companion child was aborted. The Work session keeps its record and loses
    /// its Y, so the next transform creates a fresh one.
    ///
    /// The Companion's own entry is REMOVED rather than kept as a tombstone. Unlike a
    /// handle (EXEC-009), a Companion session id is never re-presented by the model
    /// and never joined, so there is no request that a stale entry would have to
    /// refuse — and leaving one would make `isCompanion` true for a session that no
    /// longer exists.
    let unlink (mainSessionId: SessionId) (current: Map<SessionId, SessionAssociation>) =
        match tryBloggerOf mainSessionId current with
        | None -> current
        | Some blogger ->
            let parent =
                tryFind mainSessionId current |> Option.bind (fun e -> e.ParentSessionId)

            current
            |> Map.remove blogger
            |> Map.add mainSessionId (workSession mainSessionId parent None)

    let describe (rejection: AssociationRejection) =
        match rejection with
        | AssociationRejection.CompanionWouldRecurse companion ->
            sprintf "session %s is a Companion and may not have one (COMPANION-002)" (SessionId.value companion)
        | AssociationRejection.SelfLink session ->
            sprintf "session %s cannot be its own Companion (HOST-008)" (SessionId.value session)
        | AssociationRejection.AlreadyLinkedToOther(existing, proposed) ->
            sprintf
                "work session already has Companion %s; refusing to repoint to %s (COMPANION-002)"
                (SessionId.value existing)
                (SessionId.value proposed)
        | AssociationRejection.CompanionClaimedByOther(owner, proposed) ->
            sprintf
                "Companion %s already belongs to work session %s (HOST-008)"
                (SessionId.value proposed)
                (SessionId.value owner)
