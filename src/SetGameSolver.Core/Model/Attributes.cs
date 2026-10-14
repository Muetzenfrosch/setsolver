namespace SetGameSolver.Core.Model;

/// <summary>Anzahl der Symbole auf einer Karte.</summary>
public enum Number : byte { One = 0, Two = 1, Three = 2 }

/// <summary>Farbe der Symbole.</summary>
public enum Color : byte { Red = 0, Green = 1, Purple = 2 }

/// <summary>Füllung der Symbole.</summary>
public enum Shading : byte { Solid = 0, Striped = 1, Open = 2 }

/// <summary>Form der Symbole.</summary>
public enum Shape : byte { Diamond = 0, Oval = 1, Squiggle = 2 }
