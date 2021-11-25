/* Copyright (C) 2021 Reznicencu Bogdan
*  This program is free software; you can redistribute it and/or modify
*  it under the terms of the GNU General Public License as published by
*  the Free Software Foundation; either version 2 of the License, or
*  (at your option) any later version.
*  
*  This program is distributed in the hope that it will be useful,
*  but WITHOUT ANY WARRANTY; without even the implied warranty of
*  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*  GNU General Public License for more details.
*  
*  You should have received a copy of the GNU General Public License along
*  with this program; if not, write to the Free Software Foundation, Inc.,
*  51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
*/

using IFilterShellView.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using static IFilterShellView.Parser.XPressCommands;

namespace IFilterShellView.Parser
{

    public class XPressParser
    {
        public static readonly CComContext ComContext = new CComContext();

        private const char StateASCIIChar = ':';
        private const char StateCharWhiteSpace = ' ';
        private const char StateCharQuote = '"';
        private const char StateCharOr = '|';
        private const char StateCharAnd = '&';



        private static readonly HashSet<int> FinalStates = new HashSet<int>() { 6, 8, 9, 12 };
        private static readonly IReadOnlyDictionary<Tuple<int, char>, int> TransitionList = new Dictionary<Tuple<int, char>, int>()
        {
            { new Tuple<int, char>(0, StateCharWhiteSpace), 0},
            { new Tuple<int, char>(0, StateASCIIChar), 1},
            { new Tuple<int, char>(1, StateASCIIChar), 1},
            { new Tuple<int, char>(1, StateCharWhiteSpace), 2},

            { new Tuple<int, char>(10, StateCharWhiteSpace), 0},
            { new Tuple<int, char>(9, StateCharOr), 10},
            { new Tuple<int, char>(9, StateCharAnd), 10},
            { new Tuple<int, char>(9, StateCharWhiteSpace), 9},
            { new Tuple<int, char>(0, '('), 11},
            { new Tuple<int, char>(11, ')'), 12},
            { new Tuple<int, char>(12, StateCharWhiteSpace), 9},

            /**/
            { new Tuple<int, char>(4, StateCharWhiteSpace), 4},
            { new Tuple<int, char>(4, StateASCIIChar), 6},
            { new Tuple<int, char>(6, StateASCIIChar), 6},
            { new Tuple<int, char>(4, StateCharQuote), 5},
            { new Tuple<int, char>(5, StateASCIIChar), 7},
            { new Tuple<int, char>(7, StateASCIIChar), 7},
            { new Tuple<int, char>(7, StateCharWhiteSpace), 7},
            { new Tuple<int, char>(5, StateCharQuote), 8},
            { new Tuple<int, char>(7, StateCharQuote), 8},
        };
        
        private readonly string Filter;


        public XPressParser(string FilterParam)
        {
            Filter = FilterParam;
            ComContext.SetToDefaultValues();
        }



        private char MapCharToTransitionChar(char InputCharacter)
        {
            if (InputCharacter == ' ') return StateCharWhiteSpace;
            if (char.IsLetterOrDigit(InputCharacter) || "/\\".Contains(InputCharacter)) return StateASCIIChar;
            if (InputCharacter == '"') return StateCharQuote;

            return InputCharacter;
        }

        private Expression<Func<CPidlData, bool>> LinkPredicateToPredicateChain(
            char OperatorChar,
            Expression<Func<CPidlData, bool>> SubPredicateChain,
            Expression<Func<CPidlData, bool>> PredicateChain
        )
        {
            if (OperatorChar == StateCharAnd)
                return PredicateChain.And(SubPredicateChain);
            else
                return PredicateChain.Or(SubPredicateChain);
        }


        public Expression<Func<CPidlData, bool>> LinkCommandToPredicateChain(
            char OperatorChar,
            CComAndArgs ComAndArgs,
            Expression<Func<CPidlData, bool>> PredicateChain
        )
        {
            if (!ComStrToComIndex.TryGetValue(ComAndArgs.Command, out ComIndex cindex))
                throw new UserException("The command you entered is not registered. Please enter a valid command.");

            if (!ComIndexOptions.TryGetValue(cindex, out int Attributes))
                throw new UserException("There are no attributes assigned to this function.");

            if (Attributes != ComAndArgs.Arguments.Count)
                throw new UserException("Wrong number of parameters given to the specified command.");

            if (!CommandAttributeDict.TryGetValue(cindex, out Func<CPidlData, CComAndArgs, bool> CommandCallback))
                throw new UserException("Registered command has no associated callback.");


            return OperatorChar switch
            {
                StateCharAnd => PredicateChain.And(Pidl => CommandCallback(Pidl, ComAndArgs)),
                StateCharOr => PredicateChain.Or(Pidl => CommandCallback(Pidl, ComAndArgs)),
                _ => throw new UserException("Predicate unifier not implemented."),
            };
        }



