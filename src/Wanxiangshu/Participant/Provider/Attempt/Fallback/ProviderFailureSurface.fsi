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

    val snapshot: handle: JournalHandle -> session: string -> obj
