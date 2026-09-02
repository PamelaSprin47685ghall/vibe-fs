namespace Wanxiangshu.Interaction.Dispatch

open System.Threading.Tasks

/// JoinGuard-owned JS boundary. Reservation state is an opaque capability;
/// HostJoinGuard remains the production decision owner.
[<RequireQualifiedAccess>]
module JoinGuardSurface =
    val newReservations: unit -> obj

    /// Run the real JoinGuard reservation/send path and expose only its decision.
    val nudge:
        port: obj ->
        handle: obj ->
        reservations: obj ->
        session: string ->
        terminalProviderRun: string ->
        directory: obj ->
            Task<obj>
