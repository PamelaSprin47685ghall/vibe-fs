namespace Wanxiangshu.Interaction.Authority

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Interaction.Dispatch

module ChildPromptAuthority =
    val ensureForLinkedChild: prompts: IPromptJournal option -> turn: ReconciledTurn -> Task<Result<unit, string>>
