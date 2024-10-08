﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace GdTool {
    public class GdScriptCompiler {
        private static readonly List<ICompilerParsable> CompilerMetatokens = new List<ICompilerParsable>() {
            new BasicCompilerToken(null, " "), // Space
            new BasicCompilerToken(null, "\t"), // Tab
            new CommentCompilerToken()
        };

        private static readonly List<ICompilerParsable> CompilerTokens = new List<ICompilerParsable>() {
            new NewlineCompilerToken(),
            new KeywordCompilerToken(GdcTokenType.CfIf, "if"),
            new KeywordCompilerToken(GdcTokenType.CfElif, "elif"),
            new KeywordCompilerToken(GdcTokenType.CfElse, "else"),
            new KeywordCompilerToken(GdcTokenType.CfFor, "for"),
            new KeywordCompilerToken(GdcTokenType.CfWhile, "while"),
            new KeywordCompilerToken(GdcTokenType.CfBreak, "break"),
            new KeywordCompilerToken(GdcTokenType.CfContinue, "continue"),
            new KeywordCompilerToken(GdcTokenType.CfPass, "pass"),
            new KeywordCompilerToken(GdcTokenType.CfReturn, "return"),
            new KeywordCompilerToken(GdcTokenType.CfMatch, "match"),
            new KeywordCompilerToken(GdcTokenType.PrFunction, "func"),
            new KeywordCompilerToken(GdcTokenType.PrClassName, "class_name"),
            new KeywordCompilerToken(GdcTokenType.PrClass, "class"),
            new KeywordCompilerToken(GdcTokenType.PrExtends, "extends"),
            new KeywordCompilerToken(GdcTokenType.PrOnready, "onready"),
            new KeywordCompilerToken(GdcTokenType.PrTool, "tool"),
            new KeywordCompilerToken(GdcTokenType.PrStatic, "static"),
            new KeywordCompilerToken(GdcTokenType.PrExport, "export"),
            new KeywordCompilerToken(GdcTokenType.PrSetget, "setget"),
            new KeywordCompilerToken(GdcTokenType.PrConst, "const"),
            new KeywordCompilerToken(GdcTokenType.PrVar, "var"),
            new KeywordCompilerToken(GdcTokenType.PrVoid, "void"),
            new KeywordCompilerToken(GdcTokenType.PrEnum, "enum"),
            new KeywordCompilerToken(GdcTokenType.PrPreload, "preload"),
            new KeywordCompilerToken(GdcTokenType.PrAssert, "assert"),
            new KeywordCompilerToken(GdcTokenType.PrYield, "yield"),
            new KeywordCompilerToken(GdcTokenType.PrSignal, "signal"),
            new KeywordCompilerToken(GdcTokenType.PrBreakpoint, "breakpoint"),
            new KeywordCompilerToken(GdcTokenType.PrRemotesync, "remotesync"),
            new KeywordCompilerToken(GdcTokenType.PrMastersync, "mastersync"),
            new KeywordCompilerToken(GdcTokenType.PrPuppetsync, "puppetsync"),
            new KeywordCompilerToken(GdcTokenType.PrRemote, "remote"),
            new KeywordCompilerToken(GdcTokenType.PrSync, "sync"),
            new KeywordCompilerToken(GdcTokenType.PrMaster, "master"),
            new KeywordCompilerToken(GdcTokenType.PrSlave, "slave"),
            new KeywordCompilerToken(GdcTokenType.PrPuppet, "puppet"),
            new KeywordCompilerToken(GdcTokenType.PrAs, "as"),
            new KeywordCompilerToken(GdcTokenType.PrIs, "is"),
            new KeywordCompilerToken(GdcTokenType.Self, "self"),
            new KeywordCompilerToken(GdcTokenType.OpIn, "in"),
            new WildcardCompilerToken(),
            new BasicCompilerToken(GdcTokenType.Comma, ","),
            new BasicCompilerToken(GdcTokenType.Semicolon, ";"),
            new BasicCompilerToken(GdcTokenType.Period, "."),
            new BasicCompilerToken(GdcTokenType.QuestionMark, "?"),
            new BasicCompilerToken(GdcTokenType.Colon, ":"),
            new BasicCompilerToken(GdcTokenType.Dollar, "$"),
            new BasicCompilerToken(GdcTokenType.ForwardArrow, "->"),
            new BasicCompilerToken(GdcTokenType.OpAssignAdd, "+="),
            new BasicCompilerToken(GdcTokenType.OpAssignSub, "-="),
            new BasicCompilerToken(GdcTokenType.OpAssignMul, "*="),
            new BasicCompilerToken(GdcTokenType.OpAssignDiv, "/="),
            new BasicCompilerToken(GdcTokenType.OpAssignMod, "+="),
            new BasicCompilerToken(GdcTokenType.OpAssignShiftLeft, "<<="),
            new BasicCompilerToken(GdcTokenType.OpAssignShiftRight, ">>="),
            new BasicCompilerToken(GdcTokenType.OpShiftLeft, "<<"),
            new BasicCompilerToken(GdcTokenType.OpShiftRight, ">>"),
            new BasicCompilerToken(GdcTokenType.OpAssignBitAnd, "&="),
            new BasicCompilerToken(GdcTokenType.OpAssignBitOr, "|="),
            new BasicCompilerToken(GdcTokenType.OpAssignBitXor, "^="),
            new BasicCompilerToken(GdcTokenType.OpEqual, "=="),
            new BasicCompilerToken(GdcTokenType.OpNotEqual, "!="),
            new BasicCompilerToken(GdcTokenType.OpLessEqual, "<="),
            new BasicCompilerToken(GdcTokenType.OpLess, "<"),
            new BasicCompilerToken(GdcTokenType.OpGreaterEqual, ">="),
            new BasicCompilerToken(GdcTokenType.OpGreater, ">"),
            new KeywordCompilerToken(GdcTokenType.OpAnd, "and"),
            new KeywordCompilerToken(GdcTokenType.OpOr, "or"),
            new KeywordCompilerToken(GdcTokenType.OpNot, "not"),
            new BasicCompilerToken(GdcTokenType.OpAdd, "+"),
            new BasicCompilerToken(GdcTokenType.OpSub, "-"),
            new BasicCompilerToken(GdcTokenType.OpMul, "*"),
            new BasicCompilerToken(GdcTokenType.OpDiv, "/"),
            new BasicCompilerToken(GdcTokenType.OpMod, "%"),
            new BasicCompilerToken(GdcTokenType.OpBitAnd, "&"),
            new BasicCompilerToken(GdcTokenType.OpBitOr, "|"),
            new BasicCompilerToken(GdcTokenType.OpBitXor, "^"),
            new BasicCompilerToken(GdcTokenType.OpBitInvert, "!"),
            new BasicCompilerToken(GdcTokenType.OpAssign, "="),
            new BasicCompilerToken(GdcTokenType.BracketOpen, "["),
            new BasicCompilerToken(GdcTokenType.BracketClose, "]"),
            new BasicCompilerToken(GdcTokenType.CurlyBracketOpen, "{"),
            new BasicCompilerToken(GdcTokenType.CurlyBracketClose, "}"),
            new BasicCompilerToken(GdcTokenType.ParenthesisOpen, "("),
            new BasicCompilerToken(GdcTokenType.ParenthesisClose, ")"),
            new KeywordCompilerToken(GdcTokenType.ConstPi, "PI"),
            new KeywordCompilerToken(GdcTokenType.ConstTau, "TAU"),
            new KeywordCompilerToken(GdcTokenType.ConstInf, "INF"),
            new KeywordCompilerToken(GdcTokenType.ConstNan, "NAN"),
            new ConstantCompilerToken(),
            new BuiltInTypeCompilerToken(),
            new BuiltInFuncCompilerToken(),
            new IdentifierCompilerToken(),
        };

        static GdScriptCompiler() {
            for (int i = CompilerMetatokens.Count - 1; i >= 0; i--) {
                CompilerTokens.Insert(0, CompilerMetatokens[i]);
            }
        }

        public static byte[] Compile(string source, BytecodeProvider provider) {
            SourceCodeReader reader = new SourceCodeReader(source.Trim());
            List<CompilerTokenData> tokens = new List<CompilerTokenData>();
            while (reader.HasRemaining) {
                bool foundToken = false;
                foreach (ICompilerParsable token in CompilerTokens) {
                    try {
                        CompilerTokenData data = token.Parse(reader, provider);
                        if (data != null) {
                            tokens.Add(data);
                            foundToken = true;
                            break;
                        }
                    } catch (Exception e) {
                        throw new Exception("Error on line " + reader.CurrentRow, e);
                    }
                }
                if (!foundToken) {
                    throw new InvalidOperationException("Unexpected token on line " + reader.CurrentRow);
                }
            }

            tokens = tokens.Where(token => !CompilerMetatokens.Contains(token.Creator)).ToList();
            tokens.Add(new CompilerTokenData(new NewlineCompilerToken()));
            tokens.Add(new CompilerTokenData(new BasicCompilerToken(GdcTokenType.Eof, null)));
            tokens.Add(new CompilerTokenData(new BasicCompilerToken(GdcTokenType.Empty, null)));

            List<string> identifiers = new List<string>();
            List<IGdStructure> constants = new List<IGdStructure>();
            List<uint> linesList = new List<uint> {
                0
            };
            for (uint i = 0; i < tokens.Count; i++) {
                CompilerTokenData data = tokens[(int)i];
                if (data.Creator is IdentifierCompilerToken) {
                    GdcIdentifier ident = (GdcIdentifier)data.Operand;
                    int index = identifiers.IndexOf(ident.Identifier);
                    if (index == -1) {
                        data.Data = (uint)identifiers.Count;
                        identifiers.Add(ident.Identifier);
                    } else {
                        data.Data = (uint)index;
                    }
                } else if (data.Creator is ConstantCompilerToken) {
                    int index = constants.IndexOf(data.Operand);
                    if (index == -1) {
                        data.Data = (uint)constants.Count;
                        constants.Add(data.Operand);
                    } else {
                        data.Data = (uint)index;
                    }
                } else if (data.Creator is NewlineCompilerToken) {
                    linesList.Add(i);
                }
            }

            using (MemoryStream ms = new MemoryStream()) {
                using (BinaryWriter buf = new BinaryWriter(ms)) {
                    buf.Write(Encoding.ASCII.GetBytes("GDSC")); // magic header
                    buf.Write(provider.ProviderData.BytecodeVersion); // version
                    buf.Write((uint)identifiers.Count); // identifiers count
                    buf.Write((uint)constants.Count); // constants count
                    buf.Write((uint)linesList.Count); // line count
                    buf.Write((uint)tokens.Count); // tokens count

                    // write identifiers
                    foreach (string ident in identifiers) {
                        byte[] encoded = Encoding.UTF8.GetBytes(ident);

                        long lengthPos = ms.Position;
                        buf.Write(0U);
                        foreach (byte val in encoded) {
                            buf.Write((byte)(val ^ 0xB6));
                        }

                        uint padding = 0;
                        if (ms.Position % 4 == 0) { // add four bytes of null termination
                            for (int i = 0; i < 4; i++) {
                                buf.Write((byte)(0x00 ^ 0xB6)); // padding null bytes
                                padding++;
                            }
                        }
                        while (ms.Position % 4 != 0) {
                            buf.Write((byte)(0x00 ^ 0xB6)); // padding null bytes
                            padding++;
                        }
                        long currentPos = ms.Position;
                        ms.Position = lengthPos;
                        buf.Write((uint)(encoded.Length + padding));
                        ms.Position = currentPos;
                    }

                    // write constants
                    foreach (IGdStructure structure in constants) {
                        structure.Serialize(buf, provider);
                    }

                    // write lines
                    for (uint i = 0; i < linesList.Count; i++) {
                        buf.Write(linesList[(int)i]);
                        buf.Write(i + 1);
                    }

                    // write tokens
                    foreach (CompilerTokenData token in tokens) {
                        ICompilerToken creator = (ICompilerToken)token.Creator;
                        creator.Write(buf, provider, token);
                    }

                    return ms.ToArray();
                }
            }
        }
    }
}
