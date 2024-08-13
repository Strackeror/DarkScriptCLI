declare class EVENT {
  ID: number;
  RestBehavior: number;
}

declare const REST: {
  Restart: number;
  End: number;
  Default: number;
};

declare const EVD: {
  Events: {
    Add(EVENT);
  };
};

declare class Scripter {
  static MakeInstruction(_: EVENT, ...arg: any[]);
  static FillSkipPlaceholder(_: EVENT, _1: any);
  static ConvertFloatToIntBytes(float: number): number;
}

declare class $$$_host {
  static newArr(len: number): any[];
}

declare class Console {
  static WriteLine(str: string);
}

declare const MAIN: any;
declare function InitializeEvent(slot: number, id: number, ...args: number[]);

declare enum EventEndType {
  End,
  Restart,
}

declare function Always(): Condition;
declare type Label = number;
