//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Collections.Immutable;

//using Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.CSharp;
//using Microsoft.CodeAnalysis.Text;
//using Microsoft.CodeAnalysis.CSharp.Syntax;
//using System.Diagnostics;
//using Microsoft.CodeAnalysis.Diagnostics;

//namespace ObjectiveEnumSourceGenerators
//{
//    //[DiagnosticAnalyzer(LanguageNames.CSharp)]
//    class ObjectiveEnumCodeAnalyzer : DiagnosticAnalyzer
//    {
//        //OER = Objective Enum Requirements
//        private static readonly DiagnosticDescriptor PrivateCtorRule =
//            new("OER001", "enum cannot contain public constructor", "enum class '{0}' can contains only private or protected constructors", "OOP Design", DiagnosticSeverity.Error, true);

//        private static readonly DiagnosticDescriptor CtorParamModifierRule =
//            new("OER002", "enum constructor cannot contain param with modifier", "enum class constructor does not support a parameter with a modifier '{0}'", "OOP Design", DiagnosticSeverity.Error, true);

//        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(PrivateCtorRule, CtorParamModifierRule);

//        public override void Initialize(AnalysisContext context)
//        {
//            context.RegisterSyntaxTreeAction(HandleSyntaxTree);
//        }
//        private static void HandleSyntaxTree(SyntaxTreeAnalysisContext context)
//        {
//            //переходим к самой верхней ноде
//            var root = context.Tree.GetRoot();

//            //переходим к пространству имен
//            var spaces = root.ChildNodes().OfType<NamespaceDeclarationSyntax>();
//            foreach (var spaceDecl in spaces)
//            {
//                //собираем все классы в пространстве
//                var classSyntxs = spaceDecl.ChildNodes().OfType<ClassDeclarationSyntax>();
//                foreach (var classDecl in classSyntxs)
//                {
//                    //проверяем содержит ли класс необходимый атрибут
//                    var attribute = classDecl.AttributeLists
//                        .Select(list => list.Attributes.AsEnumerable())
//                        .Aggregate((a, b) => a.Concat(b))
//                        .FirstOrDefault(FindObjectiveEnumAttribute);

//                    if (attribute != null)
//                    {
//                        //находим все конструкторы класса
//                        var ctors = classDecl.ChildNodes().OfType<ConstructorDeclarationSyntax>();
//                        foreach (var ctor in ctors)
//                        {
//                            //проверяем, есть ли у конструктора ключевые слова public или internal
//                            if (ctor.Modifiers.Any(SyntaxKind.PublicKeyword) || ctor.Modifiers.Any(SyntaxKind.InternalKeyword))
//                            {
//                                //сообщаем о первой ошибке
//                                context.ReportDiagnostic(Diagnostic.Create(PrivateCtorRule, ctor.Identifier.GetLocation(), classDecl.Identifier.ToString()));
//                            }

//                            //собираем все параметры конструктора с модификаторами
//                            var paramsWithMod = ctor.ParameterList.Parameters.Where(p => p.Modifiers.Any());
//                            foreach (var param in paramsWithMod)
//                            {
//                                //сообщаем, что модификаторы конструктора не поддерживаются
//                                context.ReportDiagnostic(Diagnostic.Create(CtorParamModifierRule, param.GetLocation(), param.Modifiers.ToString()));
//                            }
//                        }
//                    }
//                }
//            }
//        }
//        private static bool FindObjectiveEnumAttribute(AttributeSyntax attribute)
//        {
//            var name = attribute.Name.ToString();
//            return
//                name == "ObjectiveEnum" ||
//                name == "ObjectiveEnumAttribute" ||
//                name == "System.ObjectiveEnum.ObjectiveEnum" ||
//                name == "System.ObjectiveEnum.ObjectiveEnumAttribute";
//        }
//    }
//}


//потом доделаю, пока и так хорошо