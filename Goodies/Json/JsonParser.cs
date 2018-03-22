﻿using BusterWood.Collections;
using BusterWood.Contracts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BusterWood.Json
{
    public class Parser
    {
        static readonly object _true = true;    // pre-boxed value
        static readonly object _false = false;  // pre-boxed value
        static readonly object[] _emptyArray = new object[0];  // pre-boxed value

        readonly Scanner scanner;
        readonly Dictionary<string, string> _names = new Dictionary<string, string>();

        public Parser(string text) : this(new StringReader(text))
        {
        }

        public Parser(TextReader reader) : this(new Scanner(reader))
        {
        }

        public Parser(Scanner scanner)
        {
            Contract.RequiresNotNull(scanner);
            this.scanner = scanner;
        }

        /// <summary>Read a JSON document, i.e. an array or an object.  Note empty input returns NULL</summary>
        /// <exception cref="ParseException">thrown in a parse error</exception>
        public object Read()
        {
            scanner.MoveNext();
            switch (scanner.Current.Type)
            {
                case Type.StartObject:
                    return ReadObjectBody();
                case Type.StartArray:
                    return ReadArrayBody();
                case 0:
                    return null;
                default:
                    throw new ParseException($"Unexpected {scanner.Current.Type} '{scanner.Current.Text}' at {scanner.Current.Index}");
            }
        }

        /// <summary>Reads a JSON object</summary>
        /// <exception cref="ParseException">thrown in a parse error</exception>
        public Dictionary<string, object> ReadObject()
        {
            Read(Type.StartObject);
            return ReadObjectBody();
        }

        public Dictionary<string, object> ReadObjectBody()
        {
            var result = new Dictionary<string, object>();

            switch (scanner.Next.Type)
            {
                case Type.EndObject:
                    scanner.MoveNext();
                    return result;
                case Type.String:
                    break;
                default:
                    throw new ParseException($"Expected a name or end of object but got end-of-file at {scanner.Next.Index}");
            }

            for (;;)
            {
                Read(Type.String);
                string name = ReadName();
                Read(Type.Colon);
                object value = ReadValue();

                result.Add(name, value); // TODO: what about duplicate names?

                switch (scanner.Next.Type)
                {
                    case Type.Comma:
                        scanner.MoveNext();
                        break;
                    case Type.EndObject:
                        scanner.MoveNext();
                        return result;
                    default:
                        throw new ParseException($"Expected a comma or end of object but got {scanner.Next.Type} '{scanner.Next.Text}' at {scanner.Next.Index}");
                }
            }
        }

        private string ReadName()
        {
            string name = scanner.Current.Text;
            return _names.GetOrAdd(name, name); // cache and reuse names
        }

        private void Read(Type expected)
        {
            scanner.MoveNext();
            if (scanner.Current.Type != expected)
                throw new ParseException($"Expected a {expected} but got {scanner.Current.Type} '{scanner.Current.Text}' at {scanner.Current.Index}");
        }

        private object ReadValue()
        {
            scanner.MoveNext();
            switch (scanner.Current.Type)
            {
                case Type.Null:
                    return null;
                case Type.True:
                    return _true;
                case Type.False:
                    return _false;
                case Type.String:
                    return scanner.Current.Text;
                case Type.Number:
                    return ReadNumber();
                case Type.StartObject:
                    return ReadObjectBody();
                case Type.StartArray:
                    return ReadArrayBody();
                default:
                    throw new ParseException($"Unexpected {scanner.Current.Type} '{scanner.Current.Text}' at {scanner.Current.Index}");
            }
        }

        private object ReadNumber()
        {
            var txt = scanner.Current.Text;
            if (txt.IndexOf('.') > 0)
                return double.Parse(txt);
            else if (txt.Length >= 10) // getting close to int.MaxValue
                return long.Parse(txt);
            else
                return int.Parse(txt);
        }

        /// <summary>Reads a JSON array</summary>
        /// <exception cref="ParseException">thrown in a parse error</exception>
        public IList ReadArray()
        {
            Read(Type.StartArray);
            return ReadArrayBody();
        }

        private IList ReadArrayBody()
        {
            switch (scanner.Next.Type)
            {
                case 0:
                    throw new ParseException($"Expected a value or end of array but got end-of-file at {scanner.Next.Index}");
                case Type.EndArray:
                    scanner.MoveNext();
                    return _emptyArray;
            }

            // optimization for array of integers
            if (scanner.Next.IsInteger)
                return TryReadIntArrayBody();

            // optimization for array of doubles
            if (scanner.Next.IsDouble)
                return TryReadDoubleArrayBody();

            var result = new List<object>();
            return ReadArrayBody(result);
        }

        private IList TryReadIntArrayBody()
        {
            var numbers = new List<int>();
            for (;;)
            {
                var next = scanner.Next;
                if (!next.IsInteger)
                    break; // fall back to List<object>
                numbers.Add(int.Parse(next.Text));
                scanner.MoveNext();

                switch (scanner.Next.Type)
                {
                    case Type.Comma:
                        scanner.MoveNext();
                        break;
                    case Type.EndArray:
                        scanner.MoveNext();
                        return numbers;
                    default:
                        throw new ParseException($"Expected a comma or end of array but got {scanner.Next.Type} '{scanner.Next.Text}' at {scanner.Next.Index}");
                }
            }

            // array contains a mixture of values, fall back to a list of objects
            var objs = new List<object>(numbers.Count);
            foreach (var i in numbers)
                objs.Add(i);
            return ReadArrayBody(objs);
        }

        private IList TryReadDoubleArrayBody()
        {
            var numbers = new List<double>();
            for (;;)
            {
                var next = scanner.Next;
                if (next.Type != Type.Number)
                    break; // fall back to List<object>
                numbers.Add(double.Parse(next.Text));
                scanner.MoveNext();

                switch (scanner.Next.Type)
                {
                    case Type.Comma:
                        scanner.MoveNext();
                        break;
                    case Type.EndArray:
                        scanner.MoveNext();
                        return numbers;
                    default:
                        throw new ParseException($"Expected a comma or end of array but got {scanner.Next.Type} '{scanner.Next.Text}' at {scanner.Next.Index}");
                }
            }

            // array contains a mixture of values, fall back to a list of objects
            var objs = new List<object>(numbers.Count);
            foreach (var d in numbers)
                objs.Add(d);
            return ReadArrayBody(objs);
        }

        private IList ReadArrayBody(List<object> objs)
        {
            for (;;)
            {
                object value = ReadValue();

                objs.Add(value);

                switch (scanner.Next.Type)
                {
                    case Type.Comma:
                        scanner.MoveNext();
                        break;
                    case Type.EndArray:
                        scanner.MoveNext();
                        return objs;
                    default:
                        throw new ParseException($"Expected a comma or end of array but got {scanner.Next.Type} '{scanner.Next.Text}' at {scanner.Next.Index}");
                }
            }
        }

        /// <remarks>Nested in the parser, you should not need this separately</remarks>
        public class Scanner : IEnumerator<Token>
        {
            readonly TextReader reader;
            int index;
            char[] buffer = new char[16]; // use our own string builder for 20% faster scanning
            int bufferLen;

            public Scanner(TextReader reader)
            {
                Contract.RequiresNotNull(reader);
                this.reader = reader;
            }

            /// <summary>Current token, <see cref="Token.Type"/> will be zero at end of stream</summary>
            public Token Current { get; private set; }

            /// <summary>The token after the <see cref="Current"/>, <see cref="Token.Type"/> will be zero at end of stream</summary>
            public Token Next { get; private set; }

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                if (BeforeFirstToken())
                {
                    Current = Read();
                    Next = Read();
                }
                else
                {
                    Current = Next;
                    Next = Read();
                }
                return Current.Type != 0;
            }

            private bool BeforeFirstToken() => index == 0;

            private Token Read()
            {
                for (;;)
                {
                    int ch = reader.Read();
                    if (ch == -1)
                        return new Token(index, null, 0);

                    index++;
                    
                    switch (ch)
                    {
                        case ' ':
                        case '\t':
                        case '\n':
                        case '\r':
                        case '\f':
                            break; // whitespace
                        case '"':
                            return ReadString();
                        case '-':
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
                            return ReadNumber((char)ch);
                        case 't':
                            return ReadTrue();
                        case 'f':
                            return ReadFalse();
                        case 'n':
                            return ReadNull();
                        case '{':
                            return new Token(index, "{", Type.StartObject);
                        case '}':
                            return new Token(index, "}", Type.EndObject);
                        case '[':
                            return new Token(index, "[", Type.StartArray);
                        case ']':
                            return new Token(index, "]", Type.EndArray);
                        case ':':
                            return new Token(index, ":", Type.Colon);
                        case ',':
                            return new Token(index, ",", Type.Comma);
                        default:
                            throw new ParseException($"Unexpected '{(char)ch}' at {index}");
                    }
                }
            }

            private Token ReadTrue()
            {
                int startIdx = index;
                Expect("rue", Type.True);
                CheckEnded(Type.True);
                return new Token(startIdx, "true", Type.True);
            }

            private Token ReadFalse()
            {
                int startIdx = index;
                Expect("alse", Type.False);
                CheckEnded(Type.False);
                return new Token(startIdx, "false", Type.False);
            }

            private Token ReadNull()
            {
                int startIdx = index;
                Expect("ull", Type.Null);
                CheckEnded(Type.Null);
                return new Token(startIdx, "null", Type.Null);
            }

            private void Expect(string expected, Type type)
            {
                foreach (var e in expected)
                {
                    int ch = reader.Read();
                    index++;
                    if (ch != e)
                        throw new ParseException($"Expected '{e}' when reading {type} but got '{(char)ch}' at {index}");
                }
            }

            private void CheckEnded(Type type)
            {
                var next = reader.Peek();
                if (next >= 0 && char.IsLetterOrDigit((char)next))
                    throw new ParseException($"Unexpected '{(char)next}' after {type} at {index+1}");
            }

            private Token ReadString()
            {
                int startIdx = index;
                ClearBuf();
                for (;;)
                {
                    int next = reader.Read();
                    if (next == -1)
                        throw new ParseException($"Expected end of string but got end of file at {index}");

                    index++;

                    switch ((char)next)
                    {
                        case '\\':
                            AddToBuf(ReadEscapeChar());
                            break;
                        case '"':
                            CheckEnded(Type.String);
                            return new Token(startIdx, BufToString(), Type.String);
                        case '\b':
                        case '\f':
                        case '\n':
                        case '\r':
                        case '\t':
                            throw new ParseException($"Unexpected character '{next:X}' in string at {index}");
                        default:
                            AddToBuf((char)next);
                            break;
                    }
                }
            }

            private char ReadEscapeChar()
            {
                int next = reader.Read();
                if (next == -1)
                    throw new ParseException($"Expected end of string but got end of file at {index}");

                index++;
                char ch = (char)next;

                switch (ch)
                {
                    case '\\':
                    case '/':
                    case '"':
                        return ch;
                    case 'b':
                        return '\b';
                    case 'f':
                        return '\f';
                    case 'n':
                        return '\n';
                    case 'r':
                        return '\r';
                    case 't':
                        return '\t';
                    case 'u':
                        return ReadUnicode();
                    default:
                        throw new ParseException($"Unexpected '\\{ch}' in string at {index}");
                }
            }

            private char ReadUnicode()
            {
                var temp = new char[4];
                for (int i = 0; i < temp.Length; i++)
                {
                    int next = reader.Read();
                    if (next < 0)
                        throw new ParseException($"Unexpected end of Unicode escape sequence in String at {index}");
                    index++;
                    var ch = (char)next;
                    if (!char.IsNumber(ch))
                        throw new ParseException($"Unexpected '{ch}' in Unicode escape sequence in String at {index}");
                    temp[i] = ch;
                }
                return (char)int.Parse(new string(temp), System.Globalization.NumberStyles.HexNumber);
            }

            private Token ReadNumber(char firstChar)
            {
                int startIdx = index;
                ClearBuf();
                AddToBuf(firstChar);

                if (!ReadDigits() && firstChar == '-')
                    throw new ParseException($"Expected to read some digits after '-' at {index}");

                int next = reader.Peek();
                if (next == '.')
                {
                    AddToBuf('.');
                    reader.Read();
                    index++;
                    if (!ReadDigits())
                        throw new ParseException($"Expected to read some digits after '.' at {index}");
                }

                //TODO: E+-digits
                CheckEnded(Type.Number);
                return new Token(startIdx, BufToString(), Type.Number);
            }

            bool ReadDigits()
            {
                bool readDigit = false;
                for (;;)
                {
                    int next = reader.Peek();
                    if (next == -1)
                        return readDigit;

                    char ch = (char)next;
                    if (char.IsNumber(ch))
                    {
                        AddToBuf(ch);
                        readDigit = true;
                        reader.Read(); // we peeked above, move to next char
                        index++;
                    }
                    else
                        return readDigit;
                }
            }

            void ClearBuf() => bufferLen = 0;

            void AddToBuf(char ch)
            {
                if (bufferLen == buffer.Length)
                    Array.Resize(ref buffer, bufferLen * 2);
                buffer[bufferLen++] = ch;
            }

            string BufToString() => new string(buffer, 0, bufferLen);

            public void Dispose()
            {
            }

            public void Reset() => throw new NotImplementedException();
        }

        public struct Token
        {
            public int Index { get; }
            public string Text { get; }
            public Type Type { get; }

            public Token(int index, string text, Type type)
            {
                Index = index;
                Text = text;
                Type = type;
            }

            public bool HasValue => Type != 0;
            internal bool IsInteger => Type == Type.Number && Text.IndexOf('.') < 0 && Text.Length < 10;
            internal bool IsDouble => Type == Type.Number && Text.IndexOf('.') > 0;
        }

        public enum Type
        {
            StartObject = 1,
            EndObject,
            StartArray,
            EndArray,
            Number,
            String,
            True,
            False,
            Null,
            Colon,
            Comma,
        }
    }

}
