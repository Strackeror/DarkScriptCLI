// C# Shared stuff
declare class EVENT {
  ID: number;
  RestBehavior: number;
  Instructions: {
    Count: number;
  };
}

declare const REST: {
  Restart: number;
  End: number;
  Default: number;
};

declare const EVD: {
  Events: {
    Add(event: EVENT): void;
  };
};

declare class Scripter {
  static MakeInstruction(_: EVENT, ...arg: any[]): unknown;
  static FillSkipPlaceholder(event: EVENT, index: number): unknown;
  static ConvertFloatToIntBytes(float: number): number;
}

declare class $$$_host {
  static newArr(len: number): any[];
}

declare class Console {
  static WriteLine(str: string): void;
}

declare type ConditionGroup = number;
declare type Label = number;
declare type IfFunc = (cond: ConditionGroup, ...args: any[]) => void;
declare type SkipFunc = (skip: number, ...args: any[]) => void;
declare type Endfunc = (end: EventEndType, ...args: any[]) => void;
declare type GotoFunc = (label: Label, ...args: any[]) => void;
declare type Wait = (...args: any[]) => void;

// Common event stuff
declare const MAIN: any;
declare function InitializeEvent(slot: number, id: number, ...args: number[]): void;
declare enum EventEndType {
  End,
  Restart,
}
declare function Always(): Condition;
