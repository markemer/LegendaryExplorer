﻿using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class ForEachLoop : Statement
    {
        public Expression IteratorCall;
        public CodeBody Body;

        public ForEachLoop(Expression iterator, CodeBody body, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.ForEachLoop, start, end)
        {
            IteratorCall = iterator;
            Body = body;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return IteratorCall;
                yield return Body;
            }
        }
    }
}
