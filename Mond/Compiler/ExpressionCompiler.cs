﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mond.Compiler.Expressions;
using Mond.Debugger;

namespace Mond.Compiler
{
    class ExpressionCompiler
    {
        private readonly List<FunctionContext> _contexts;
        private int _labelIndex;
        private List<Instruction> _instructions;

        public readonly MondCompilerOptions Options;

        public readonly ConstantPool<double> NumberPool;
        public readonly ConstantPool<string> StringPool;

        public int LambdaId;

        public int ScopeId;
        public int ScopeDepth;

        public ExpressionCompiler(MondCompilerOptions options)
        {
            _contexts = new List<FunctionContext>();
            _labelIndex = 0;

            Options = options;

            NumberPool = new ConstantPool<double>();
            StringPool = new ConstantPool<string>();

            LambdaId = 0;

            ScopeId = 0;
            ScopeDepth = -1;
        }

        public MondProgram Compile(Expression expression, string debugSourceCode = null)
        {
            var context = new FunctionContext(this, 0, 0, null, null, null);
            RegisterFunction(context);

            context.Function(context.FullName);

            context.PushScope();
            context.Enter();
            expression.Compile(context);
            context.LoadUndefined();
            context.Return();
            context.PopScope();

            var length = PatchLabels();
            var bytecode = GenerateBytecode(length);
            var debugInfo = GenerateDebugInfo(expression.Token.FileName, debugSourceCode);

            return new MondProgram(bytecode, NumberPool.Items, StringPool.Items, debugInfo);
        }

        private int PatchLabels()
        {
            var offset = 0;

            foreach (var instruction in AllInstructions())
            {
                instruction.Offset = offset;
                offset += instruction.Length;
            }

            return offset;
        }

        private byte[] GenerateBytecode(int bufferSize)
        {
            var bytecode = new byte[bufferSize];
            var memoryStream = new MemoryStream(bytecode);
            var writer = new BinaryWriter(memoryStream);

            foreach (var instruction in AllInstructions())
            {
                //instruction.Print();
                instruction.Write(writer);
            }

            return bytecode;
        }

        private MondDebugInfo GenerateDebugInfo(string sourceFileName, string sourceCode)
        {
            if (Options.DebugInfo == MondDebugInfoLevel.None)
                return null;

            if (Options.DebugInfo <= MondDebugInfoLevel.StackTrace)
                sourceCode = null;

            var prevName = -1;

            var functions = AllInstructions()
                .Where(i => i.Type == InstructionType.Function)
                .Select(i =>
                {
                    var name = ((ConstantOperand<string>)i.Operands[0]).Id;
                    return new MondDebugInfo.Function(i.Offset, name);
                })
                .Where(f =>
                {
                    if (f.Name == prevName)
                        return false;

                    prevName = f.Name;

                    return true;
                })
                .ToList();

            var prevLineNumber = -1;
            var prevColumnNumber = -1;

            var lines = AllInstructions()
                .Where(i => i.Type == InstructionType.Position)
                .Select(i =>
                {
                    var line = ((ImmediateOperand)i.Operands[0]).Value;
                    var column = ((ImmediateOperand)i.Operands[1]).Value;

                    return new MondDebugInfo.Position(i.Offset, line, column);
                })
                .Where(l =>
                {
                    if (l.LineNumber == prevLineNumber && l.ColumnNumber == prevColumnNumber)
                        return false;

                    prevLineNumber = l.LineNumber;
                    prevColumnNumber = l.ColumnNumber;

                    return true;
                })
                .ToList();

            if (Options.DebugInfo <= MondDebugInfoLevel.StackTrace)
                return new MondDebugInfo(sourceFileName, sourceCode, functions, lines, null, null);

            var prevAddress = -1;

            var statements = AllInstructions()
                .Where(i => i.Type == InstructionType.Statement)
                .Select(s =>
                {
                    var startLine = ((ImmediateOperand)s.Operands[0]).Value;
                    var startColumn = ((ImmediateOperand)s.Operands[1]).Value;
                    var endLine = ((ImmediateOperand)s.Operands[2]).Value;
                    var endColumn = ((ImmediateOperand)s.Operands[3]).Value;

                    return new MondDebugInfo.Statement(s.Offset, startLine, startColumn, endLine, endColumn);
                })
                .Where(s =>
                {
                    if (s.Address == prevAddress)
                        return false;

                    prevAddress = s.Address;
                    return true;
                })
                .ToList();

            var scopes = AllInstructions()
                .Where(i => i.Type == InstructionType.Scope)
                .Select(s =>
                {
                    var id = ((ImmediateOperand)s.Operands[0]).Value;
                    var depth = ((ImmediateOperand)s.Operands[1]).Value;
                    var parentId = ((ImmediateOperand)s.Operands[2]).Value;
                    var start = ((LabelOperand)s.Operands[3]).Position;
                    var end = ((LabelOperand)s.Operands[4]).Position - 1;
                    var identOperands = ((DeferredOperand<ListOperand<DebugIdentifierOperand>>)s.Operands[5]).Value.Operands;

                    if (!start.HasValue || !end.HasValue)
                        throw new Exception("scope labels not bound");

                    var identifiers = identOperands
                        .Select(i => new MondDebugInfo.Identifier(i.Name.Id, i.IsReadOnly, i.FrameIndex, i.Id))
                        .ToList();

                    return new MondDebugInfo.Scope(id, depth, parentId, start.Value, end.Value, identifiers);
                })
                .OrderBy(s => s.Id)
                .ToList();

            return new MondDebugInfo(sourceFileName, sourceCode, functions, lines, statements, scopes);
        }

        private IEnumerable<Instruction> AllInstructions()
        {
            return _instructions ?? (_instructions = _contexts.SelectMany(c => c.Instructions).ToList());
        }

        public void RegisterFunction(FunctionContext context)
        {
            _contexts.Add(context);
        }

        public LabelOperand MakeLabel(string name = null)
        {
            return new LabelOperand(_labelIndex++, name);
        }
    }
}
