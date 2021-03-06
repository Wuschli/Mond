﻿using System.Collections.Generic;
using Mond.Compiler.Expressions;
using Mond.Compiler.Expressions.Statements;
using Mond.Compiler.Parselets.Statements;

namespace Mond.Compiler.Parselets
{
    class GroupParselet : IPrefixParselet
    {
        public Expression Parse(Parser parser, Token token)
        {
            if ((parser.Match(TokenType.RightParen) && parser.Match(TokenType.Pointy, 1)) ||                                            // () ->
                (parser.Match(TokenType.Identifier) && parser.Match(TokenType.RightParen, 1) && parser.Match(TokenType.Pointy, 2)) ||   // (ident) ->
                (parser.Match(TokenType.Identifier) && parser.Match(TokenType.Comma, 1)) ||                                             // (ident,
                 parser.Match(TokenType.Ellipsis))                                                                                      // (...
            {
                return ParseLambdaExpression(parser, token);
            }

            if (parser.Match(TokenSubType.Operator) && parser.Match(TokenType.RightParen, 1))
            {
                var operatorToken = parser.Take();
                parser.Take(); // right paren

                var name = Lexer.GetOperatorIdentifier(operatorToken.Contents);
                var ident = new Token(operatorToken, TokenType.Identifier, name);

                return new IdentifierExpression(ident);
            }

            var expression = parser.ParseExpression();
            parser.Take(TokenType.RightParen);
            return expression;
        }

        private static Expression ParseLambdaExpression(Parser parser, Token token)
        {
            var arguments = new List<string>();
            string otherArgs = null;

            // parse argument list
            if (!parser.Match(TokenType.RightParen))
            {
                while (true)
                {
                    if (parser.MatchAndTake(TokenType.Ellipsis))
                    {
                        otherArgs = parser.Take(TokenType.Identifier).Contents;
                        break;
                    }

                    var identifier = parser.Take(TokenType.Identifier);
                    arguments.Add(identifier.Contents);

                    if (parser.Match(TokenType.RightParen))
                        break;

                    parser.Take(TokenType.Comma);
                }
            }

            parser.Take(TokenType.RightParen);
            parser.Take(TokenType.Pointy);

            var body = FunctionParselet.ParseLambdaExpressionBody(parser, token);
            return new FunctionExpression(token, null, arguments, otherArgs, body);
        }
    }
}
