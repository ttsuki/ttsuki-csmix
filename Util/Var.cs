#define JAVASCRIPT_LIKE_CONVERSION
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;

namespace Tsukikage.Util
{
    /// <summary>
    /// Jsonほぼ互換の、似非バリアント型。
    /// ・実数が扱えない。
    /// ・byte配列を扱う独自拡張あり。(B[DEADBEAF0123456789ABCDEF])
    /// ・文法チェック緩め 読み込み時、Objectのキーが""で囲まれてなくてもよい
    /// ・コンパイラのチェックもゆるめになるので注意。できない計算をさせると実行時例外が出ます。
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct Var : IComparable<Var>, IEquatable<Var>, ICloneable
    {
        [FieldOffset(0)]
        private VarType type;

        [FieldOffset(4)]
        private object asObject;

        [FieldOffset(4)]
        private VarList asList;

        [FieldOffset(4)]
        private VarDictionary asDictionary;

        [FieldOffset(4)]
        private string asString;

        [FieldOffset(4)]
        private byte[] asByteArray;

        [FieldOffset(12)]
        private bool asBool;

        [FieldOffset(12)]
        private long asInt;

        /// <summary>
        /// サポートしている型
        /// </summary>
        public enum VarType : byte
        {
            Null = 0x00,
            Boolean = 0x01,
            Int = 0x02,
            String = 0x03,
            ByteArray = 0x04,

            List = 0x10,
            Dictionary = 0x20,

            UNDEFINED = 0xFF,
        }

        public bool IsNull { get { return type == VarType.Null; } }
        public bool IsUndefined { get { return type == VarType.UNDEFINED; } }

        public bool IsBoolean { get { return type == VarType.Boolean; } }
        public bool IsInt { get { return type == VarType.Int; } }
        public bool IsString { get { return type == VarType.String; } }
        public bool IsBinary { get { return type == VarType.ByteArray; } }
        public bool IsList { get { return type == VarType.List; } }
        public bool IsDictionary { get { return type == VarType.Dictionary; } }

        public bool AsBoolean { get { return (bool)this; } }
        public int AsInt { get { return (int)this; } }
        public long AsLong { get { return (long)this; } }
        public string AsString { get { return (string)this; } }
        public byte[] AsBinary { get { return (byte[])this; } }
        public VarList AsList { get { return (VarList)this; } }
        public VarDictionary AsDictionary { get { return (VarDictionary)this; } }

        public static implicit operator bool(Var v)
        {
            if (v.IsNull || v.IsUndefined) return default(bool);
            if (v.IsBoolean) return v.asBool;
#if JAVASCRIPT_LIKE_CONVERSION
            if (v.IsInt) return v.asInt != 0;
            if (v.IsString) return v.asString.Length != 0;
            if (v.IsBinary) return true;
            if (v.IsList) return true;
            if (v.IsDictionary) return true;
#endif
            throw new InvalidCastException("Var[" + v.type + "]型からbool型への変換はサポートしません。");
        }

        public static implicit operator int(Var v)
        {
            if (v.IsNull || v.IsUndefined) return default(int);
            if (v.IsInt) return (int)v.asInt;
#if JAVASCRIPT_LIKE_CONVERSION
            if (v.IsBoolean) return v.asBool ? 1 : 0;
            if (v.IsString) { long r; if (long.TryParse(v.asString, out r)) return (int)r; }
#endif
            throw new InvalidCastException("Var[" + v.type + "]型からint型への変換はサポートしません。");
        }

        public static implicit operator long(Var v)
        {
            if (v.IsNull || v.IsUndefined) return default(int);
            if (v.IsInt) return v.asInt;
#if JAVASCRIPT_LIKE_CONVERSION
            if (v.IsBoolean) return v.asBool ? 1 : 0;
            if (v.IsString) { long r; if (long.TryParse(v.asString, out r)) return (int)r; }
#endif
            throw new InvalidCastException("Var[" + v.type + "]型からlong型への変換はサポートしません。");
        }

        public static implicit operator double(Var v)
        {
            if (v.IsNull || v.IsUndefined) return default(int);
            if (v.IsInt) return v.asInt;
#if JAVASCRIPT_LIKE_CONVERSION
            if (v.IsBoolean) return v.asBool ? 1 : 0;
            if (v.IsString) { double r; if (double.TryParse(v.asString, out r)) return r; }
#endif
            throw new InvalidCastException("Var[" + v.type + "]型からdouble型への変換はサポートしません。");
        }

        public static implicit operator string(Var v)
        {
            if (v.IsNull || v.IsUndefined) return default(string);
            if (v.IsString) return v.asString;
#if JAVASCRIPT_LIKE_CONVERSION
            return v.ToString(); // ちょっと違う。
#endif
            throw new InvalidCastException("Var[" + v.type + "]型からstring型への変換はサポートしません。");
        }

        public static implicit operator byte[](Var v)
        {
            if (v.IsNull || v.IsUndefined) return default(byte[]);
            if (v.IsBinary) return v.asByteArray;
            throw new InvalidCastException("Var[" + v.type + "]型からbyte[]型への変換はサポートしません。");
        }

        public static implicit operator VarList(Var v)
        {
            if (v.IsNull || v.IsUndefined) return default(VarList);
            if (v.IsList) return v.asList;
            throw new InvalidCastException("Var[" + v.type + "]型からVarList型への変換はサポートしません。");
        }

        public static implicit operator VarDictionary(Var v)
        {
            if (v.IsNull || v.IsUndefined) return default(VarDictionary);
            if (v.IsDictionary) return v.asDictionary;
            throw new InvalidCastException("Var[" + v.type + "]型からVarDictionary型への変換はサポートしません。");
        }

        public static implicit operator Var(bool data) { return new Var(data); }
        public static implicit operator Var(byte[] data) { return new Var(data); }
        public static implicit operator Var(long data) { return new Var(data); }
        public static implicit operator Var(string data) { return new Var(data); }
        public static implicit operator Var(VarList data) { return new Var(data); }
        public static implicit operator Var(VarDictionary data) { return new Var(data); }

        /// <summary>
        /// length
        /// </summary>
        public int Count
        {
            get
            {
                if (IsList) return asList.Count;
                if (IsDictionary) return asDictionary.Count;
                if (IsUndefined || IsNull) return 0;
                throw new InvalidOperationException("指定されたVar型変数はListまたはDictionaryではありません。");
            }
        }

        /// <summary>
        /// Array[index]
        /// </summary>
        /// <param name="index">Array[index]</param>
        /// <returns>Var</returns>
        public Var this[int index]
        {
            get
            {
                if (IsNull || IsUndefined) return Undefined;
                if (IsList) return asList[index];
                throw new InvalidOperationException("指定されたVar型変数は配列ではありません。");
            }

            set
            {
                if (IsList) { asList[index] = value; return; }
                throw new InvalidOperationException("指定されたVar型変数は配列ではありません。");
            }
        }

        /// <summary>
        /// Object[key]
        /// </summary>
        /// <param name="key">Object[key]</param>
        /// <returns>Var</returns>
        public Var this[string key]
        {
            get
            {
                if (IsNull || IsUndefined) return Undefined;
                if (IsDictionary) return asDictionary[key];
                throw new InvalidOperationException("指定されたVar型変数はVarDictionaryではありません。");
            }

            set
            {
                if (IsDictionary) { asDictionary[key] = value; return; }
                throw new InvalidOperationException("指定されたVar型変数はVarDictionaryではありません。");
            }
        }

        public new VarType GetType() { return type; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            switch (type)
            {
                case VarType.Null: return null;
                case VarType.Boolean: return asBool.ToString();
                case VarType.Int: return asInt.ToString();
                case VarType.String: return asString;
                case VarType.ByteArray: return "{Binary}";
                case VarType.List: return ToFormattedString();
                case VarType.Dictionary: return ToFormattedString();
                default:
                    throw new Exception("指定されたVar型は状態が変です。");
            }
        }

        public static Var Null { get { return new Var(); } }
        public static Var Undefined { get { Var v = new Var(); v.type = VarType.UNDEFINED; return v; } }

        private Var(bool data) : this() { this.type = VarType.Boolean; this.asBool = data; }
        private Var(long data) : this() { this.type = VarType.Int; this.asInt = data; }
        private Var(byte[] data) : this() { this.type = VarType.ByteArray; this.asByteArray = data; }
        private Var(string data) : this() { this.type = VarType.String; this.asString = data; }
        private Var(VarList data) : this() { this.type = VarType.List; this.asList = data; }
        private Var(VarDictionary data) : this() { this.type = VarType.Dictionary; this.asDictionary = data; }

        public static bool operator <(Var a, Var b) { return a.CompareTo(b) < 0; }
        public static bool operator >(Var a, Var b) { return a.CompareTo(b) > 0; }
        public static bool operator <=(Var a, Var b) { return a.CompareTo(b) <= 0; }
        public static bool operator >=(Var a, Var b) { return a.CompareTo(b) >= 0; }
        public static bool operator ==(Var a, Var b) { return a.Equals(b); }
        public static bool operator !=(Var a, Var b) { return !a.Equals(b); }

        public static Var operator +(Var a, Var b)
        {
            if (a.IsInt && b.IsInt) return (long)a + (long)b;
            if (a.IsString || b.IsString) return a.ToString() + b.ToString();
            throw new NotImplementedException(a.type + "型に" + b.type + "型を+できません。");
        }

        public struct VarInt
        {
            long value;
            public VarInt(long value) { this.value = value; }
            public static implicit operator int(VarInt v) { return (int)v.value; }
            public static implicit operator long(VarInt v) { return v.value; }
            public static implicit operator double(VarInt v) { return v.value; }
            public static implicit operator Var(VarInt v) { return new Var(v.value); }
            public static implicit operator VarInt(long v) { return new VarInt(v); }
        }

        public struct VarBool
        {
            bool value;
            public VarBool(bool value) { this.value = value; }
            public static implicit operator bool(VarBool v) { return v.value; }
            public static implicit operator Var(VarBool v) { return new Var(v.value); }
            public static implicit operator VarBool(bool v) { return new VarBool(v); }
        }

        public static VarInt operator +(Var a) { return +(long)a; }
        public static VarInt operator -(Var a) { return -(long)a; }
        public static VarInt operator ~(Var a) { return ~(long)a; }
        public static VarInt operator -(Var a, Var b) { return (long)a - (long)b; }
        public static VarInt operator *(Var a, Var b) { return (long)a * (long)b; }
        public static VarInt operator /(Var a, Var b) { return (long)a / (long)b; }
        public static VarInt operator %(Var a, Var b) { return (long)a % (long)b; }

        public static VarBool operator &(Var a, bool b) { return (bool)a & (bool)b; }
        public static VarBool operator &(bool a, Var b) { return (bool)a & (bool)b; }
        public static Var operator &(Var a, Var b)
        {
            if (a.IsBoolean && b.IsBoolean) return (bool)a & (bool)b;
            if (a.IsInt && b.IsInt) return (long)a & (long)b;
#if JAVASCRIPT_LIKE_CONVERSION
            if (a.IsInt || b.IsInt) return (long)a & (long)b;
#endif
            throw new NotImplementedException(a.type + "型に" + b.type + "型を&できません。");
        }

        public static VarBool operator |(Var a, bool b) { return (bool)a | (bool)b; }
        public static VarBool operator |(bool a, Var b) { return (bool)a | (bool)b; }
        public static Var operator |(Var a, Var b)
        {
            if (a.IsBoolean && b.IsBoolean) return (bool)a | (bool)b;
            if (a.IsInt && b.IsInt) return (long)a | (long)b;
#if JAVASCRIPT_LIKE_CONVERSION
            if (a.IsInt || b.IsInt) return (long)a | (long)b; /* 迷う */
#endif
            throw new NotImplementedException(a.type + "型に" + b.type + "型を|できません。");
        }

        public static VarBool operator ^(Var a, bool b) { return (bool)a ^ (bool)b; }
        public static VarBool operator ^(bool a, Var b) { return (bool)a ^ (bool)b; }
        public static Var operator ^(Var a, Var b)
        {
            if (a.IsBoolean && b.IsBoolean) return (bool)a ^ (bool)b;
            if (a.IsInt && b.IsInt) return (long)a ^ (long)b;
#if JAVASCRIPT_LIKE_CONVERSION
            if (a.IsInt || b.IsInt) return (long)a ^ (long)b;
#endif
            throw new NotImplementedException(a.type + "型に" + b.type + "型を^できません。");
        }
 
        public override int GetHashCode()
        {
            if (IsNull) return 0;
            if (IsBoolean) return ((bool)this).GetHashCode();
            if (IsInt) return ((long)this).GetHashCode();
            if (IsString) return ((string)this).GetHashCode();
            if (IsBinary) return ((byte[])this).GetHashCode();
            if (IsList) return ((VarList)this).GetHashCode();
            if (IsDictionary) return ((VarDictionary)this).GetHashCode();
            return base.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is Var ? Equals((Var)obj) : false;
        }

        public bool Equals(Var obj)
        {
            if (this.type != obj.type) return false;
            if (IsNull) return true;
            if (IsUndefined) return true;
            if (IsInt) return asInt == obj.asInt;
            if (IsBoolean) return asBool == obj.asBool;
            return asObject.Equals(obj.asObject);
        }

        public int CompareTo(Var other)
        {
            if (IsInt && other.IsInt) return ((long)this).CompareTo((long)other);
#if JAVASCRIPT_LIKE_CONVERSION
            if (IsBoolean && other.IsBoolean) return ((long)this).CompareTo((long)other);
            if (IsString && other.IsString) return string.CompareOrdinal(this, other);
            if (IsInt || other.IsInt) return ((long)this).CompareTo((long)other);
#endif
            throw new NotImplementedException(type + "型と" + other.type + "型は比較できません。");
        }

        // From bool[]
        public static implicit operator Var(bool[] array)
        {
            return new VarList(Array.ConvertAll<bool, Var>(array, delegate(bool v) { return v; }));
        }

        // From int[]
        public static implicit operator Var(int[] array)
        {
            return new VarList(Array.ConvertAll<int, Var>(array, delegate(int v) { return v; }));
        }

        // From long[]
        public static implicit operator Var(long[] array)
        {
            return new VarList(Array.ConvertAll<long, Var>(array, delegate(long v) { return v; }));
        }

        // From string[]
        public static implicit operator Var(string[] array)
        {
            return new VarList(Array.ConvertAll<string, Var>(array, delegate(string v) { return v; }));
        }

        // From Var[]
        public static implicit operator Var(Var[] array)
        {
            return new VarList(array);
        }

        // To bool[]
        public static implicit operator bool[](Var array)
        {
            return Array.ConvertAll<Var, bool>(array.AsList.ToArray(), delegate(Var v) { return v; });
        }

        // To int[]
        public static implicit operator int[](Var array)
        {
            return Array.ConvertAll<Var, int>(array.AsList.ToArray(), delegate(Var v) { return v; });
        }

        // To long[]
        public static implicit operator long[](Var array)
        {
            return Array.ConvertAll<Var, long>(array.AsList.ToArray(), delegate(Var v) { return v; });
        }

        // To string[]
        public static implicit operator string[](Var array)
        {
            return Array.ConvertAll<Var, string>(array.AsList.ToArray(), delegate(Var v) { return v; });
        }

        // To Var[]
        public static implicit operator Var[](Var array)
        {
            return array.AsList.ToArray();
        }

        string EncodeString(string input)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '\n') sb.Append("\\n");
                else if (input[i] == '\\') sb.Append("\\\\");
                else if (input[i] == '\b') sb.Append("\\b");
                else if (input[i] == '\f') sb.Append("\\f");
                else if (input[i] == '\r') sb.Append("\\r");
                else if (input[i] == '\t') sb.Append("\\t");
                else if (input[i] == '\"') sb.Append("\\\"");
                else if (input[i] == '<') sb.Append("\\<");
                else if (input[i] == '>') sb.Append("\\>");
                //else if (input[i] > 127) sb.Append(string.Format("\\u{0:X4}", (int)input[i]));
                else sb.Append(input[i]);

            }
            return sb.ToString();
        }

        string ToFormattedString(int indent, bool useIndent)
        {
            switch (type)
            {
                case VarType.Null: return "null";
                case VarType.Boolean: return asBool ? "true" : "false";
                case VarType.Int: return asInt.ToString();
                case VarType.String: return "\"" + EncodeString(asString) + "\"";
                case VarType.ByteArray:
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.Append("B[");
                        for (int i = 0; i < asByteArray.Length; i++)
                            sb.Append(string.Format("{0:X2}", asByteArray[i]));
                        sb.Append(']');
                        return sb.ToString();
                    }
                case VarType.List:
                    {
                        if (asList.Count == 0)
                            return "[]";

                        StringBuilder sb = new StringBuilder();
                        sb.Append('[');
                        if (useIndent) sb.Append('\n');
                        foreach (Var v in asList)
                        {
                            if (useIndent) sb.Append(new string('\t', indent + 1));
                            sb.Append(v.ToFormattedString(indent + 1, useIndent));
                            sb.Append(",");
                            if (useIndent) sb.Append("\n");
                        }
                        if (useIndent) sb.Remove(sb.Length - 1, 1);
                        sb.Remove(sb.Length - 1, 1);
                        if (useIndent) sb.Append('\n');
                        if (useIndent) sb.Append(new string('\t', indent));
                        sb.Append("]");
                        return sb.ToString();
                    }

                case VarType.Dictionary:
                    {
                        if (asDictionary.Count == 0)
                            return "{}";

                        StringBuilder sb = new StringBuilder();
                        sb.Append('{');
                        if (useIndent) sb.Append('\n');
                        foreach (string key in asDictionary.Keys)
                        {
                            if (useIndent) sb.Append(new string('\t', indent + 1));
                            sb.Append("\"" + EncodeString(key) + "\"");
                            if (useIndent) sb.Append(' ');
                            sb.Append(':');
                            if (useIndent) sb.Append(' ');
                            sb.Append(asDictionary[key].ToFormattedString(indent + 1, useIndent));
                            sb.Append(',');
                            if (useIndent) sb.Append('\n');
                        }
                        if (useIndent) sb.Remove(sb.Length - 1, 1);
                        sb.Remove(sb.Length - 1, 1);
                        if (useIndent) sb.Append('\n');
                        if (useIndent) sb.Append(new string('\t', indent));
                        sb.Append("}");
                        return sb.ToString();
                    }

                default:
                    throw new Exception("指定されたVar型は状態が変です。");
            }
        }

        /// <summary>
        /// Json形式の文字列に変換します。
        /// </summary>
        /// <returns></returns>
        public string ToFormattedString()
        {
            return ToFormattedString(0, true);
        }

        /// <summary>
        /// インデントや改行を省いた1行のJson形式に変換します。
        /// </summary>
        /// <returns></returns>
        public string ToCompressedFormattedString()
        {
            return ToFormattedString(0, false);
        }

        /// <summary>
        /// 参照を共有しないオブジェクトの完全なコピーを作ります。
        /// でかいデータを含んでると遅い。
        /// </summary>
        /// <returns>コピー</returns>
        public Var Clone()
        {
            return FromFormattedString(ToCompressedFormattedString());
        }

        /// <summary>
        /// 参照を共有しないオブジェクトの完全なコピーを作ります。
        /// でかいデータを含んでると遅い。
        /// </summary>
        /// <returns>コピー</returns>
        object ICloneable.Clone()
        {
            return Clone();
        }

        /// <summary>
        /// Json形式からVarを生成します。
        /// </summary>
        /// <param name="serialized">文字列</param>
        /// <exception cref="FormatException">文法エラー</exception>
        /// <returns></returns>
        public static Var FromFormattedString(string serialized)
        {
            return VarSerializer.Parse(serialized);
        }

        /// <summary>
        /// ストリームを読んでVarを生成します。
        /// ストリームはUtf-8でエンコードされていると仮定します。
        /// </summary>
        /// <param name="serialized">文字列</param>
        /// <exception cref="FormatException">文法エラー</exception>
        /// <returns></returns>
        public static Var FromFormattedStream(Stream serialized)
        {
            return VarSerializer.Load(serialized);
        }

        class VarSerializer
        {
            TextReader textReader;
            StringBuilder readString;
            char cur;

            private VarSerializer(TextReader textReader)
            {
                this.textReader = textReader;
                this.readString = new StringBuilder(4096);
                Next();
            }

            void Next()
            {
                int x = textReader.Read();
                if (x != -1)
                {
                    readString.Append((char)x);
                    cur = (char)x;
                }
                else
                {
                    cur = '\0';
                }
            }

            void Abort(string message)
            {
                string[] lines = readString.ToString().Split('\n');
                throw new FormatException("文法エラー。" + message + "\n" + lines.Length + "行目 " + lines[lines.Length - 1] + " ←ここが変。");
            }

            void Assert(bool exp, string message)
            {
                if (!exp) Abort(message);
            }

            void Assert(bool exp)
            {
                if (cur == '\0') Assert(exp, "予期せぬEOFに出会いました。");
                else Assert(exp, "解釈できませんでした。");
            }

            Var ReadObject()
            {
                SkipWhiteSpace();
                Assert(cur != '\0');
                switch (cur)
                {
                    case '0': 
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                    case '-': return ReadNumber();
                    case 'N':
                    case 'n': return ReadNull();
                    case 'T':
                    case 't': return ReadTrue();
                    case 'F':
                    case 'f': return ReadFalse();
                    case '"': return ReadString();
                    case '[': return ReadList();
                    case '{': return ReadDictionary();
                    case 'B': return ReadByteArray();
                    case '\0': Assert(false); break;
                    default: Assert(false); break;
                }
                return Var.Null; /* not reachable */
            }

            Var ReadNull()
            {
                Assert(cur == 'n' || cur == 'N'); Next();
                Assert(cur == 'u' || cur == 'U'); Next();
                Assert(cur == 'l' || cur == 'L'); Next();
                Assert(cur == 'l' || cur == 'L'); Next();
                return Var.Null;
            }

            Var ReadTrue()
            {
                Assert(cur == 't' || cur == 'T'); Next();
                Assert(cur == 'r' || cur == 'R'); Next();
                Assert(cur == 'u' || cur == 'U'); Next();
                Assert(cur == 'e' || cur == 'E'); Next();
                return true;
            }

            Var ReadFalse()
            {
                Assert(cur == 'f' || cur == 'F'); Next();
                Assert(cur == 'a' || cur == 'A'); Next();
                Assert(cur == 'l' || cur == 'L'); Next();
                Assert(cur == 's' || cur == 'S'); Next();
                Assert(cur == 'e' || cur == 'E'); Next();
                return false;
            }

            Var ReadNumber()
            {
                bool minus = false;
                long value = 0;

                if (cur == '-')
                {
                    minus = true;
                    Next();
                }

                while (cur >= '0' && cur <= '9')
                {
                    value = value * 10 + (cur - '0');
                    Next();
                }
                if (minus) return -value;
                return value;
            }

            Var ReadByteArray()
            {
                Assert(cur == 'B'); Next();
                Assert(cur == '['); Next();

                List<byte> bytes = new List<byte>();
                while (true)
                {
                    Assert(cur != '\0');
                    if (cur == ']') break;
                    int b = Hex(cur); Next();
                    b = b * 16 + Hex(cur); Next();
                    bytes.Add((byte)b);
                }
                Assert(cur == ']'); Next();
                return bytes.ToArray();
            }

            VarList ReadList()
            {
                Assert(cur == '['); Next();
                VarList list = new VarList();
                while (true)
                {
                    SkipWhiteSpace();
                    if (cur == ']') break;
                    Assert(cur != '\0');
                    list.Add(ReadObject());
                    SkipWhiteSpace();
                    Assert(cur == ']' || cur == ',');
                    if (cur == ',') Next();
                }

                Next();
                return list;
            }

            VarDictionary ReadDictionary()
            {
                Assert(cur == '{'); Next();
                VarDictionary dict = new VarDictionary();
                while (true)
                {
                    SkipWhiteSpace();
                    if (cur == '}') break;
                    string key = ReadKey();
                    SkipWhiteSpace();
                    Assert(cur == ':'); Next();
                    SkipWhiteSpace();
                    dict.Add(key, ReadObject());
                    SkipWhiteSpace();
                    Assert(cur == ',' || cur == '}');
                    if (cur == ',') Next();
                }

                Next();
                return dict;
            }

            void SkipWhiteSpace()
            {
                bool inBlockComment = false;
                bool inLinerComment = false;
                while (cur != '\0')
                {
                    if (inBlockComment)
                    {
                        if (cur == '*')
                        {
                            Next();
                            if (cur == '/')
                            {
                                Next();
                                inBlockComment = false;
                            }
                        }
                        else
                        {
                            Next();
                        }
                    }
                    else if (inLinerComment)
                    {
                        if (cur == '\n')
                        {
                            inLinerComment = false;
                            Next();
                        }
                        else
                        {
                            Next();
                        }
                    }
                    else
                    {
                        if (cur <= ' ')
                        {
                            Next();
                        }
                        else if (cur == '/')
                        {
                            Next();
                            if (cur == '*')
                            {
                                inBlockComment = true;
                                Next();
                            }
                            else if (cur == '/')
                            {
                                inLinerComment = true;
                                Next();
                            }
                            else
                            {
                                Abort("コメントかと思ったら違いました。");
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            string ReadKey()
            {
                if (cur == '"' || cur == '\'')
                    return ReadString();

                StringBuilder sb = new StringBuilder();
                while (cur > ' ' && cur != ':')
                {
                    Assert(cur != '\0');
                    sb.Append(cur);
                    Next();
                }
                return sb.ToString();
            }

            string ReadString()
            {
                Assert(cur == '\"' || cur == '\'', "文字列？がQuoteで始まっていません。");

                char q = cur;
                StringBuilder sb = new StringBuilder();
                bool escape = false;
                while (true)
                {
                    Assert(cur != '\0');
                    Next();
                    if (escape)
                    {
                        escape = false;
                        switch (cur)
                        {
                            case 'n': sb.Append('\n'); break;
                            case 't': sb.Append('\t'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'r': sb.Append('\r'); break;
                            case '"': sb.Append('"'); break;
                            case '\'': sb.Append('\''); break;
                            case '<': sb.Append('<'); break;
                            case '>': sb.Append('>'); break;
                            case 'u':
                                int u = Hex(cur);
                                u = u * 16 + Hex(cur);
                                u = u * 16 + Hex(cur);
                                u = u * 16 + Hex(cur);
                                sb.Append((char)u);
                                break;
                        }
                    }
                    else if (cur == '\\')
                    {
                        escape = true;
                    }
                    else if (cur == q)
                    {
                        Next();
                        break;
                    }
                    else
                    {
                        sb.Append(cur);
                    }
                }
                return sb.ToString();
            }

            int Hex(char c)
            {
                if (c >= '0' && c <= '9') return c - '0';
                if (c >= 'A' && c <= 'F') return c - 'A' + 10;
                if (c >= 'a' && c <= 'f') return c - 'a' + 10;
                Abort("16進数に変換できませんでした。");
                return 0; /* no reachable */
            }

            public static Var Load(Stream s)
            {
                return Read(new StreamReader(s, true));
            }
            public static Var Load(Stream s, Encoding encoding)
            {
                return Read(new StreamReader(s, encoding));
            }

            public static Var Parse(string s)
            {
                return Read(new StringReader(s));
            }

            public static string ToString(Var v)
            {
                return v.ToString();
            }

            public static Var Read(TextReader textReader)
            {
                return new VarSerializer(textReader).ReadObject();
            }
        }
    }

    /// <summary>
    /// Var配列型
    /// </summary>
    public class VarList : List<Var>
    {
        /// <summary>
        /// 新しいVar配列を準備します。
        /// </summary>
        public VarList()
            : base()
        {
        }

        /// <summary>
        /// 新しいVar配列を準備します。
        /// </summary>
        /// <param name="collection">新しいオブジェクトにあらかじめコピーしておくデータ</param>
        public VarList(IEnumerable<Var> collection)
            : base(collection)
        {
        }

        public static Var FromFormattedString(string serialized) { return Var.FromFormattedString(serialized); }
    }

    /// <summary>
    /// Varオブジェクト型
    /// </summary>
    public class VarDictionary : Dictionary<string, Var>
    {
        /// <summary>
        /// 新しいVarオブジェクトを準備します。
        /// </summary>
        public VarDictionary()
            : base()
        {
        }

        /// <summary>
        /// 新しいVarオブジェクト型を準備します。
        /// </summary>
        /// <param name="dictionary">新しいオブジェクトにあらかじめコピーしておくデータ</param>
        public VarDictionary(IDictionary<string, Var> dictionary)
            : base(dictionary)
        {
        }

        /// <summary>
        /// 指定したキーに関連付けられている値を取得します。
        /// おかしな値をとりだそうとするとVar.Nullが返ります。
        /// </summary>
        /// <param name="key">キー</param>
        /// <returns>値</returns>
        public new Var this[string key]
        {
            get
            {
                Var v;
                if (TryGetValue(key, out v))
                    return v;
                return Var.Null;
            }
            set
            {
                base[key] = value;
            }
        }
        public static Var FromFormattedString(string serialized){ return Var.FromFormattedString(serialized);}
    }
}