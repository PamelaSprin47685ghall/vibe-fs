namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module CursorSurface =
    val cursor: obj
    val fallbackProjection: obj

    val authorityRootAccepted: value: obj -> obj
    val fallbackCursorAdvanced: value: obj -> obj
    val fallbackExhausted: value: obj -> obj
    val fallbackSucceeded: value: obj -> obj
    val envelope: value: obj -> obj
    val fold: values: obj array -> obj
    val fallbackFactCaseNames: string array

    val acceptHumanRoot:
        handle: JournalHandle -> session: string -> physicalMessage: string -> agent: string -> Task<obj>

    val recordConfirmedFailure:
        handle: JournalHandle -> budget: int -> session: string -> providerRun: string -> reason: string -> Task<obj>

    val snapshot: handle: JournalHandle -> session: string -> obj
