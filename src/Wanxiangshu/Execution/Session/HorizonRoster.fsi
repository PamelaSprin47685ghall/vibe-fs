namespace Wanxiangshu.Execution.Session

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Foundation
open Wanxiangshu.Host.Domain
open Wanxiangshu.Participant.Provider

/// horizon(): who remains at the caller's horizon, as prose or as data.
/// Host-neutral; a host reaches it through `DelegationEnvironment`.
module HorizonRoster =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Description: string = "tool/horizon/description"

        [<Literal>]
        val Returned: string = "tool/horizon/returned"

        [<Literal>]
        val StillAway: string = "tool/horizon/still-away"

        [<Literal>]
        val DidNotReturn: string = "tool/horizon/did-not-return"

        [<Literal>]
        val Voided: string = "tool/horizon/voided"

        [<Literal>]
        val RemainsOpen: string = "tool/horizon/remains-open"

        [<Literal>]
        val Someone: string = "tool/horizon/someone"

        [<Literal>]
        val TerminalLabel: string = "tool/horizon/terminal-label"

        [<Literal>]
        val EmptyRoster: string = "tool/horizon/empty-roster"

        [<Literal>]
        val UnavailableFromContext: string = "tool/horizon/unavailable-from-context"

        [<Literal>]
        val CannotBeSeen: string = "tool/horizon/cannot-be-seen"

        [<Literal>]
        val LatestWork: string = "tool/horizon/latest-work"

        [<Literal>]
        val NoWorkYet: string = "tool/horizon/no-work-yet"

        [<Literal>]
        val LatestWorkUnavailable: string = "tool/horizon/latest-work-unavailable"

        [<Literal>]
        val Calibration: string = "tool/horizon/calibration"

    /// One participant on the roster, as data.
    type RosterAgent =
        {
            Label: string
            /// returned | still-away | did-not-return | voided
            Status: string
            Target: string
            /// latest | none
            Work: string
            LatestRecord: string
            TurnsUsed: int
            TurnsLeft: int
            TimeUsedMs: float
            TimeLeftMs: float
        }

    /// The roster as data: participants, then the open terminals' names
    /// (empty when the runtime knows none).
    type Roster =
        { Agents: RosterAgent list
          Terminals: string list }

    /// Whether an office holding this role may call horizon.
    val permits: role: Role -> bool

    /// The roster as instruction lines in the caller's language; a horizon
    /// the caller cannot be shown answers with one line saying so.
    val instructions: host: DelegationEnvironment -> Task<string list>

    /// The same roster as data: every horizon-visible participant with the
    /// status the prose line reports, its work state, its latest record body,
    /// and the four calibration numbers, plus the terminals still open. None
    /// when the caller cannot be shown a horizon. No provider prose beyond the
    /// participant labels is rendered.
    val roster: language: ProviderLanguage -> host: DelegationEnvironment -> Task<Roster option>
