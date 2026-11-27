using System;
using System.Collections.Generic;
using System.Linq;
using Kentavr.SheetsToSO.Core;
using NUnit.Framework;

// Stand-in for Unity's attribute: CodeSchema matches it by full type name.
namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute
    {
    }
}

namespace Kentavr.SheetsToSO.Core.Tests
{
#pragma warning disable CS0169, CS0414, CS0649 // Fields are read through reflection only.
    public enum Rarity { Common, Epic }

    public enum Grade { Low, High }

    public class UnitRow
    {
        public string id;
        public int hp;
        public float speed;
        public Rarity rarity;
        public string[] tags;
    }

    public class TypedRow
    {
        public string id;
        public int hp;
        public Grade rarity;
        public string weapon;
        public string[] weapons;
        public List<string> tags;
        public List<int> levels;
        public bool[] flags;
    }

    public class CaseRow
    {
        public string id;
        public int Hp;
    }

    public class UnsupportedRow
    {
        public string id;
        public double damage;
        public long gold;
        public int[][] grid;
        public UnitRow nested;
        public List<double> weights;
    }

    public class VisibilityRow
    {
        public string id;
        private int hidden;
        [UnityEngine.SerializeField] private int serialized;
        [UnityEngine.SerializeField] protected int guarded;
        public static int shared;
        public readonly int fixedValue = 1;
        public const int Constant = 1;
        [NonSerialized] public int skipped;
    }

    public class BaseRow
    {
        [UnityEngine.SerializeField] private string id;
        public int level;
    }

    public class DerivedRow : BaseRow
    {
        public int hp;
    }
#pragma warning restore CS0169, CS0414, CS0649

    public class CodeSchemaTests
    {
        private static CodeSchemaResult Resolve(Type rowType, params string[] header)
        {
            var parsed = HeaderParser.Parse(header);
            Assert.That(parsed.Errors, Is.Empty, string.Join("; ", parsed.Errors));
            return CodeSchema.Resolve(rowType, parsed.Columns);
        }

        private static CodeSchemaResult ResolveValid(Type rowType, params string[] header)
        {
            var result = Resolve(rowType, header);
            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            return result;
        }

        private static HeaderError SingleError(CodeSchemaResult result)
        {
            Assert.That(result.Errors, Has.Count.EqualTo(1), string.Join("; ", result.Errors));
            return result.Errors[0];
        }

        private static string[] Types(CodeSchemaResult result) => result.Columns.Select(c => c.Type.ToString()).ToArray();

