
import { SquadError } from "./SquadTypes.js";
import { FlowBuilder$2__For_Z203E708, FlowBuilder$2__Combine_Z1BEA2E47, FlowBuilder$2__ReturnFrom_Z3BB19842, Flow_run, FlowBuilder$2__Return_1505, Flow_create, FlowBuilder$2__Using_Z25CD278, FlowBuilder$2__Bind_Z40B88B2D, FlowBuilder$2__Delay_Z73C1716C, FlowBuilder$2_$ctor_Z35F942C6, ProgressGuard$2 } from "../Kernel/Flow.js";
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { ChildScript, ChildFlows_child } from "../Session/ChildFlows.js";
import { printf, toText } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";

export const squadProgress = new ProgressGuard$2((s) => s.GetProgressStamp(), (msg) => (new SquadError(0, [msg])));

export const squad = FlowBuilder$2_$ctor_Z35F942C6(squadProgress);

export function prepareTask(z, taskInfo) {
    const builder$0040 = squad;
    return FlowBuilder$2__Delay_Z73C1716C(builder$0040, () => FlowBuilder$2__Bind_Z40B88B2D(builder$0040, z.CreateWorktree(taskInfo), (_arg) => FlowBuilder$2__Using_Z25CD278(builder$0040, _arg, (_arg_1) => {
        const worktree_1 = _arg_1;
        return FlowBuilder$2__Bind_Z40B88B2D(builder$0040, z.StartSlave(worktree_1, taskInfo), (_arg_2) => FlowBuilder$2__Using_Z25CD278(builder$0040, _arg_2, (_arg_3) => {
            const slave_1 = _arg_3;
            return FlowBuilder$2__Bind_Z40B88B2D(builder$0040, Flow_create((_arg_4, ct) => {
                const builder$0040_1 = task();
                return builder$0040_1.Run(builder$0040_1.Delay(() => {
                    const dummyChildScript = new ChildScript((_arg_5) => FlowBuilder$2__Delay_Z73C1716C(ChildFlows_child, () => FlowBuilder$2__Return_1505(ChildFlows_child, slave_1)));
                    return builder$0040_1.Bind(Flow_run(dummyChildScript, ct, slave_1.Run(taskInfo.Prompt)), (_arg_6) => {
                        const r = _arg_6;
                        return (r.tag === 1) ? builder$0040_1.Return(new FSharpResult$2(1, [new SquadError(2, [toText(printf("%A"))(r.fields[0])])])) : builder$0040_1.Return(new FSharpResult$2(0, [r.fields[0]]));
                    });
                }));
            }), (_arg_7) => FlowBuilder$2__Bind_Z40B88B2D(builder$0040, z.Verify(_arg_7), (_arg_8) => FlowBuilder$2__ReturnFrom_Z3BB19842(builder$0040, z.PublishVerified(worktree_1, _arg_8))));
        }));
    })));
}

export function runSquad(z, plan) {
    const builder$0040 = squad;
    return FlowBuilder$2__Delay_Z73C1716C(builder$0040, () => FlowBuilder$2__Combine_Z1BEA2E47(builder$0040, FlowBuilder$2__For_Z203E708(builder$0040, plan.Waves, (_arg) => FlowBuilder$2__Bind_Z40B88B2D(builder$0040, z.RunParallel(_arg.Tasks, (taskInfo) => prepareTask(z, taskInfo)), (_arg_1) => {
        const verified = _arg_1;
        return FlowBuilder$2__Combine_Z1BEA2E47(builder$0040, FlowBuilder$2__For_Z203E708(builder$0040, z.MergeOrder(verified), (_arg_2) => FlowBuilder$2__Bind_Z40B88B2D(builder$0040, z.FastForward(_arg_2), () => FlowBuilder$2__Return_1505(builder$0040, undefined))), FlowBuilder$2__Delay_Z73C1716C(builder$0040, () => FlowBuilder$2__Bind_Z40B88B2D(builder$0040, z.AcceptWave(verified), () => FlowBuilder$2__Return_1505(builder$0040, undefined))));
    })), FlowBuilder$2__Delay_Z73C1716C(builder$0040, () => FlowBuilder$2__ReturnFrom_Z3BB19842(builder$0040, z.Complete()))));
}

