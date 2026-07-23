
import { Union, Record } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { class_type, lambda_type, int64_type, unit_type, union_type, list_type, int32_type, record_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { SquadTaskResult_$reflection } from "../Kernel/Fact.js";
import { Flow$3_$reflection } from "../Kernel/Flow.js";
import { ChildResult_$reflection, ChildSession_$reflection } from "../Session/ChildFlows.js";

export class SquadTask extends Record {
    constructor(TaskId, TargetAgent, Prompt) {
        super();
        this.TaskId = TaskId;
        this.TargetAgent = TargetAgent;
        this.Prompt = Prompt;
    }
}

export function SquadTask_$reflection() {
    return record_type("Wanxiangshu.Next.Wanxiangzhen.SquadTask", [], SquadTask, () => [["TaskId", string_type], ["TargetAgent", string_type], ["Prompt", string_type]]);
}

export class SquadWave extends Record {
    constructor(WaveIndex, Tasks) {
        super();
        this.WaveIndex = (WaveIndex | 0);
        this.Tasks = Tasks;
    }
}

export function SquadWave_$reflection() {
    return record_type("Wanxiangshu.Next.Wanxiangzhen.SquadWave", [], SquadWave, () => [["WaveIndex", int32_type], ["Tasks", list_type(SquadTask_$reflection())]]);
}

export class SquadPlan extends Record {
    constructor(Waves) {
        super();
        this.Waves = Waves;
    }
}

export function SquadPlan_$reflection() {
    return record_type("Wanxiangshu.Next.Wanxiangzhen.SquadPlan", [], SquadPlan, () => [["Waves", list_type(SquadWave_$reflection())]]);
}

export class VerifiedResult extends Record {
    constructor(TaskId, Result) {
        super();
        this.TaskId = TaskId;
        this.Result = Result;
    }
}

export function VerifiedResult_$reflection() {
    return record_type("Wanxiangshu.Next.Wanxiangzhen.VerifiedResult", [], VerifiedResult, () => [["TaskId", string_type], ["Result", SquadTaskResult_$reflection()]]);
}

export class SquadOutcome extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["SquadCompleted", "SquadFailed"];
    }
}

export function SquadOutcome_$reflection() {
    return union_type("Wanxiangshu.Next.Wanxiangzhen.SquadOutcome", [], SquadOutcome, () => [[["summary", string_type]], [["error", string_type]]]);
}

export class SquadScript extends Record {
    constructor(GetProgressStamp, CreateWorktree, StartSlave, Verify, PublishVerified, MergeOrder, FastForward, AcceptWave, Complete, RunParallel) {
        super();
        this.GetProgressStamp = GetProgressStamp;
        this.CreateWorktree = CreateWorktree;
        this.StartSlave = StartSlave;
        this.Verify = Verify;
        this.PublishVerified = PublishVerified;
        this.MergeOrder = MergeOrder;
        this.FastForward = FastForward;
        this.AcceptWave = AcceptWave;
        this.Complete = Complete;
        this.RunParallel = RunParallel;
    }
}

export function SquadScript_$reflection() {
    return record_type("Wanxiangshu.Next.Wanxiangzhen.SquadScript", [], SquadScript, () => [["GetProgressStamp", lambda_type(unit_type, int64_type)], ["CreateWorktree", lambda_type(SquadTask_$reflection(), Flow$3_$reflection(SquadScript_$reflection(), SquadError_$reflection(), class_type("System.IAsyncDisposable")))], ["StartSlave", lambda_type(class_type("System.IAsyncDisposable"), lambda_type(SquadTask_$reflection(), Flow$3_$reflection(SquadScript_$reflection(), SquadError_$reflection(), ChildSession_$reflection())))], ["Verify", lambda_type(ChildResult_$reflection(), Flow$3_$reflection(SquadScript_$reflection(), SquadError_$reflection(), SquadTaskResult_$reflection()))], ["PublishVerified", lambda_type(class_type("System.IAsyncDisposable"), lambda_type(SquadTaskResult_$reflection(), Flow$3_$reflection(SquadScript_$reflection(), SquadError_$reflection(), VerifiedResult_$reflection())))], ["MergeOrder", lambda_type(list_type(VerifiedResult_$reflection()), list_type(VerifiedResult_$reflection()))], ["FastForward", lambda_type(VerifiedResult_$reflection(), Flow$3_$reflection(SquadScript_$reflection(), SquadError_$reflection(), unit_type))], ["AcceptWave", lambda_type(list_type(VerifiedResult_$reflection()), Flow$3_$reflection(SquadScript_$reflection(), SquadError_$reflection(), unit_type))], ["Complete", lambda_type(unit_type, Flow$3_$reflection(SquadScript_$reflection(), SquadError_$reflection(), SquadOutcome_$reflection()))], ["RunParallel", lambda_type(list_type(SquadTask_$reflection()), lambda_type(lambda_type(SquadTask_$reflection(), Flow$3_$reflection(SquadScript_$reflection(), SquadError_$reflection(), VerifiedResult_$reflection())), Flow$3_$reflection(SquadScript_$reflection(), SquadError_$reflection(), list_type(VerifiedResult_$reflection()))))]]);
}

export class SquadError extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["SquadNoProgress", "SquadCancelled", "SquadExecutionError"];
    }
}

export function SquadError_$reflection() {
    return union_type("Wanxiangshu.Next.Wanxiangzhen.SquadError", [], SquadError, () => [[["Item", string_type]], [], [["Item", string_type]]]);
}

