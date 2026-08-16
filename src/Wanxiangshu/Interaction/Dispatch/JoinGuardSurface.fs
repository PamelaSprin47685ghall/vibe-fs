namespace Wanxiangshu.Interaction.Dispatch

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// JoinGuard-owned JS boundary. Reservation state is an opaque capability;
/// HostJoinGuard remains the production decision owner.
[<RequireQualifiedAccess>]
module JoinGuardSurface =

    type private ReservationCapability() =
        let keys = HashSet<string>()

        member _.Keys = keys

    let newReservations () : obj = ReservationCapability() :> obj

    let private outcomeView outcome : obj =
        match outcome with
        | HostJoinGuard.JoinGuardNudgeOutcome.Sent promptKey ->
            box {| outcome = "Sent"; promptKey = PromptKey.value promptKey; reason = null |}
        | HostJoinGuard.JoinGuardNudgeOutcome.AlreadyOutstanding ->
            box {| outcome = "AlreadyOutstanding"; promptKey = null; reason = null |}
        | HostJoinGuard.JoinGuardNudgeOutcome.Failed reason ->
            box {| outcome = "Failed"; promptKey = null; reason = reason |}

    /// Run the real JoinGuard reservation/send path and expose only its decision.
    let nudge
        (port: obj)
        (handle: obj)
        (reservations: obj)
        (session: string)
        (directory: obj)
        : Task<obj> =
        task {
            let journal =
                if isNull handle then
                    None
                else
                    Some((unbox<JournalHandle> handle).Journal)

            let keys = (unbox<ReservationCapability> reservations).Keys
            let! outcome =
                HostJoinGuard.nudge
                    (DispatchSurface.sessionPort port)
                    journal
                    keys
                    (SessionId.create session)
                    (if isNull directory then None else Some(string directory))

            return outcomeView outcome
        }
