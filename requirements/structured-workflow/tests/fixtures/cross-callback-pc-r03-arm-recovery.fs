module CrossCallbackPcR03ArmRecovery

open System.Collections.Generic

type SlotArming =
    | ArmedByAdvance
    | ArmedByRetry

// R03 boundary shape: Map/ref-wrapped recovery arming permit channel
// Written in observePendingContinuation, consumed in XWire transform via TryTakeRecoveryPermit
let recoveryArmingMap = ref (Map.empty<string, Result<SlotArming, string>>)

type Scope() =
    member _.ArmRecovery(sessionId: string, slot: SlotArming) =
        recoveryArmingMap := Map.add sessionId (Ok slot) (!recoveryArmingMap)

    member _.TryTakeRecoveryPermit(sessionId: string) : Result<SlotArming, string> option =
        match Map.tryFind sessionId (!recoveryArmingMap) with
        | Some res ->
            recoveryArmingMap := Map.remove sessionId (!recoveryArmingMap)
            Some res
        | None -> None
