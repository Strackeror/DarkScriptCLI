declare type Behavior = number;
declare const Restart: Behavior;
declare const End: Behavior;
declare const Default: Behavior;

declare type Body = (...any: any[]) => void;
declare type ONOFF = typeof ON | typeof OFF;

declare function Event(id: number, behavior: Behavior, func: Body);
declare function $Event(id: number, behavior: Behavior, func: Body);
declare function EndEvent();
declare function RestartEvent();
declare function RestartIf(cond: Condition);
declare function EndIf(cond: Condition);
declare function Goto(label: Label);
declare function GotoIf(label: Label, cond: Condition);
declare function Skip(count: number);
declare function SkipIf(count: number, cond: Condition);
declare function SkipTo(label: string);
declare function SkipToIf(label: string, cond: Condition);
declare function WaitFor(cond: Condition);
declare function NoOp();
declare function NamedLabel(str: string);

declare const Else: undefined;
declare const mainGroupAbuse: Condition;

type Alternating<A, B> = [A, B, A?, B?, A?, B?, A?, B?, A?, B?];
type IfArgs = Alternating<Condition, () => void>;

declare function If(...args: IfArgs);

declare class EventC {
  constructor(id: number, behavior: Behavior, func: Body);
  Initialize(slot: number, ...args: number[]);
}

declare class Condition {
  get Passed(): Condition;
  And(cond: Condition): Condition;
  Or(cond: Condition): Condition;
  Not(): Condition;
  Get(): Condition;

  static New(): Condition;
}

declare function Not(cond: Condition): Condition;
declare function Get(cond: Condition): Condition;

declare class Comparable {
  Eq(comp: Comparable | number): Condition;
  NEq(comp: Comparable | number): Condition;
  Gt(comp: Comparable | number): Condition;
  GtE(comp: Comparable | number): Condition;
  Lt(comp: Comparable | number): Condition;
  LtE(comp: Comparable | number): Condition;
}

declare interface Number {
  Eq(comp: Comparable | number): Condition;
  NEq(comp: Comparable | number): Condition;
  Gt(comp: Comparable | number): Condition;
  GtE(comp: Comparable | number): Condition;
  Lt(comp: Comparable | number): Condition;
  LtE(comp: Comparable | number): Condition;
}

declare const L0: Label;
declare const L1: Label;
declare const L2: Label;
declare const L3: Label;
declare const L4: Label;
declare const L5: Label;
declare const L6: Label;
declare const L7: Label;
declare const L8: Label;
declare const L9: Label;
declare const L10: Label;
declare const L11: Label;
declare const L12: Label;
declare const L13: Label;
declare const L14: Label;
declare const L15: Label;
declare const L16: Label;
declare const L17: Label;
declare const L18: Label;
declare const L19: Label;
declare const L20: Label;