        [Test]
        public void TakesTypesFromFields()
        {
            var result = ResolveValid(typeof(UnitRow), "id", "hp", "speed", "rarity", "tags");

            Assert.That(result.Columns.Select(c => c.Name), Is.EqualTo(new[] { "id", "hp", "speed", "rarity", "tags" }));
            Assert.That(Types(result), Is.EqualTo(new[] { "string", "int", "float", "enum<Rarity>", "string[]" }));
            Assert.That(result.Columns.Select(c => c.ColumnIndex), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
            Assert.That(result.Columns.Select(c => c.Address), Is.EqualTo(new[] { "A1", "B1", "C1", "D1", "E1" }));
            Assert.That(result.Columns.All(c => !c.IsTypeExplicit && !c.IsRequired), Is.True);
        }

        [Test]
        public void KeepsColumnPositionWithSkippedColumns()
        {
            var result = ResolveValid(typeof(UnitRow), "id", "#notes", "hp");

            Assert.That(result.Columns.Select(c => c.Name), Is.EqualTo(new[] { "id", "hp" }));
            Assert.That(result.Columns[1].ColumnIndex, Is.EqualTo(2));
            Assert.That(result.Columns[1].Address, Is.EqualTo("C1"));
        }

        [Test]
        public void MatchingHeaderTypeIsAccepted()
        {
            var result = ResolveValid(typeof(UnitRow), "id:string!", "hp:int", "rarity:enum<Rarity>", "tags:string[]");

            Assert.That(Types(result), Is.EqualTo(new[] { "string", "int", "enum<Rarity>", "string[]" }));
            Assert.That(result.Columns[0].IsRequired, Is.True);
            Assert.That(result.Columns[0].IsTypeExplicit, Is.True);
        }

        [Test]
        public void DifferentHeaderTypeIsMismatch()
        {
            var result = Resolve(typeof(UnitRow), "id", "hp:float");

            var error = SingleError(result);
            Assert.That(error.Code, Is.EqualTo(HeaderErrorCode.TypeMismatch));
            Assert.That(error.Address, Is.EqualTo("B1"));
            Assert.That(error.Message, Is.EqualTo("Column 'hp' has type 'float' in header, but field 'UnitRow.hp' is 'int'"));
            Assert.That(result.Columns.Select(c => c.Name), Is.EqualTo(new[] { "id" }));
        }

        [Test]
        public void ArrayAndScalarAreMismatch()
        {
            var result = Resolve(typeof(UnitRow), "id", "tags:string");

            var error = SingleError(result);
            Assert.That(error.Code, Is.EqualTo(HeaderErrorCode.TypeMismatch));
            Assert.That(error.Message, Does.Contain("'string'").And.Contain("'string[]'"));
        }

        [Test]
        public void DifferentEnumNameIsMismatch()
        {
            var result = Resolve(typeof(TypedRow), "id", "rarity:enum<Rarity>");

            var error = SingleError(result);
            Assert.That(error.Code, Is.EqualTo(HeaderErrorCode.TypeMismatch));
            Assert.That(error.Address, Is.EqualTo("B1"));
            Assert.That(error.Message,
                Is.EqualTo("Column 'rarity' has type 'enum<Rarity>' in header, but field 'TypedRow.rarity' is 'enum<Grade>'"));
        }

        [Test]
        public void RefIsCompatibleWithStringField()
        {
            var result = ResolveValid(typeof(TypedRow), "id", "weapon:ref<Weapons>", "weapons:ref<Weapons>[]", "tags:ref<Tags>[]");

            Assert.That(Types(result), Is.EqualTo(new[] { "string", "ref<Weapons>", "ref<Weapons>[]", "ref<Tags>[]" }));
        }

        [Test]
        public void RefArrayIsNotCompatibleWithScalarString()
        {
            var result = Resolve(typeof(TypedRow), "id", "weapon:ref<Weapons>[]");

            Assert.That(SingleError(result).Code, Is.EqualTo(HeaderErrorCode.TypeMismatch));
        }

        [Test]
        public void RefIsNotCompatibleWithIntField()
        {
            var result = Resolve(typeof(TypedRow), "id", "hp:ref<Units>");

            Assert.That(SingleError(result).Code, Is.EqualTo(HeaderErrorCode.TypeMismatch));
        }

        [Test]
        public void ListIsArray()
        {
            var result = ResolveValid(typeof(TypedRow), "id", "tags:string[]", "levels", "flags");

            Assert.That(Types(result), Is.EqualTo(new[] { "string", "string[]", "int[]", "bool[]" }));
        }

        [Test]
        public void ColumnWithoutFieldIsUnknownField()
        {
            var result = Resolve(typeof(UnitRow), "id", "hp", "armor");

            var error = SingleError(result);
            Assert.That(error.Code, Is.EqualTo(HeaderErrorCode.UnknownField));
            Assert.That(error.Address, Is.EqualTo("C1"));
            Assert.That(error.Message, Is.EqualTo("Column 'armor' has no matching field in 'UnitRow'"));
            Assert.That(result.Columns.Select(c => c.Name), Is.EqualTo(new[] { "id", "hp" }));
        }

        [Test]
        public void NamesAreCaseSensitiveWithHint()
        {
            var result = Resolve(typeof(CaseRow), "id", "hp");

            var error = SingleError(result);
            Assert.That(error.Code, Is.EqualTo(HeaderErrorCode.UnknownField));
            Assert.That(error.Address, Is.EqualTo("B1"));
            Assert.That(error.Message, Does.Contain("did you mean 'Hp'?"));
        }

        [TestCase("damage", "Double")]
        [TestCase("gold", "Int64")]
        [TestCase("grid", "Int32[][]")]
        [TestCase("nested", "UnitRow")]
        [TestCase("weights", "List<Double>")]
        public void UnsupportedFieldType(string column, string typeName)
        {
            var result = Resolve(typeof(UnsupportedRow), "id", column);

            var error = SingleError(result);
            Assert.That(error.Code, Is.EqualTo(HeaderErrorCode.UnsupportedFieldType));
            Assert.That(error.Address, Is.EqualTo("B1"));
            Assert.That(error.Message, Does.Contain("UnsupportedRow." + column));
            Assert.That(error.Message, Does.Contain("'" + typeName + "'"));
            Assert.That(result.Columns.Select(c => c.Name), Is.EqualTo(new[] { "id" }));
        }

        [Test]
        public void PrivateFieldNeedsSerializeField()
        {
            var result = ResolveValid(typeof(VisibilityRow), "id", "serialized", "guarded");
            Assert.That(Types(result), Is.EqualTo(new[] { "string", "int", "int" }));

            Assert.That(SingleError(Resolve(typeof(VisibilityRow), "id", "hidden")).Code,
                Is.EqualTo(HeaderErrorCode.UnknownField));
        }

        [TestCase("shared")]
        [TestCase("fixedValue")]
        [TestCase("Constant")]
        [TestCase("skipped")]
        public void NonSerializedFieldsAreIgnored(string column)
        {
            var result = Resolve(typeof(VisibilityRow), "id", column);

            Assert.That(SingleError(result).Code, Is.EqualTo(HeaderErrorCode.UnknownField));
        }

        [Test]
        public void BaseClassFieldsAreFound()
        {
            var result = ResolveValid(typeof(DerivedRow), "id", "level", "hp");

            Assert.That(Types(result), Is.EqualTo(new[] { "string", "int", "int" }));
        }

        [Test]
        public void FieldWithoutColumnIsNotError()
        {
            var result = ResolveValid(typeof(UnitRow), "id");

            Assert.That(result.Columns.Select(c => c.Name), Is.EqualTo(new[] { "id" }));
        }

        [Test]
        public void CollectsAllErrors()
        {
            var result = Resolve(typeof(UnitRow), "id", "hp:float", "armor", "speed");

            Assert.That(result.Errors.Select(e => e.Code),
                Is.EqualTo(new[] { HeaderErrorCode.TypeMismatch, HeaderErrorCode.UnknownField }));
            Assert.That(result.Errors.Select(e => e.Address), Is.EqualTo(new[] { "B1", "C1" }));
            Assert.That(result.Columns.Select(c => c.Name), Is.EqualTo(new[] { "id", "speed" }));
        }

        [Test]
        public void NullArgumentsThrow()
        {
            Assert.Throws<ArgumentNullException>(() => CodeSchema.Resolve(null, Array.Empty<ColumnDefinition>()));
            Assert.Throws<ArgumentNullException>(() => CodeSchema.Resolve(typeof(UnitRow), null));
        }
    }
}
