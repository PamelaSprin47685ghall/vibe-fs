namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module ProviderFailureSurface =
    val budget: obj
    val providerFailureProjection: obj

    val authorityRootAccepted: value: obj -> obj
    val providerFailureRecorded: value: obj -> obj
    val providerRetryExhausted: value: obj -> obj
    val providerSuccessRecorded: value: obj -> obj
    val envelope: value: obj -> obj
    val fold: values: obj array -> obj
    val providerFailureFactCaseNames: string array

    val acceptHumanRoot:
        handle: JournalHandle -> session: string -> physicalMessage: string -> agent: string -> Task<obj>

    val recordConfirmedFailure:
        handle: JournalHandle -> budget: int -> session: string -> providerRun: string -> reason: string -> Task<obj>

    /// PAR-021: the durable `ProviderRetryAttempt` dispatch fact of one failed
    /// physical attempt — the fact the recovery target settlement consumes.
    val wasLwrRetryAttempt: handle: JournalHandle -> session: string -> physicalMessage: string -> bool

    val snapshot: handle: JournalHandle -> session: string -> obj
