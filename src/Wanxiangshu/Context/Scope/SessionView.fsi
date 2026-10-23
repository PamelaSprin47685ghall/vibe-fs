namespace Wanxiangshu.Context.Scope

open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.HostSession

module SessionView =
    val render: messages: WireMessage list -> string
    val renderWithTable: tableName: string -> messages: WireMessage list -> string
    val renderJs: messages: obj array -> string
    val renderJsWithTable: tableName: string -> messages: obj array -> string
    val blocksJs: messages: obj array -> LlmFacing.DataBlock list
    val blocksJsWithTable: tableName: string -> messages: obj array -> LlmFacing.DataBlock list
    val ofSnapshotMessages: messages: SessionMessage list -> obj array