        private Expression<Func<CPidlData, bool>> ParseToLinqPredicateList(ref SyntaxIdentities Identities)
        {
            Expression<Func<CPidlData, bool>> PredicateChain = PredicateBuilder.False<CPidlData>();
            char LastPredicateUnifier = StateCharOr;
            int CurrentState = 0;


            CComAndArgs ComAndArgs;
            ComAndArgs.Command = "";
            ComAndArgs.Arguments = new List<string>();


            string Accumulator = "";
            int ArgumentCount = 0;
            int Cursor;

            for (Cursor = 0; Cursor < Filter.Length; Cursor++)
            {
                char CurrentChar = Filter[Cursor];
                char StateChar = MapCharToTransitionChar(CurrentChar);

                Tuple<int, char> NextState = new Tuple<int, char>(CurrentState, StateChar);

                if (TransitionList.TryGetValue(NextState, out int NewState))
                {
                    if (NewState == 1 || NewState == 6 || NewState == 7)
                    {
                        Accumulator += CurrentChar;
                    }

                    if (NewState == 2 && CurrentState == 1)
                    {
                        Identities.Intervals.Add(SyntaxInterval.Get(Cursor - Accumulator.Length, Cursor, IdentityType.CMD));
                        ComAndArgs.Command = Accumulator;
                        Accumulator = "";
                    }

                    if (NewState == 10)
                    {
                        LastPredicateUnifier = CurrentChar;
                    }

                    if (NewState == 11)
                    {
                        int Balance = 0;
                        string Subgroup = "";

                        for (; Cursor < Filter.Length; Cursor++)
                        {
                            if (Filter[Cursor] == '(')
                            {
                                Balance++;
                            }
                            else if (Filter[Cursor] == ')')
                            {
                                Balance--;
                            }

                            Subgroup += Filter[Cursor];

                            if (Balance == 0)
                            {
                                break;
                            }
                        }

                        if (Balance != 0)
                        {
                            throw new UserException("There is no matching paranthesis in the filter string.");
                        }

                        Subgroup = Subgroup[1..^1];

                        XPressParser xpress = new XPressParser(Subgroup);
                        var SubPredicateChain = xpress.ParseToLinqPredicateList(ref Identities);
                        PredicateChain = LinkPredicateToPredicateChain(LastPredicateUnifier, SubPredicateChain, PredicateChain);

                        Cursor--;
                    }

                    CurrentState = NewState;
                }
                else if (CurrentState == 2)
                {
                    try
                    {
                        ComIndex cindex = ComStrToComIndex[ComAndArgs.Command];
                        int Attribute = ComIndexOptions[cindex];
                        ArgumentCount = Attribute;
                        Cursor--;

                        ComAndArgs.Arguments = new List<string>();

                        if (ArgumentCount > 0)
                        {
                            CurrentState = 4;
                        }
                        else
                        {
                            CurrentState = 9;
                            PredicateChain = LinkCommandToPredicateChain(LastPredicateUnifier, ComAndArgs, PredicateChain);
                        }
                    }
                    catch (Exception)
                    {
                        throw new UserException("Given command was not found.");
                    }
                }
                else if (CurrentState == 6 || CurrentState == 8)
                {
                    Identities.Intervals.Add(SyntaxInterval.Get(Cursor - Accumulator.Length, Cursor, IdentityType.ARG));
                    ComAndArgs.Arguments.Add(Accumulator);
                    ArgumentCount--;

                    if (ArgumentCount != 0)
                    {
                        CurrentState = 4;
                    }
                    else
                    {
                        CurrentState = 12;
                        PredicateChain = LinkCommandToPredicateChain(LastPredicateUnifier, ComAndArgs, PredicateChain);
                    }

                    Cursor--;
                    Accumulator = "";
                }
                else throw new UserException("The parser entered an undetermined state.");
            }


            if (!FinalStates.Contains(CurrentState))
                throw new UserException("Reached end of input too early and entered a non final state.");


            if (CurrentState == 6 || CurrentState == 8)
            {
                Identities.Intervals.Add(SyntaxInterval.Get(Cursor - Accumulator.Length, Cursor, IdentityType.ARG));
                ComAndArgs.Arguments.Add(Accumulator);
                PredicateChain = LinkCommandToPredicateChain(LastPredicateUnifier, ComAndArgs, PredicateChain);
            }

            return PredicateChain;
        }




        /// <exception cref="UserException">Thrown when compiling</exception>
        /// <exception cref="Exception">Thrown when compiling</exception>
        public Func<CPidlData, bool> Compile()
        {
            SyntaxIdentities Identities = new SyntaxIdentities();
            Expression<Func<CPidlData, bool>> LanguageSentence = ParseToLinqPredicateList(ref Identities);
            return LanguageSentence.Compile();
        }


        public SyntaxIdentities ParseIdentities()
        {
            SyntaxIdentities Identities = new SyntaxIdentities();
            _ = ParseToLinqPredicateList(ref Identities);
            return Identities;
        }

    }
}
