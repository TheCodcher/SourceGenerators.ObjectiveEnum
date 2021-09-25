using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;

namespace ObjectiveEnumSourceGenerators
{
    [Generator]
    public class ObjectiveEnumSlimGen : ISourceGenerator
    {
        //генерация атрибута, которым следует пометить интерфейсы базовой реализации
        const string AttributeDiscription = @"
        using System;
        namespace System.ObjectiveEnum
        {
            [AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
            public class ObjectiveEnumSlimAttribute : Attribute { }
        }";

        ImmutableHashSet<ActualSemanticModel> targetTypes;
        public void Execute(GeneratorExecutionContext context)
        {
            var reciever = context.SyntaxReceiver as SyntaxReciver;
            if (reciever is null) return;

            //закидываем атрибут
            context.AddSource("ObjectiveEnumSlimAttribute.cs", AttributeDiscription);

            //подгружаем сборку с новым атрибутом
            CSharpParseOptions options = (context.Compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;
            Compilation compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(AttributeDiscription, options));

            //находим определение атрибута в сборке
            var attributeSymbol = compilation.GetTypeByMetadataName("System.ObjectiveEnum.ObjectiveEnumSlimAttribute");

            //првоеряем, нужно ли продолжать работать перед длительной опирацией
            context.CancellationToken.ThrowIfCancellationRequested();

            //получаем все подходящие типы из ресивера
            var temptargetTypes = reciever.TargetTypes

                //переходим к их семантической модели
                .Select(x => new ActualSemanticModel(compilation, x))

                //проверяем наличие необходимого атрибута
                .Where(x => x.Semantic.GetAttributes().Any(ad => ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default)))

                //проверяем наличие статического конструктора
                .Where(x => x.StaticCtor != null);

            //выполняем все действия выше
            targetTypes = temptargetTypes.ToImmutableHashSet();


            foreach (var targetType in targetTypes)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                //генерируем код для каждого выбранного типа
                var source = GenerateEnumForType(targetType);
 
                //добавляем сгенерированный код в сборку
                context.AddSource($"{targetType.Semantic.Name}.ObjectiveEnumSlim.cs", source);
            }
        }



