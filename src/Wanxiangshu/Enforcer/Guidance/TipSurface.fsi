namespace Wanxiangshu.Enforcer.Guidance

open System.Threading.Tasks

/// Opaque JS owner for Main tip-guidance delivery. Journal handles and typed
/// facts stay inside this boundary; tests provide only semantic ids and
/// observation payload data as plain objects.
[<RequireQualifiedAccess>]
module TipSurface =
    val createJournal: directory: string -> Task<obj>
    val disposeJournal: journal: obj -> unit

    val appendCompanionLink: journal: obj -> value: obj -> Task<obj>

    val appendObservation: journal: obj -> value: obj -> Task<obj>

    val appendContextReanchored: journal: obj -> value: obj -> Task<obj>

    val resolve: journal: obj -> session: string -> Task<obj>

    val latest: journal: obj -> session: string -> Task<obj>

    val latestNudge: journal: obj -> session: string -> Task<obj>
