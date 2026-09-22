namespace Wanxiangshu.Sphinx

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

module Inquiry =
    type Progress =
        | Working of state: EpistemicState * request: Request
        | Complete of CanonicalAnswer
    type RootBudget =
        { Expectation: TurnExpectation
          UsedTurns: int
          ChargedWork: Set<string>
          Frontier: EventId }
    type BudgetBinding =
        | Historical
        | OwnBudget of RootBudget
        | SharedBudget of root: string
    type BudgetReport =
        { Root: string
          Expectation: TurnExpectation
          UsedTurns: int }
    type Entry =
        { Revision: int
          Head: EventId
          Progress: Progress
          Budget: BudgetBinding
          CompletionBudget: BudgetReport option }
    type Current =
        { Entries: Map<string, Entry>
          Calibration: Map<string * int, TurnCalibration> }

    val currentKey: string
    val question: entry: Entry -> string
    val rootBudget: current: Current -> invocationId: string -> (string * RootBudget) option
    val budgetIsOpen: current: Current -> invocationId: string -> bool
    val budgetReport: current: Current -> invocationId: string -> BudgetReport option
    val workId: invocationId: string -> state: EpistemicState -> string
    val startData: current: Current -> question: string -> expected: int option -> root: string option -> Result<obj, string>
    val envelope: invocationId: string -> previous: Entry option -> kind: string -> data: obj -> EventEnvelope
    val transition: current: Current -> invocationId: string -> kind: string -> data: obj -> EventEnvelope
    val apply: current: Current -> event: EventEnvelope -> Result<Current, string>
    val rule: IntegrationRule