        private string GenerateEnumForType(ActualSemanticModel typeSymbol)
        {
            var semanticSymb = typeSymbol.Semantic;

            string typeSymbolDeclarationType = semanticSymb.IsRecord ? "record" : semanticSymb.TypeKind.ToString().ToLower();

            //находим необходимые конструкции
            var prepared = PrepareType(typeSymbol);

            //создаем по шаблону соответствующий тип
            return $@"
            using System;
            using System.Reflection;
            namespace {semanticSymb.ContainingNamespace}
            {{
                partial {typeSymbolDeclarationType} {semanticSymb.Name}
                {{
                    {GenerateFields(semanticSymb.Name, prepared.EnumNames)}

                    private static class Enum
                    {{
                        private static void SetField(string name, params object[] ctorParam)
                        {{
                            var type = typeof({semanticSymb.Name});
                            var value = Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, ctorParam, null);
                            type.GetField(name).SetValue(null, value);
                        }}

                        {GenerateEnumMethods(prepared.EnumNames, prepared.CtorParamStrings)}
                    }}
                }}
            }}";
        }

        private (MethodParamString[] CtorParamStrings, string[] EnumNames) PrepareType(ActualSemanticModel typeSymbol)
        {
            var ctors = typeSymbol.GetMemberSyntax<ConstructorDeclarationSyntax>(m => !m.Modifiers.Any(SyntaxKind.StaticKeyword))
                .Select(c => new MethodParamString(c)).ToArray();

            return (ctors, typeSymbol.GetCtorInvocation());
        }


        private string GenerateFields(string typeName, string[] names)
        {
            const string format = "public static readonly {0} {1};";

            //просто так, хотя стоило бы использовать везде
            var charCount = names.Sum(n => n.Length + typeName.Length + format.Length);
            var builder = new StringBuilder(charCount);

            foreach (var name in names)
            {
                builder.AppendFormat(format, typeName, name);
                builder.AppendLine();
            }

            return builder.ToString();
        }

        //обходим все параметры метода и генерируем соответствующую строку c# кода

        private string GenerateEnumMethod(string name, MethodParamString[] paramStrings)
        {
            const string format = "public static void {0}({1}) => SetField(\"{2}\", {3});";

            var builder = new StringBuilder();
            foreach (var paramString in paramStrings)
            {
                var param = paramString.ToStringOnlyValue();
                string actualParam = string.IsNullOrEmpty(param) ? "null" : param;
                builder.AppendFormat(format, name, paramString.ToString(), name, actualParam);
                builder.AppendLine();
            }
            return builder.ToString();
        }

        private string GenerateEnumMethods(string[] methodNames, MethodParamString[] CtorParamStrings)
        {
            var builder = new StringBuilder();

            foreach (var methname in methodNames)
            {
                var method = GenerateEnumMethod(methname, CtorParamStrings);
                builder.AppendLine(method);
            }

            return builder.ToString();
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            //В режиме дебага можно поставить точку останова и продебажить анализатор кода
#if DEBUG
            //if (!Debugger.IsAttached)
            //{
            //    Debugger.Launch();
            //}
#endif
            context.RegisterForSyntaxNotifications(() => new SyntaxReciver());
            //пока ничего
        }

        class SyntaxReciver : ISyntaxReceiver
        {
            public readonly List<TypeDeclarationSyntax> TargetTypes = new List<TypeDeclarationSyntax>();
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                var node = syntaxNode as TypeDeclarationSyntax;
                if (node != null && node is not InterfaceDeclarationSyntax)
                {
                    if (node.Modifiers.Any(SyntaxKind.PartialKeyword) && node.AttributeLists.Any())
                    {
                        TargetTypes.Add(node);
                    }
                };
            }
        }

        class ActualSemanticModel
        {
            public readonly INamedTypeSymbol Semantic;
            public readonly MemberDeclarationSyntax StaticCtor;
            public readonly TypeDeclarationSyntax Syntax;

            const string CLASS_NAME = "Enum";
            public ActualSemanticModel(Compilation compilation, TypeDeclarationSyntax syntax)
            {
                Syntax = syntax;

                Semantic = compilation.GetSemanticModel(syntax.SyntaxTree).GetDeclaredSymbol(syntax);

                StaticCtor = syntax.Members.Where(m => m is ConstructorDeclarationSyntax).SingleOrDefault(c => c.Modifiers.Any(SyntaxKind.StaticKeyword));
            }

            public string[] GetCtorInvocation()
            {
                //можно использовать для проверки при дебаге
                //var e1 = StaticCtor.ChildNodes().SingleOrDefault(n=>n.IsKind(SyntaxKind.Block));
                //var e2 = e1.ChildNodes().Where(n => n.IsKind(SyntaxKind.ExpressionStatement)).ToArray();
                //var e3 = e2.Select(n => n.ChildNodes().SingleOrDefault(c => c.IsKind(SyntaxKind.InvocationExpression))).ToArray();
                //var e4 = e3.Select(n => n.ChildNodes().SingleOrDefault(c => c.IsKind(SyntaxKind.SimpleMemberAccessExpression))).ToArray();
                //var e5 = e4.Select(n => n as MemberAccessExpressionSyntax).Where(n => n != null).Select(n => (n.Expression.ToString(), n.Name.ToString())).ToArray();

                //получаем содержимое статического конструктора
                var block = StaticCtor.ChildNodes().SingleOrDefault(n => n.IsKind(SyntaxKind.Block));

                var exprs = block

                    //переходим к выражениям
                    .ChildNodes().Where(n => n.IsKind(SyntaxKind.ExpressionStatement))

                    //переходим к выражениям-вызова
                    .Select(n => n.ChildNodes().SingleOrDefault(c => c.IsKind(SyntaxKind.InvocationExpression)))

                    //переходим к названию вызываемых членов
                    .Select(n =>
                    {
                        var nodes = n.ChildNodes();
                        var member = nodes.SingleOrDefault(c => c.IsKind(SyntaxKind.SimpleMemberAccessExpression));
                        if (member == null)
                        {
                            var newNodes = nodes.SingleOrDefault(c => c.IsKind(SyntaxKind.InvocationExpression));
                            return newNodes.ChildNodes().SingleOrDefault(c => c.IsKind(SyntaxKind.SimpleMemberAccessExpression));
                        }
                        else
                        {
                            return member;
                        }
                    })

                    //проверям предыдущий переход + каст
                    .Select(n => n as MemberAccessExpressionSyntax).Where(n => n != null)

                    //проверям необходимый нам вызов
                    .Where(n => n.Expression.ToString() == CLASS_NAME)

                    //возвращаем имя вызываемого члена
                    .Select(n => n.Name.ToString());

                //var local = block

                //    //переходим к выражениям
                //    .ChildNodes().Where(n => n.IsKind(SyntaxKind.ExpressionStatement))

                //    //переходим к выражениям-вызова
                //    .Select(n => n.ChildNodes().SingleOrDefault(c => c.IsKind(SyntaxKind.SimpleMemberAccessExpression)))

                //    //проверям предыдущий переход + каст
                //    .Select(n => n as MemberAccessExpressionSyntax).Where(n => n != null)

                //    //проверям необходимый нам вызов
                //    .Where(n => n.Expression.ToString() == CLASS_NAME)

                //    //возвращаем имя вызываемого члена
                //    .Select(n => n.Name.ToString());

                //return exprs.Concat(local).ToArray();
                return exprs.ToArray();
            }

            public IEnumerable<T> GetMemberSyntax<T>(Func<T, bool> predicate) where T : MemberDeclarationSyntax
            {
                return Syntax.Members.Where(m => m is T).Select(m => m as T).Where(predicate);
            }

        }

        class MethodParamString
        {
            BaseMethodDeclarationSyntax symb;
            public MethodParamString(BaseMethodDeclarationSyntax methodSymbol)
            {
                symb = methodSymbol;
            }

            public override string ToString()
            {
                return symb.ParameterList.Parameters.ToFullString();
            }
            public string ToStringOnlyValue()
            {
                return string.Join(", ", symb.ParameterList.Parameters.Select(p => p.Identifier));
            }
        }
    }
}

