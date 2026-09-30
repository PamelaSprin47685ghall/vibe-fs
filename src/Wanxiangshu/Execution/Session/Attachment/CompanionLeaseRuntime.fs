namespace Wanxiangshu.Execution.Session.Attachment

open System.Threading.Tasks
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode

/// The observed allocation origin of a Companion child. The core's `Origin` is the
/// single source; this is its Companion-facing name.
type SatelliteOrigin =
    | Created
    | Reused
    | Replacement

/// DSL-class: PhysicalHandle — HOST-014 session lease identity and observed allocation origin; owner CompanionLeaseRuntime, law HOST-014, proof SatelliteSurface.
type SatelliteLease =
    { SessionId: SessionId
      Origin: SatelliteOrigin }

/// DSL-class: PhysicalHandle — HOST-014 injected session lifecycle ports and physical launch coordinates; owner CompanionLeaseRuntime, law HOST-014, proof SatelliteSurface.
type SatelliteSpec =
    { Kind: SatelliteKind
      Agent: string
      Title: string
      Directory: string option
      RestoredSessionId: SessionId option
      Link: SessionId -> SessionId -> string -> Task<Result<unit, string>>
      Close: SessionId -> Task<Result<unit, string>> }

/// HOST-014: the Companion leaf kind's adapter onto the ONE attachment mechanism
/// (`AttachmentLeaseCore` + `AttachmentLeaseRegistry`). It supplies only this kind's
/// parameters — where the family root is, the durable link/close ports, and the
/// exact agent+title child identity — and owns no lifecycle of its own: creation,
/// recovery, replacement, close-then-link ordering, single-flight and release all
/// happen in the core.
///
/// The durable link port stays a caller-supplied `SatelliteSpec` field, so this
/// adapter never needs the journal or the OpenCode layer.
type CompanionLeaseRuntime(sessions: ISessionHostPort, registry: AttachmentLeaseRegistry) =
    let selfKind = AttachmentKind.Companion

    /// HOST-015: physical parent is always the family root; ownership is proven by
    /// the journal link (`RestoredSessionId`), never by Host parentID. Root children
    /// are the flat location, owner children are queried too so satellites created
    /// before flattening stay reusable. A root that IS the owner has no separate
    /// listing.
    let operations (spec: SatelliteSpec) : AttachmentLeaseCore.Operations =
        { AttachmentLeaseCore.Operations.ListChildren =
            fun ownerText ->
                task {
                    // HOST-015: the physical parent is whatever the port reports as
                    // this owner's family root; that value is handed straight back to
                    // the port, never re-derived through a typed round trip.
                    let owner = SessionId.create ownerText
                    let root = sessions.FamilyRootOf owner

                    let! rootChildren = sessions.ListChildren root

                    let! ownerChildren =
                        if root = owner then
                            Task.FromResult(Ok [])
                        else
                            sessions.ListChildren owner

                    match rootChildren, ownerChildren with
                    | Ok rootList, Ok ownerList ->
                        return
                            Ok(
                                (rootList @ ownerList)
                                |> List.distinctBy (fun child -> SessionId.value child.SessionId)
                                |> List.map (fun child ->
                                    { AttachmentLeaseCore.Child.SessionId = SessionId.value child.SessionId
                                      AttachmentLeaseCore.Child.Agent = child.Agent
                                      AttachmentLeaseCore.Child.Title = child.Title })
                            )
                    | Error error, _
                    | _, Error error ->
                        return Error(sprintf "Cannot recover companion satellite: %s" error)
                }
          AttachmentLeaseCore.Operations.CreateChild =
            fun ownerText _agent directory ->
                task {
                    let! created =
                        sessions.CreateChildSession(
                            SessionId.create ownerText,
                            { Title = Some spec.Title
                              Agent = Some spec.Agent
                              Directory = directory }
                        )

                    return created |> Result.map SessionId.value
                }
          AttachmentLeaseCore.Operations.AbortChild =
            fun child -> sessions.AbortSession(SessionId.create child)
          AttachmentLeaseCore.Operations.Link =
            fun owner child agent -> spec.Link (SessionId.create owner) (SessionId.create child) agent
          AttachmentLeaseCore.Operations.Close = fun owner -> spec.Close (SessionId.create owner) }

    /// Exact agent+title identity; a restore hint is the journal-linked id whose
    /// absence authorizes a Replacement (managed-session-lifecycle [011]).
    let specOf (spec: SatelliteSpec) : AttachmentLeaseCore.Spec =
        { Kind = selfKind
          Agent = spec.Agent
          Title = spec.Title
          Directory = spec.Directory
          RestoreHint = spec.RestoredSessionId |> Option.map SessionId.value }

    let originOf (origin: AttachmentLeaseCore.Origin) : SatelliteOrigin =
        match origin with
        | AttachmentLeaseCore.Origin.Created -> SatelliteOrigin.Created
        | AttachmentLeaseCore.Origin.Reused -> SatelliteOrigin.Reused
        | AttachmentLeaseCore.Origin.Replacement -> SatelliteOrigin.Replacement

    /// End one resident lease: abort the child, close the durable association and
    /// drop the binding. Nothing resident only closes the association.
    let endLease (spec: SatelliteSpec) (owner: SessionId) (lease: AttachmentLeaseCore.Lease) =
        task {
            let! aborted = sessions.AbortSession(SessionId.create lease.SessionId)
            let! closed = spec.Close owner
            registry.Remove(owner, selfKind) |> ignore
            registry.FinishEnsure(owner, selfKind)

            match aborted, closed with
            | Ok(), Ok() -> return Ok()
            | Error error, _
            | _, Error error -> return Error error
        }

    member _.Ensure(owner: SessionId, spec: SatelliteSpec) : Task<Result<SatelliteLease, string>> =
        let start () =
            task {
                try
                    return! AttachmentLeaseCore.ensure (operations spec) owner (specOf spec)
                finally
                    // A failed ensure must never become a permanent poisoned flight.
                    registry.FinishEnsure(owner, selfKind)
            }

        task {
            match! registry.Ensure(owner, selfKind, start) with
            | Error error -> return Error error
            | Ok lease ->
                registry.Bind(owner, selfKind, lease)

                return
                    Ok
                        { SessionId = SessionId.create lease.SessionId
                          Origin = originOf lease.Origin }
        }

    /// Only the binding is dropped, so the next material re-observes Host and durable
    /// state instead of trusting a stale cache entry. The child lease itself ends with
    /// the owner's lifetime or with an explicit `Retire`.
    member _.Invalidate(owner: SessionId, kind: SatelliteKind) =
        // The parameter names the caller's SatelliteKind (always Companion today);
        // this adapter owns exactly the Companion AttachmentKind.
        ignore kind
        registry.Remove(owner, selfKind) |> ignore
        registry.FinishEnsure(owner, selfKind)

    /// The owner ended, or the child has to go: end the resident child and close the
    /// durable association. Nothing resident means only the association is closed,
    /// exactly as an owner outside the runtime closes it.
    member _.Retire(owner: SessionId, spec: SatelliteSpec) : Task<Result<unit, string>> =
        task {
            match registry.TryFind(owner, selfKind) with
            | Some lease -> return! endLease spec owner lease
            | None ->
                registry.FinishEnsure(owner, selfKind)
                return! spec.Close owner
        }
