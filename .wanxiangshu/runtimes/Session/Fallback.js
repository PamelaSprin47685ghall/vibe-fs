
import { Flow_fail, FlowBuilder$2__ReturnFrom_Z3BB19842, FlowBuilder$2__Bind_Z40B88B2D, FlowBuilder$2__Return_1505, FlowBuilder$2__Delay_Z73C1716C } from "../Kernel/Flow.js";
import { session } from "./SessionFlows.js";
import { SessionError } from "../Kernel/Outcome.js";
import { tail, head, isEmpty } from "../fable_modules/fable-library-js.5.6.0/List.js";

export function tryAttempts(s, sendContinue, model, attempt) {
    return FlowBuilder$2__Delay_Z73C1716C(session, () => ((attempt > s.Config.MaxRetriesPerModel) ? FlowBuilder$2__Return_1505(session, undefined) : FlowBuilder$2__Bind_Z40B88B2D(session, sendContinue(model, attempt), (_arg) => {
        const outcome = _arg;
        return (outcome.tag === 1) ? FlowBuilder$2__ReturnFrom_Z3BB19842(session, tryAttempts(s, sendContinue, model, attempt + 1)) : ((outcome.tag === 3) ? FlowBuilder$2__ReturnFrom_Z3BB19842(session, Flow_fail(new SessionError(7, [outcome.fields[0]]))) : ((outcome.tag === 2) ? FlowBuilder$2__ReturnFrom_Z3BB19842(session, Flow_fail(new SessionError(4, []))) : FlowBuilder$2__Return_1505(session, outcome)));
    })));
}

export function tryModels(s, sendContinue, models) {
    return FlowBuilder$2__Delay_Z73C1716C(session, () => (!isEmpty(models) ? FlowBuilder$2__Bind_Z40B88B2D(session, tryAttempts(s, sendContinue, head(models), 1), (_arg) => {
        const resultOpt = _arg;
        return (resultOpt == null) ? FlowBuilder$2__ReturnFrom_Z3BB19842(session, tryModels(s, sendContinue, tail(models))) : FlowBuilder$2__Return_1505(session, resultOpt);
    }) : FlowBuilder$2__ReturnFrom_Z3BB19842(session, Flow_fail(new SessionError(2, [])))));
}

export function continueWork(s, sendContinue) {
    return FlowBuilder$2__Delay_Z73C1716C(session, () => FlowBuilder$2__Bind_Z40B88B2D(session, tryModels(s, sendContinue, s.Config.FallbackModels), (_arg) => FlowBuilder$2__Bind_Z40B88B2D(session, s.CommitTodoFrom(_arg), () => FlowBuilder$2__Return_1505(session, undefined))));
}

