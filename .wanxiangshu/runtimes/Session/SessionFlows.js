
import { SessionError } from "../Kernel/Outcome.js";
import { Flow_run, FlowBuilder$2__Return_1505, FlowBuilder$2__Bind_Z40B88B2D, FlowBuilder$2__While_31AC1067, FlowBuilder$2__Delay_Z73C1716C, FlowBuilder$2_$ctor_Z35F942C6, ProgressGuard$2 } from "../Kernel/Flow.js";
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { Operators_IsNull } from "../fable_modules/fable-library-js.5.6.0/FSharp.Core.js";
import { toString } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";

export const sessionProgress = new ProgressGuard$2((s) => s.GetProgressStamp(), (msg) => (new SessionError(0, [msg])));

export const session = FlowBuilder$2_$ctor_Z35F942C6(sessionProgress);

export function finishTodo(s) {
    const builder$0040 = session;
    return FlowBuilder$2__Delay_Z73C1716C(builder$0040, () => FlowBuilder$2__While_31AC1067(builder$0040, () => s.GetTodo().Unfinished, FlowBuilder$2__Delay_Z73C1716C(builder$0040, () => FlowBuilder$2__Bind_Z40B88B2D(builder$0040, s.ContinueWork(), () => FlowBuilder$2__Return_1505(builder$0040, undefined)))));
}

export function passReview(s) {
    const builder$0040 = session;
    return FlowBuilder$2__Delay_Z73C1716C(builder$0040, () => FlowBuilder$2__Bind_Z40B88B2D(builder$0040, finishTodo(s), () => FlowBuilder$2__While_31AC1067(builder$0040, () => s.GetReview().Required, FlowBuilder$2__Delay_Z73C1716C(builder$0040, () => FlowBuilder$2__Bind_Z40B88B2D(builder$0040, s.RequestReview(), () => FlowBuilder$2__Bind_Z40B88B2D(builder$0040, finishTodo(s), () => FlowBuilder$2__Return_1505(builder$0040, undefined)))))));
}

export function run(s) {
    const builder$0040 = session;
    return FlowBuilder$2__Delay_Z73C1716C(builder$0040, () => FlowBuilder$2__Bind_Z40B88B2D(builder$0040, passReview(s), () => FlowBuilder$2__Bind_Z40B88B2D(builder$0040, s.Finish(), (_arg_1) => FlowBuilder$2__Return_1505(builder$0040, _arg_1))));
}

export function runFlow(s, ct, flow) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => builder$0040.TryWith(builder$0040.Delay(() => builder$0040.ReturnFrom(Flow_run(s, ct, flow))), (_arg) => {
        const ex = _arg;
        const msg = Operators_IsNull(ex) ? "" : toString(ex);
        return (((msg.indexOf("cancel") >= 0) ? true : (msg.indexOf("Cancel") >= 0)) ? true : (msg.indexOf("Operation") >= 0)) ? builder$0040.Return(new FSharpResult$2(1, [new SessionError(1, [])])) : builder$0040.Return(new FSharpResult$2(1, [new SessionError(7, [msg])]));
    })));
}

