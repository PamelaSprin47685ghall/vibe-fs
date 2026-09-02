namespace Wanxiangshu.Interaction.Authority

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Persistence.Journal

module ChildPromptAuthority =
    val ensureForLinkedChild: journal: AgentJournal option -> turn: ReconciledTurn -> Task<Result<unit, string>>
