using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ObjectiveEnumSourceGenerators
{
    //необходимый атрибут и интерфейс 
    [Generator]
    public class ObjectiveEnumGen : ISourceGenerator
    {
        //генерация атрибута, которым следует пометить интерфейсы базовой реализации
        const string AttributeDiscription = @"
        using System;
        namespace System.ObjectiveEnum
        {
            [AttributeUsage(AttributeTargets.Class)]
            public class ObjectiveEnumAttribute : Attribute { }
        }";

        //генерация класса с базовой функциональностью объектно-ориентированного перечисления
        //вместо <ReplaceToken> генерируются все типы для которых доступна функциональнось ООП-перечисления
        const string ObjectiveEnumExtentionsDiscription = @"
        using System;
        using System.Reflection;
        using System.Linq;
        using System.Collections.Generic;
        using System.Collections.Immutable;
        namespace System.ObjectiveEnum
        {
            public interface IObjectiveEnum
            {
                string Name { get; }
                int Ordinal { get; }
            }
            public static class ObjectiveEnum
        {
        private static readonly ImmutableDictionary<string, EnumStatement> DeclaratedEnums = new EnumStatement[]
        {
            <ReplaceToken>
        }.ToImmutableDictionary(t => t.Type.FullName);
        private static EnumStatement GetEnum(Type type)
        {
            if (DeclaratedEnums.TryGetValue(type.FullName, out var value))
            {
                return value;
            }
            else
            {
                throw new ArgumentException($""type {type.FullName} is not an enum"");
            }
        }
        public static Array GetValues<T>() where T : class => GetValues(typeof(T));
        public static Array GetValues(Type type) => GetEnum(type).GetValues();
        public static Array GetNames<T>() where T : class => GetNames(typeof(T));
        public static Array GetNames(Type type) => GetEnum(type).GetNames();
        public static T GetValue<T>(int ordinal) where T : class => GetValue(typeof(T), ordinal) as T;
        public static object GetValue(Type type, int ordinal) => GetEnum(type).GetValueByOrdinal(ordinal);
        public static T GetValue<T>(string name) where T : class => GetValue(typeof(T), name) as T;
        public static object GetValue(Type type, string name) => GetEnum(type).GetValueByName(name);
        public static bool IsDefined<T>() => IsDefined(typeof(T));
        public static bool IsDefined(Type type) => DeclaratedEnums.ContainsKey(type.FullName);
        public static bool TryGetValue(Type type, string name, out object value)
        {
            try
            {
                value = GetValue(type, name);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }
        public static bool TryGetValue(Type type, int ordinal, out object value)
        {
            try
            {
                value = GetValue(type, ordinal);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }
        public static bool TryGetValue<T>(string name, out T value) where T : class
        {
            if (TryGetValue(typeof(T), name, out var val))
            {
                value = val as T;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }
        public static bool TryGetValue<T>(int ordinal, out T value) where T : class
        {
            if (TryGetValue(typeof(T), ordinal, out var val))
            {
                value = val as T;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }
        public static bool Exists(Type type, string name)
        {
            return TryGetValue(type, name, out _);
        }
        public static bool Exists(Type type, int ordinal)
        {
            return TryGetValue(type, ordinal, out _);
        }
        public static bool Exists<T>(string name) where T : class => Exists(typeof(T), name);
        public static bool Exists<T>(int ordinal) where T : class => Exists(typeof(T), ordinal);
        public static bool HasFlag(int flags, int flag)
        {
            return (flag & flags) == flag;
        }
        public static T[] GetFlags<T>(int flags) where T : class
        {
            return GetFlagsByEnumerable(typeof(T), flags).OfType<T>().ToArray();
        }
        public static Array GetFlags(Type type, int flags)
        {
            return GetFlagsByEnumerable(type, flags).ToArray();
        }
        private static IEnumerable<IObjectiveEnum> GetFlagsByEnumerable(Type type, int flags)
        {
            return ObjectiveEnum.GetValues(type).OfType<IObjectiveEnum>().Where(e => HasFlag(flags, e.Ordinal));
        }

        class EnumStatement
        {
            public readonly Type Type;
            public readonly Func<string, object> GetValueByName;
            public readonly Func<int, object> GetValueByOrdinal;
            public readonly Func<Array> GetValues;
            public readonly Func<Array> GetNames;

            public EnumStatement(Type type, Func<string, object> getValueByName, Func<int, object> getValueByOrdinal, Func<Array> getValues, Func<Array> getNames)
            {
                Type = type;
                GetValueByName = getValueByName;
                GetValueByOrdinal = getValueByOrdinal;
                GetValues = getValues;
                GetNames = getNames;
            }
        }
    }
}";

        //основной метод, вызываемчй для генерации
        public void Execute(GeneratorExecutionContext context)
        {
            //класс, куда среда передает информацию о синтаскическом дереве
            var reciever = context.SyntaxReceiver as SyntaxReciver;
            if (reciever is null) return;

            //закидываем атрибут в сборку
            context.AddSource("ObjectiveEnumAttribute.cs", AttributeDiscription);

            //подгружаем сборку с новым атрибутом
            CSharpParseOptions options = (context.Compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;
            Compilation compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(AttributeDiscription, options));

            //находим определение атрибута в сборке по метаданным
            var attributeSymbol = compilation.GetTypeByMetadataName("System.ObjectiveEnum.ObjectiveEnumAttribute");

            //првоеряем, нужно ли продолжать работать перед длительной опирацией
            //context.CancellationToken.ThrowIfCancellationRequested();

            //получаем все подходящие типы из ресивера
            var temptargetTypes = reciever.TargetTypes

                //переходим к их семантической модели
                .Select(x => new ActualSemanticModel(compilation, x))

                //проверяем наличие необходимого атрибута
                .Where(x => x.Semantic.GetAttributes().Any(ad => ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default)))

                //проверяем наличие статического конструктора
                .Where(x => x.StaticCtor != null);

            //выполняем все действия выше
            var targetTypes = temptargetTypes.ToImmutableHashSet();

            //создаем список зарегистрированных типов
            var refBuilder = new List<string>();

            //генерируем для каждого типа дополнение
            foreach (var targetType in targetTypes)
            {
                //context.CancellationToken.ThrowIfCancellationRequested();
                //невозможно предусмотреть все вариации, так как часть работы идет с симантикой, а часть с синтаксисом
                //поэтому, чтобы избежать поломки всего генератора, каждая отдельная генерация типа обернута в try/catch
                try
                {
                    //генерируем код для каждого выбранного типа
                    var source = GenerateEnumForType(targetType);

                    //если генерация произошла успешна, то регистрируем тип
                    refBuilder.Add(RegisterType($"{targetType.Semantic.ContainingNamespace}.{targetType.Semantic.Name}"));

                    //добавляем сгенерированный код в сборку
                    context.AddSource($"{targetType.Semantic.Name}.ObjectiveEnum.cs", source);
                }
                catch(Exception e)
                {

                }
            }

            //объединяем информацию обо всех зарегистрированных типов, чтобы записать их в словарь
            var registredSet = string.Join($",{Environment.NewLine}", refBuilder);

            //добавляем класс с базовой функциональностью в сборку
            context.AddSource("ObjectiveEnum.cs", ObjectiveEnumExtentionsDiscription.Replace("<ReplaceToken>", registredSet));
        }

        //регистрирует тип, создавая необходимый код для класса с базовой функциональностью
        string RegisterType(string typeName)
        {
            return $"new EnumStatement(typeof({typeName}), {typeName}.GetByName, {typeName}.GetByOrdinal, () => {typeName}.Values, () => {typeName}.Names)";
        }

        //генерирует дополнение для класса, которое и позволяет называть его ООП-перечислением
        private string GenerateEnumForType(ActualSemanticModel typeSymbol)
        {
            var semanticSymb = typeSymbol.Semantic;

            //название типа без пространсва имен, для которого в данный момент идет генерация
            var typeName = typeSymbol.Semantic.Name;

            //определение типа для генерации struct,class,record. Однако для генерации в силу других ограничений доступны только классы
            string typeSymbolDeclarationType = semanticSymb.IsRecord ? "record" : semanticSymb.TypeKind.ToString().ToLower();

            //находим необходимые конструкции
            var prepared = PrepareType(typeSymbol);

            //создаем по шаблону соответствующий тип
            return $@"
            using System;
            using System.Reflection;
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using System.Linq;
            using System.ObjectiveEnum;
            namespace {semanticSymb.ContainingNamespace}
            {{
                sealed partial {typeSymbolDeclarationType} {typeName} : IObjectiveEnum
                {{
                    {GenSystemEnumFlow(typeName, prepared.EnumNames)}
                    {GenerateFields(typeName, prepared.EnumNames)}
                    {GenTypeFlow(typeName)}
                    string IObjectiveEnum.Name => Name;
                    int IObjectiveEnum.Ordinal => Ordinal;

                    {GenTypeEnumMethodsFlow(typeName, prepared.EnumNames)}
                    {GenToStringMeth(typeSymbol.Semantic)}

                    private static class Enum
                    {{
                        static Action<int> SetField(string name, int ord, {typeName} item)
                        {{
                            var type = typeof({typeName});
                            type.GetField(name).SetValue(null, item);
                            type.GetField(""Ordinal"").SetValue(item, ord);
                            type.GetField(""Name"").SetValue(item, name);
                            return i => {{ }};
                        }}

                        {GenerateEnumMethods(prepared.EnumNames, typeName, prepared.CtorParamStrings)}
                    }}
                }}
            }}";
        }

        //генерируем функциональности для класса, для его использования как перечисления
        private string GenTypeFlow(string typeName)
        {
            return $@"
            public readonly int Ordinal;
            public readonly string Name;

            public override int GetHashCode()
            {{
                return Ordinal;
            }}
            public override bool Equals(object obj)
            {{
                return (obj is {typeName} val) ? val.GetHashCode() == GetHashCode() : false;
            }}
            public static bool TryGetByName(string name, out {typeName} value)
            {{
                try
                {{
                    value = GetByName(name);
                    return true;
                }}
                catch
                {{
                    value = default({typeName});
                    return false;
                }}
            }}

            public static explicit operator {typeName}(int ordinal) => GetByOrdinal(ordinal);
            public static implicit operator int({typeName} value) => value.Ordinal;
            public static int operator |({typeName} value1, {typeName} value2) => value1.Ordinal | value2.Ordinal;
            public static int operator &({typeName} value1, {typeName} value2) => value1.Ordinal & value2.Ordinal;
            public static int operator |({typeName} value1, int value2) => value1.Ordinal | value2;
            public static int operator &({typeName} value1, int value2) => value1.Ordinal & value2;
            public static bool operator ==({typeName} value1, {typeName} value2) => value1.GetHashCode() == value2.GetHashCode();
            public static bool operator !=({typeName} value1, {typeName} value2) => !(value1 == value2);
            ";
        }

        //генерируем 4 базовых метода для перечисления
        private string GenTypeEnumMethodsFlow(string typeName, EnumItemDescription[] enumNames)
        {
            return $@"
            {GenGetNames(enumNames)}
            {GenGetValues(typeName, enumNames)}
            {GenByNameSwitch(typeName, enumNames)}
            {GenByOrdinalSwitch(typeName, enumNames)}
            ";
        }

        //генерируем статический метод получения объекта по его имени. switch/case используется как наиоблее быстрый способ,
        //полностью готовый на уровне компиляции, что выделяет его на фоне способа со словарями
        private string GenByNameSwitch(string typeName, EnumItemDescription[] enumNames)
        {
            const string caseForm = "case nameof({0}): return {1}.{2};";

            var builder = new StringBuilder();

            foreach (var currentname in enumNames)
            {
                builder.AppendLine(string.Format(caseForm, currentname.Name, typeName, currentname.Name));
            }

            return $@"            
            public static {typeName} GetByName(string name)
            {{
                switch (name)
                {{
                    {builder}
                    default: throw new ArgumentException($""{{name}} not exists in enum"");
                }}
            }}";
        }

        //генерирует системное перечисление внутри ООП для использования в неадаптированных библиотечных методах
        private string GenSystemEnumFlow(string typeName, EnumItemDescription[] enumNames)
        {
            return $@"
            public enum {typeName}SystemEnum : int
            {{
                {string.Join($",{Environment.NewLine}", enumNames.Select(n => $"{n.Name} = {n.Ordinal}"))}
            }}

            public static explicit operator {typeName}({typeName}SystemEnum systemEnumKind) => GetByOrdinal((int)systemEnumKind);
            public static implicit operator {typeName}SystemEnum({typeName} value) => ({typeName}SystemEnum)value.Ordinal;
            ";
        }

        //аналогичный предыдущей генерации - генерация метода по поиску объекта по его номеру
        private string GenByOrdinalSwitch(string typeName, EnumItemDescription[] enumNames)
        {
            const string caseForm = "case ({0}): return {1}.{2};";

            var builder = new StringBuilder();

            foreach (var current in enumNames)
            {
                builder.AppendLine(string.Format(caseForm, current.Ordinal, typeName, current.Name));
            }

            return $@"            
            public static {typeName} GetByOrdinal(int ordinal)
            {{
                switch (ordinal)
                {{
                    {builder}
                    default: throw new ArgumentException($""{{ordinal}} not exists in enum"");
                }}
            }}";
        }
        //генерирует свойство возврашающее массив имен перечисления
        private string GenGetNames(EnumItemDescription[] enumNames)
        {
            return $@"
            public static string[] Names => new string[]
            {{
                {string.Join($",{Environment.NewLine}", enumNames.Select(n => $"\"{n.Name}\""))}
            }};
            ";
        }
        //генерирует свойство возврашающее массив объектов перечисления
        private string GenGetValues(string typeName, EnumItemDescription[] enumNames)
        {
            return $@"
            public static {typeName}[] Values => new {typeName}[]
            {{
                {string.Join($",{Environment.NewLine}", enumNames.Select(n => $"{typeName}.{n.Name}"))}
            }};
            ";
        }
        //генерирует метод ToString возвращающий иимя объекта перечисления, 
        //если метод не был пререопределен до этого пользователем
        private string GenToStringMeth(INamedTypeSymbol typeSymbol)
        {
            //выесняется, был ли переопределен метод польователем
            var hasMethod = typeSymbol.MemberNames.Contains("ToString");
            if (hasMethod)
            {
                return "";
            }
            else
            {
                //генерируем метод ToString
                return $@"
                public override string ToString()
                {{
                    return Name;
                }}";
            }
        }

        //из модели класса получается информацию о методах в статическом конструкторе
        //по названиям которых и будут сгенерированы объекты перечисления
        //а также извлекается информация о параметрах конструктора
        private (MethodParamString[] CtorParamStrings, EnumItemDescription[] EnumNames) PrepareType(ActualSemanticModel typeSymbol)
        {
            var ctors = typeSymbol.GetMemberSyntax<ConstructorDeclarationSyntax>(m => !m.Modifiers.Any(SyntaxKind.StaticKeyword))
                .Select(c => new MethodParamString(c)).ToArray();

            return (ctors, typeSymbol.GetCtorInvocation());
        }

        //генерация статических полей которые и предсталяют перечисление
        private string GenerateFields(string typeName, EnumItemDescription[] names)
        {
            const string format = "public static readonly {0} {1};";

            //просто так, хотя стоило бы использовать везде (оптимизация, хоть и спорная)
            var charCount = names.Sum(n => n.Name.Length + typeName.Length + format.Length);
            var builder = new StringBuilder(charCount);

            foreach (var name in names)
            {
                builder.AppendFormat(format, typeName, name.Name);
                builder.AppendLine();
            }

            return builder.ToString();
        }

        //обходим все параметры метода и генерируем соответствующую строку c# кода
        //возвращение Action<int> позволяет сделать изящную синтаксическую конструкцию для определения номера объекта перечисления
        private string GenerateEnumMethod(string name, string ordinal, string typeName, MethodParamString[] paramStrings)
        {
            const string format = "public static Action<int> {0}({1}) => SetField(\"{2}\", {3}, new {4}({5}));";

            //если в массиве нет объектов, значит используется конструктор по умолчанию без параметров
            if (paramStrings is null || paramStrings.Length == 0)
                return string.Format(format, name, "", name, ordinal, typeName, "");

            var builder = new StringBuilder();

            foreach (var paramString in paramStrings)
            {
                //получаем текстовое представления параметров метода с ключевыми словами out, ref.
                var param = paramString.ToStringValuesAndKeywords();
                builder.AppendFormat(format, name, paramString.ToString()/*получаем полное текстовое представлние параметров*/ , name, ordinal, typeName, param);
                builder.AppendLine();
            }

            return builder.ToString();
        }

        //генерируем методы, которые создают объекты перечисления
        private string GenerateEnumMethods(EnumItemDescription[] methodNames, string typeName, MethodParamString[] CtorParamStrings)
        {
            var builder = new StringBuilder();

            foreach (var methname in methodNames)
            {
                var method = GenerateEnumMethod(methname.Name, methname.Ordinal, typeName, CtorParamStrings);
                builder.AppendLine(method);
            }

            return builder.ToString();
        }

        //метод, который вызывается при первом запуске генератора ресурсов
        public void Initialize(GeneratorInitializationContext context)
        {
            //В режиме дебага можно поставить точку останова и продебажить анализатор кода
#if DEBUG
            //if (!Debugger.IsAttached)
            //{
            //    Debugger.Launch();
            //}
#endif
            //регистрируем ресивер, которые планируем использовать для получения синтаксического дерева
            context.RegisterForSyntaxNotifications(() => new SyntaxReciver());
        }

        class SyntaxReciver : ISyntaxReceiver
        {
            //сохраняем узлы дерева для дальнейшего использования
            public readonly List<TypeDeclarationSyntax> TargetTypes = new List<TypeDeclarationSyntax>();

            //метод вызывающийся каждый раз, когда среда собирает информацию о синтаксическом дереве, передавая сюдакаждую проходящую ноду
            //нода может содержать как описание класса, переменной, вызова метода, пространства имен и т.д.
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                //нас интересуют только ноды декларирующие тип и не интерфейсы
                var node = syntaxNode as TypeDeclarationSyntax;
                if (node is not null && node is not InterfaceDeclarationSyntax)
                {
                    //проверяем есть ли у типа хоть один атрибут
                    //Проверка на сам отрибут проводится исходя из семантической модели
                    if (node.Modifiers.Any(SyntaxKind.PartialKeyword) && node.AttributeLists.Any())
                    {
                        //добавляем тип, как потенциальный, для которого будет вызвана генерация ООП-перечисления
                        TargetTypes.Add(node);
                    }
                };
            }
        }

        //вспомогательный класс, содержащию логику обработки синтаксической и семантической моделей для одной ноды
        class ActualSemanticModel
        {
            //содержит семантическую модель
            public readonly INamedTypeSymbol Semantic;

            //содержит описание обязательного статического конструктора
            public readonly MemberDeclarationSyntax StaticCtor;

            //содержит синтаксическую модель
            public readonly TypeDeclarationSyntax Syntax;

            //название внутреннего приватного класса, в котором будут содержаться методы создания перечисления
            //для того, чтобы не засарять IntelliSense
            const string CLASS_NAME = "Enum";

            public ActualSemanticModel(Compilation compilation, TypeDeclarationSyntax syntax)
            {
                Syntax = syntax;

                Semantic = compilation.GetSemanticModel(syntax.SyntaxTree).GetDeclaredSymbol(syntax);

                StaticCtor = syntax.Members.Where(m => m is ConstructorDeclarationSyntax).SingleOrDefault(c => c.Modifiers.Any(SyntaxKind.StaticKeyword));
            }

            public EnumItemDescription[] GetCtorInvocation()
            {
                //получаем содержимое статического конструктора
                var block = StaticCtor.ChildNodes().SingleOrDefault(n => n.IsKind(SyntaxKind.Block));

                //определяем ordinal каждого следующего объекта будущего перечисления
                int addedOrdDelta = -1;
                string lastOrdValue = "0";

                //прочесываем дерево выражений внутри статического конструктора для получения необходимой для генерации информации
                var exprs = block

                    //переходим к выражениям
                    .ChildNodes().Where(n => n.IsKind(SyntaxKind.ExpressionStatement))

                    //переходим к выражениям-вызова
                    .Select(n => n.ChildNodes().SingleOrDefault(c => c.IsKind(SyntaxKind.InvocationExpression)))

                    //переходим к названию вызываемых членов
                    //порефакторить бы
                    /*
                     * Ниже представлена фильтрация трех видов нод
                     * Пример: 
                     * 1. SomeMethod();
                     * 2. Enum.OurEnumObjectName(<ctor params>);
                     * 3. Enum.OurEnumObjectName(<ctor params>)(<int ordinal>);
                     * 
                     * В первом случае нет вызова Enum - а значит данный метод не будет использоваться в генерации
                     * 
                     * Во втором случае необходимо извлеч имя метода, так как оно будет использовано как название объекта перечисления
                     * и поставить следующий свободный поядковый номер как номер объекта 
                     * 
                     * В третьем случае необходимо сделать все тоже что и во втором, но установить указанный номер
                     * и записать этот номер как базовый для послудющего отсчета номеров для нод 2-ого случая.
                     */
                    .Select(n =>
                    {
                        var nodes = n.ChildNodes();

                        //member не null, если нода 2-ого случая
                        var member = nodes.SingleOrDefault(c => c.IsKind(SyntaxKind.SimpleMemberAccessExpression));

                        if (member is null)
                        {
                            var newNodes = nodes.SingleOrDefault(c => c.IsKind(SyntaxKind.InvocationExpression));

                            //newNodes - null - если нода 1-ого случая, а значит не подходит для генерации вовсе
                            if (newNodes is null) return (null, null);

                            var findnode = newNodes.ChildNodes().SingleOrDefault(c => c.IsKind(SyntaxKind.SimpleMemberAccessExpression)) as MemberAccessExpressionSyntax;

                            //findnode не null - если нода 3-ого случая
                            if (findnode is not null && findnode.Expression.ToString() == CLASS_NAME)
                            {
                                lastOrdValue = (n as InvocationExpressionSyntax).ArgumentList.ToString();
                                addedOrdDelta = 0;
                                return (Node: findnode, Ord: lastOrdValue);
                            }
                            else
                            {
                                return (null, null);
                            }
                        }
                        else
                        {
                            var findnode = member as MemberAccessExpressionSyntax;
                            if (findnode is not null && findnode.Expression.ToString() == CLASS_NAME)
                            {
                                addedOrdDelta++;
                                return (Node: findnode, Ord: $"{lastOrdValue} + {addedOrdDelta}");
                            }
                            else
                            {
                                return (null, null);
                            }
                        }
                    })

                    //фильтруем предыдущий переход
                    .Where(n => n.Node is not null)

                    //возвращаем номер и именя члена будущего перечисления
                    .Select(n => new EnumItemDescription(n.Node.Name.ToString(), n.Ord));

                return exprs.ToArray();
            }

            public IEnumerable<T> GetMemberSyntax<T>(Func<T,bool> predicate) where T : MemberDeclarationSyntax
            {
                return Syntax.Members.OfType<T>().Where(predicate);
            }

        }

        //класс содержащий и описывающий логику синтаксической модели параметров метода
        class MethodParamString
        {
            BaseMethodDeclarationSyntax symb;
            public MethodParamString(BaseMethodDeclarationSyntax methodSymbol)
            {
                symb = methodSymbol;
            }

            public override string ToString()
            {
                //возвращает текстовое представление параметров в первозданном виде
                return symb?.ParameterList.Parameters.ToFullString() ?? "";
            }
            public string ToStringOnlyValue()
            {
                //возвращает текстовое представление параметров без ключевых слов. только название переменных
                return symb is null ? "" : string.Join(", ", symb.ParameterList.Parameters.Select(p => p.Identifier));
            }
            public string ToStringValuesAndKeywords()
            {
                //возвращает текстовое представление параметров с ключевыми словами, но без, напрмиер, базовых значений
                return symb is null ? "" : string.Join(", ", symb.ParameterList.Parameters.Select(p => $"{p.Modifiers} {p.Identifier}"));
            }
        }

        //хранит информацию об одном объекте перечисления
        class EnumItemDescription
        {
            public string Name;
            public string Ordinal;
            public EnumItemDescription(string name, string ordinal)
            {
                Name = name;
                Ordinal = ordinal;
            }
            public override string ToString()
            {
                return Name;
            }
        }
    }
}

